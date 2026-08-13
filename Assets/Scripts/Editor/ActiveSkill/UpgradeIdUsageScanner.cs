using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Finds every place an upgrade id is consumed inside a tree's owning skill/passive assets.
///
/// This walks SerializedObjects rather than adding another virtual to the runtime types.
/// SkillDefinitionBase.CollectUpgradeIds already fans out for the *set* of ids, but it cannot
/// report where each id came from, and the "Edit" jump needs an exact property path. Walking the
/// serialized data gives both, costs the runtime nothing, and picks up new gated types
/// automatically instead of failing silently when someone forgets to override a virtual.
/// </summary>
public static class UpgradeIdUsageScanner
{
    const string StepsArrayPrefix = "steps.Array.data[";

    // The general gate field. Every conditional-application struct and SkillEffectStep uses it.
    const string RequiredUpgradeIdField = "requiredUpgradeId";

    // TriggeredPassiveDef's numeric overrides use a different field name. Matched only inside that
    // structure so an unrelated future field called "upgradeId" is not read as a gate.
    const string PassiveOverrideIdField = "upgradeId";
    const string PassiveOverrideContainer = "upgradeOverrides.Array.data[";

    public static Dictionary<string, List<UpgradeIdUsage>> Scan(IReadOnlyList<SkillDefinitionBase> owners)
    {
        var result = new Dictionary<string, List<UpgradeIdUsage>>(StringComparer.Ordinal);
        if (owners == null)
            return result;

        for (int i = 0; i < owners.Count; i++)
        {
            SkillDefinitionBase owner = owners[i];
            if (owner == null)
                continue;

            var skill = owner as SkillGemDefinition;
            CompositeSkillPayloadDef composite = skill != null
                ? skill.payload as CompositeSkillPayloadDef
                : null;

            // Target labels come from the route declarations themselves, so a new payload/step type
            // that carries a ConditionalStatusRoute is reported here without touching this scanner.
            List<SkillStatusRoute> routes = skill != null
                ? SkillStatusRouteResolver.Resolve(skill)
                : new List<SkillStatusRoute>();

            foreach (UnityEngine.Object asset in EnumerateAssetObjects(owner))
                ScanObject(owner, composite, routes, asset, result);
        }

        return result;
    }

    // Embedded payloads are separate UnityEngine.Objects inside the same .asset file, and a
    // SerializedObject never follows an object reference into them -- so each one must be walked
    // in its own right.
    static IEnumerable<UnityEngine.Object> EnumerateAssetObjects(SkillDefinitionBase owner)
    {
        string path = AssetDatabase.GetAssetPath(owner);
        if (string.IsNullOrEmpty(path))
        {
            // In-memory asset (smoke tests, unsaved authoring) -- only the owner itself exists.
            yield return owner;
            yield break;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] != null)
                yield return assets[i];
        }
    }

    static void ScanObject(
        SkillDefinitionBase owner,
        CompositeSkillPayloadDef composite,
        IReadOnlyList<SkillStatusRoute> routes,
        UnityEngine.Object asset,
        Dictionary<string, List<UpgradeIdUsage>> result)
    {
        var serialized = new SerializedObject(asset);
        SerializedProperty iterator = serialized.GetIterator();

        // enterChildren: true on every step so private and [HideInInspector] fields are visited --
        // SkillEffectStep.requiredUpgradeId is private, and the spec override flags are hidden.
        while (iterator.Next(true))
        {
            if (iterator.propertyType != SerializedPropertyType.String)
                continue;

            if (!IsUpgradeIdField(asset, iterator))
                continue;

            string rawId = iterator.stringValue;
            if (string.IsNullOrWhiteSpace(rawId))
                continue;

            UpgradeIdUsage usage = BuildUsage(owner, composite, routes, asset, serialized, iterator, rawId.Trim());
            if (!result.TryGetValue(usage.UpgradeId, out List<UpgradeIdUsage> usages))
            {
                usages = new List<UpgradeIdUsage>();
                result[usage.UpgradeId] = usages;
            }

            usages.Add(usage);
        }
    }

    static bool IsUpgradeIdField(UnityEngine.Object asset, SerializedProperty property)
    {
        if (string.Equals(property.name, RequiredUpgradeIdField, StringComparison.Ordinal))
            return true;

        return string.Equals(property.name, PassiveOverrideIdField, StringComparison.Ordinal) &&
               asset is PassiveDefinition &&
               property.propertyPath.Contains(PassiveOverrideContainer);
    }

    static UpgradeIdUsage BuildUsage(
        SkillDefinitionBase owner,
        CompositeSkillPayloadDef composite,
        IReadOnlyList<SkillStatusRoute> routes,
        UnityEngine.Object asset,
        SerializedObject serialized,
        SerializedProperty idProperty,
        string upgradeId)
    {
        string path = idProperty.propertyPath;
        int stepIndex = ResolveStepIndex(composite, asset, path);
        SkillEffectStep step = ResolveStep(composite, stepIndex);

        var usage = new UpgradeIdUsage
        {
            UpgradeId = upgradeId,
            Owner = asset,
            OwningSkill = owner,
            PropertyPath = path,
            StepIndex = stepIndex,
            SourceLabel = BuildSourceLabel(owner, asset, step, stepIndex, path),
        };

        SerializedProperty spec = FindSiblingSpec(serialized, path);
        if (spec != null)
        {
            var effect = spec.FindPropertyRelative("effect")?.objectReferenceValue as StatusEffectDef;
            string target = ResolveTargetLabel(routes, asset, path);
            string effectName = effect != null ? effect.name : "<missing Status Effect Def>";
            if (string.IsNullOrEmpty(target))
                target = "<unresolved target>";

            usage.Summary = $"Apply {effectName} to {target}";
            DescribeSpec(spec, effect, usage.Details);
        }
        else if (step is PayloadStep payloadStep && payloadStep.Payload is HealAreaSkillPayloadDef && stepIndex >= 0)
        {
            // Migrated form: target/statusSpecApplications now live on the embedded
            // HealAreaSkillPayloadDef sub-asset, a separate UnityEngine.Object from the composite,
            // so it needs its own SerializedObject rather than a composite-relative path.
            var payloadSerialized = new SerializedObject(payloadStep.Payload);
            DescribeHealAreaFields(
                payloadSerialized.FindProperty("target"),
                payloadSerialized.FindProperty("statusSpecApplications"),
                usage);
        }
        else
        {
            usage.Summary = $"Enable {BuildBehaviorLabel(asset, step, path)}";
        }

        return usage;
    }

    // HealAreaSkillPayloadDef's gate has no sibling spec (the gate covers the whole payload, not
    // one status application), so without this it falls back to "Enable Heal Area Payload" and
    // hides exactly the numbers a designer needs: which target mode, which FinalSkillStats channel
    // feeds it, and any status it always applies once unlocked. targetProperty/applications are
    // passed in rather than resolved here so the caller decides where they live (a standalone
    // SerializedObject rooted on the embedded payload sub-asset, in the current single caller).
    static void DescribeHealAreaFields(
        SerializedProperty targetProperty, SerializedProperty applications, UpgradeIdUsage usage)
    {
        bool alliesMode = targetProperty != null && targetProperty.enumValueIndex == (int)HealTargetMode.Allies;

        usage.Summary = alliesMode ? "Heal nearby Allies" : "Heal Self";
        usage.ReadsHealPowerStat = true;
        usage.ReadsAreaRadiusStat = alliesMode;

        usage.Details.Add("Heal Power channel: FinalSkillStats.healPower");
        if (alliesMode)
            usage.Details.Add("Area Radius channel: FinalSkillStats.areaRadius");

        if (applications == null || !applications.isArray)
            return;

        for (int i = 0; i < applications.arraySize; i++)
        {
            SerializedProperty spec = applications.GetArrayElementAtIndex(i).FindPropertyRelative("spec");
            if (spec == null)
                continue;

            var effect = spec.FindPropertyRelative("effect")?.objectReferenceValue as StatusEffectDef;
            if (effect == null)
            {
                usage.Warnings.Add("A status application on this step is missing its Status Effect Def.");
                continue;
            }

            usage.Details.Add($"Applies {effect.name}");
            DescribeSpec(spec, effect, usage.Details);
        }
    }

    // A conditional application is always "{...}.requiredUpgradeId" beside "{...}.spec". A bare
    // step gate has no sibling spec, which is exactly how the two are told apart.
    static SerializedProperty FindSiblingSpec(SerializedObject serialized, string path)
    {
        int lastDot = path.LastIndexOf('.');
        if (lastDot < 0)
            return null;

        SerializedProperty spec = serialized.FindProperty($"{path.Substring(0, lastDot)}.spec");
        return spec != null && spec.propertyType == SerializedPropertyType.Generic ? spec : null;
    }

    internal static void DescribeSpec(SerializedProperty spec, StatusEffectDef effect, List<string> details)
    {
        bool modifiersOverridden = spec.FindPropertyRelative("modifiersOverrideEnabled")?.boolValue ?? false;
        if (modifiersOverridden)
            DescribeSpecModifiers(spec.FindPropertyRelative("modifiers"), "Override", details);
        else if (effect != null)
            DescribeDefModifiers(effect, details);

        bool durationOverridden = spec.FindPropertyRelative("durationOverrideEnabled")?.boolValue ?? false;
        if (durationOverridden)
        {
            float duration = spec.FindPropertyRelative("durationOverride")?.floatValue ?? 0f;
            string durationText = duration <= 0f ? "Permanent" : $"Duration {Format(duration)}s";
            details.Add($"{durationText} (Application Override)");
        }
        else if (effect != null)
        {
            details.Add("Duration: Skill/Taunt Fallback, then Status Effect Def");
        }

        int stacks = spec.FindPropertyRelative("stacks")?.intValue ?? 1;
        if (stacks > 1)
            details.Add($"Stacks {stacks}");

        DescribeTick(spec, effect, details);
        DescribeTriggerRules(effect, details);
    }

    // Trigger rules are gameplay behavior owned by the Status Effect Def, just like modifiers and
    // ticking. Omitting them made a triggered status (for example Aires3_Thorns) look like it only
    // had a duration even though taking damage changes its stacks at runtime.
    static void DescribeTriggerRules(StatusEffectDef effect, List<string> details)
    {
        if (effect == null || effect.triggerRules == null)
            return;

        for (int i = 0; i < effect.triggerRules.Count; i++)
        {
            StatusEffectTriggerRule rule = effect.triggerRules[i];
            if (rule == null || rule.triggerType == EffectTriggerType.None)
                continue;

            string trigger = ObjectNames.NicifyVariableName(rule.triggerType.ToString());
            int requiredCount = Mathf.Max(1, rule.requiredCount);
            string condition = requiredCount > 1 ? $"{trigger} x{requiredCount}" : trigger;

            if (effect.stackMode == StackMode.AddStackAndRefresh)
            {
                int maxStacks = effect.ClampedMaxStacks;
                if (rule.maxStacks > 0)
                    maxStacks = Mathf.Min(maxStacks, rule.maxStacks);

                string max = maxStacks == int.MaxValue ? string.Empty : $", Max {maxStacks}";
                details.Add(
                    $"{condition}: +{Mathf.Max(1, rule.grantedStacks)} Stack{max}, Refresh Duration " +
                    $"({Source(false)})");
            }
            else if (effect.stackMode == StackMode.RefreshDuration ||
                     effect.stackMode == StackMode.StrongestOnly)
            {
                details.Add($"{condition}: Refresh Duration ({Source(false)})");
            }
        }
    }

    static void DescribeTick(SerializedProperty spec, StatusEffectDef effect, List<string> details)
    {
        bool damageOverridden = spec.FindPropertyRelative("tickDamageOverrideEnabled")?.boolValue ?? false;
        bool intervalOverridden = spec.FindPropertyRelative("tickIntervalOverrideEnabled")?.boolValue ?? false;

        float damage = damageOverridden
            ? spec.FindPropertyRelative("tickDamageOverride")?.floatValue ?? 0f
            : effect != null ? effect.tickDamage : 0f;
        float interval = intervalOverridden
            ? spec.FindPropertyRelative("tickIntervalOverride")?.floatValue ?? 0f
            : effect != null ? effect.tickInterval : 0f;

        // Matches StatusEffectDef.HasTick -- only report ticking when it actually runs.
        if (interval <= 0f || Mathf.Approximately(damage, 0f))
            return;

        string verb = damage < 0f ? "Heal" : "Damage";
        string source = Source(damageOverridden || intervalOverridden);
        details.Add($"{verb} {Format(Mathf.Abs(damage))} every {Format(interval)}s ({source})");
    }

    static void DescribeSpecModifiers(SerializedProperty modifiers, string source, List<string> details)
    {
        if (modifiers == null || !modifiers.isArray)
            return;

        for (int i = 0; i < modifiers.arraySize; i++)
        {
            SerializedProperty modifier = modifiers.GetArrayElementAtIndex(i);
            SerializedProperty stat = modifier.FindPropertyRelative("statType");
            SerializedProperty operation = modifier.FindPropertyRelative("operation");
            SerializedProperty value = modifier.FindPropertyRelative("value");
            if (stat == null || operation == null || value == null)
                continue;

            string statName = EnumLabel(stat);
            var op = (ModifierOp)operation.enumValueIndex;
            details.Add($"{FormatModifier(statName, op, value.floatValue)} ({source})");
        }
    }

    static void DescribeDefModifiers(StatusEffectDef effect, List<string> details)
    {
        if (effect.modifiers == null)
            return;

        for (int i = 0; i < effect.modifiers.Count; i++)
        {
            StatusEffectModifier modifier = effect.modifiers[i];
            if (modifier == null)
                continue;

            details.Add($"{FormatModifier(modifier.statType.ToString(), modifier.operation, modifier.value)} " +
                        $"({Source(false)})");
        }
    }

    static string FormatModifier(string statName, ModifierOp operation, float value)
    {
        switch (operation)
        {
            case ModifierOp.AddPercent:
                return $"{statName} {Signed(value)}%";
            case ModifierOp.Multiply:
                return $"{statName} x{Format(value)}";
            default:
                return $"{statName} {Signed(value)}";
        }
    }

    static string Source(bool overridden) => overridden ? "Override" : "From Status Effect Def";

    static string Signed(float value) =>
        value >= 0f ? $"+{Format(value)}" : Format(value);

    internal static string Format(float value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    static string EnumLabel(SerializedProperty property)
    {
        string[] names = property.enumDisplayNames;
        int index = property.enumValueIndex;
        return index >= 0 && index < names.Length ? names[index] : property.intValue.ToString();
    }

    static int ResolveStepIndex(CompositeSkillPayloadDef composite, UnityEngine.Object asset, string path)
    {
        if (composite == null)
            return -1;

        if (ReferenceEquals(asset, composite))
            return ParseStepIndex(path);

        // A PayloadStep's payload is its own sub-asset, so the path says nothing about the step.
        // Find the step that wraps it instead.
        if (asset is not SkillPayloadDef payload)
            return -1;

        IReadOnlyList<SkillEffectStep> steps = composite.Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] is PayloadStep payloadStep && ReferenceEquals(payloadStep.Payload, payload))
                return i;
        }

        return -1;
    }

    static int ParseStepIndex(string path)
    {
        if (!path.StartsWith(StepsArrayPrefix, StringComparison.Ordinal))
            return -1;

        int close = path.IndexOf(']', StepsArrayPrefix.Length);
        if (close < 0)
            return -1;

        string number = path.Substring(StepsArrayPrefix.Length, close - StepsArrayPrefix.Length);
        return int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) ? index : -1;
    }

    static SkillEffectStep ResolveStep(CompositeSkillPayloadDef composite, int stepIndex)
    {
        if (composite == null || stepIndex < 0)
            return null;

        IReadOnlyList<SkillEffectStep> steps = composite.Steps;
        return stepIndex < steps.Count ? steps[stepIndex] : null;
    }

    static string BuildSourceLabel(
        SkillDefinitionBase owner,
        UnityEngine.Object asset,
        SkillEffectStep step,
        int stepIndex,
        string path)
    {
        if (step != null)
        {
            if (step is PayloadStep payloadStep && payloadStep.Payload != null)
            {
                string payloadName = SkillPayloadAssetUtility.GetPayloadDisplayName(payloadStep.Payload.GetType());
                return $"Configured In: {payloadName} Payload (Step {stepIndex})";
            }

            string stepName = ActiveSkillTreeEditorWindow.GetStepDisplayName(step.GetType());
            return $"Configured In: {stepName} (Step {stepIndex})";
        }

        string assetName = asset != null ? asset.name : owner != null ? owner.name : "<unknown>";
        return $"Configured In: {assetName} — {Prettify(path)}";
    }

    static string BuildBehaviorLabel(UnityEngine.Object asset, SkillEffectStep step, string path)
    {
        if (step is PayloadStep payloadStep && payloadStep.Payload != null)
            return $"{SkillPayloadAssetUtility.GetPayloadDisplayName(payloadStep.Payload.GetType())} Payload";

        if (step != null)
            return $"{ActiveSkillTreeEditorWindow.GetStepDisplayName(step.GetType())} Step";

        string assetName = asset != null ? asset.name : "<unknown>";
        return $"{assetName}: {Prettify(path)}";
    }

    static string Prettify(string path) => path.Replace(".Array.data[", "[");

    /// <summary>
    /// Target comes from the route this upgrade id sits in, so the Gameplay Effects summary and the
    /// wizard's destination list read the same source of truth.
    /// </summary>
    static string ResolveTargetLabel(
        IReadOnlyList<SkillStatusRoute> routes,
        UnityEngine.Object asset,
        string path)
    {
        for (int i = 0; routes != null && i < routes.Count; i++)
        {
            SkillStatusRoute route = routes[i];
            if (!ReferenceEquals(route.Container, asset))
                continue;

            if (path.StartsWith($"{route.ListPropertyPath}.Array.data[", StringComparison.Ordinal))
                return SkillStatusRoute.DescribeTarget(route.Target);
        }

        return null;
    }
}
