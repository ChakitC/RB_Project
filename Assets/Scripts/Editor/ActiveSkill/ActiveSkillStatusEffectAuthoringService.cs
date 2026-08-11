#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Application หนึ่งรายการที่มีอยู่แล้วในสกิล — พอสำหรับทั้งวาดการ์ด, เปิด Edit และลบ.</summary>
public sealed class StatusEffectApplicationHandle
{
    public SkillStatusRoute Route;
    public int ElementIndex = -1;
    public string UpgradeId;
    public StatusEffectDef Effect;

    public string Summary =>
        $"Apply {(Effect != null ? Effect.name : "<missing Status Effect Def>")} to " +
        $"{SkillStatusRoute.DescribeTarget(Route != null ? Route.Target : SkillStatusTarget.Self)}";

    public string SourceLabel => Route != null ? Route.DisplayLabel : "<unresolved destination>";

    /// <summary>Modifier/duration/stack/tick lines, same format UpgradeIdUsageScanner reports.</summary>
    public List<string> Details = new();
}

/// <summary>ค่าที่ใช้สร้าง <see cref="StatusEffectDef"/> ตัวใหม่. รองรับเฉพาะ numeric status.</summary>
public sealed class NewStatusEffectRequest
{
    public string AssetName = "NewStatusEffect";
    public string EffectId = string.Empty;
    public StatusEffectScope Scope = StatusEffectScope.Global;
    public StatusEffectCategory Category = StatusEffectCategory.Buff;
    public Sprite Icon;
    public StatusEffectVfxProfile VfxProfile;
    public float Duration = 5f;
    public int MaxStacks = 1;
    public StackMode StackMode = StackMode.RefreshDuration;
    public string FolderOverride;
}

public enum ExistingStatusScopeResolution
{
    None,
    PromoteToGlobal,
    MakeUniqueToSkill,
    DuplicateAsUnique,
}

/// <summary>ทุกอย่างที่ wizard ต้องส่งให้ service เพื่อเขียนหนึ่ง operation.</summary>
public sealed class StatusEffectAuthoringRequest
{
    public SkillGemDefinition Skill;
    public SkillUpgradeTreeDefinition Tree;
    public SkillUpgradeNodeData Node;

    /// <summary>Upgrade id ที่ใช้เป็น gate. ว่างไม่ได้ — ทุก application ที่ node สั่งต้องมี gate ของตัวเอง.</summary>
    public string UpgradeId = string.Empty;

    public bool CreateNewEffect;
    public StatusEffectDef ExistingEffect;
    public ExistingStatusScopeResolution ExistingScopeResolution;
    public NewStatusEffectRequest NewEffect = new();

    public SkillStatusRoute Route;

    public int Stacks = 1;
    public bool OverrideModifiers;
    public List<StatusEffectModifier> Modifiers = new();
    public bool OverrideDuration;
    public float Duration;
    public bool OverrideTickDamage;
    public bool OverrideTickInterval;
    public float TickDamage;
    public float TickInterval;

    /// <summary>null = Create mode. มีค่า = Edit mode ของ application เดิม.</summary>
    public StatusEffectApplicationHandle Existing;

    public bool IsEdit => Existing != null;

    public StatusEffectDef ResolveEffect() => CreateNewEffect ? null : ExistingEffect;
}

/// <summary>
/// จุดเดียวที่เขียน asset ให้ฟีเจอร์นี้. Wizard วาด UI, service เขียนข้อมูล — แยกกันเพื่อให้ทดสอบ
/// operation ได้โดยไม่ต้องเปิดหน้าต่าง และเพื่อให้ทุกเส้นทางการเขียนผ่าน validate ชุดเดียวกัน.
///
/// การเขียน payload ทำผ่าน SerializedObject/SerializedProperty ทั้งหมด เพราะ list ที่ต้องแก้เป็น
/// <c>private</c> บน payload/step (ไม่มี runtime setter และไม่ควรเพิ่มให้ editor) และเพราะ embedded
/// payload เป็น sub-asset คนละ UnityEngine.Object กับตัวสกิล.
///
/// ทุก operation ห่อด้วย undo group เดียว — undo ครั้งเดียวถอยทั้ง operation รวมถึง asset ที่เพิ่งสร้าง.
/// </summary>
public static class ActiveSkillStatusEffectAuthoringService
{
    const string RequiredUpgradeIdField = "requiredUpgradeId";
    const string SpecField = "spec";

    #region Read

    /// <summary>Application ทุกตัวในสกิลที่ถูก gate ด้วย id ที่ node นี้ปลดล็อก.</summary>
    public static List<StatusEffectApplicationHandle> Collect(
        SkillGemDefinition skill,
        IReadOnlyList<string> upgradeIds)
    {
        var result = new List<StatusEffectApplicationHandle>();
        if (skill == null || upgradeIds == null || upgradeIds.Count == 0)
            return result;

        var wanted = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < upgradeIds.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(upgradeIds[i]))
                wanted.Add(upgradeIds[i].Trim());
        }

        if (wanted.Count == 0)
            return result;

        List<SkillStatusRoute> routes = SkillStatusRouteResolver.Resolve(skill);
        for (int i = 0; i < routes.Count; i++)
        {
            SkillStatusRoute route = routes[i];
            var serialized = route.CreateSerializedObject();
            SerializedProperty list = route.FindList(serialized);
            if (list == null || !list.isArray)
                continue;

            for (int elementIndex = 0; elementIndex < list.arraySize; elementIndex++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(elementIndex);
                string id = element.FindPropertyRelative(RequiredUpgradeIdField)?.stringValue?.Trim();
                if (string.IsNullOrEmpty(id) || !wanted.Contains(id))
                    continue;

                SerializedProperty spec = element.FindPropertyRelative(SpecField);
                var effect = spec?.FindPropertyRelative("effect")?.objectReferenceValue as StatusEffectDef;

                var handle = new StatusEffectApplicationHandle
                {
                    Route = route,
                    ElementIndex = elementIndex,
                    UpgradeId = id,
                    Effect = effect,
                };

                if (spec != null)
                    UpgradeIdUsageScanner.DescribeSpec(spec, effect, handle.Details);

                result.Add(handle);
            }
        }

        return result;
    }

    /// <summary>อ่านค่าปัจจุบันของ application เข้ามาเป็น request สำหรับ Edit mode.</summary>
    public static StatusEffectAuthoringRequest BuildEditRequest(
        SkillGemDefinition skill,
        SkillUpgradeTreeDefinition tree,
        SkillUpgradeNodeData node,
        StatusEffectApplicationHandle handle)
    {
        var request = new StatusEffectAuthoringRequest
        {
            Skill = skill,
            Tree = tree,
            Node = node,
            Existing = handle,
            Route = handle?.Route,
            UpgradeId = handle?.UpgradeId ?? string.Empty,
            CreateNewEffect = false,
            ExistingEffect = handle?.Effect,
        };

        if (handle?.Route == null || handle.ElementIndex < 0)
            return request;

        var serialized = handle.Route.CreateSerializedObject();
        SerializedProperty list = handle.Route.FindList(serialized);
        if (list == null || handle.ElementIndex >= list.arraySize)
            return request;

        SerializedProperty spec = list.GetArrayElementAtIndex(handle.ElementIndex).FindPropertyRelative(SpecField);
        if (spec == null)
            return request;

        request.Stacks = Mathf.Max(1, spec.FindPropertyRelative("stacks")?.intValue ?? 1);
        request.OverrideModifiers = spec.FindPropertyRelative("modifiersOverrideEnabled")?.boolValue ?? false;
        request.Modifiers = ReadModifiers(spec, request.OverrideModifiers, handle.Effect);
        request.OverrideDuration = spec.FindPropertyRelative("durationOverrideEnabled")?.boolValue ?? false;
        request.Duration = request.OverrideDuration
            ? spec.FindPropertyRelative("durationOverride")?.floatValue ?? 0f
            : handle.Effect != null ? handle.Effect.duration : 0f;

        bool tickDamageOverridden = spec.FindPropertyRelative("tickDamageOverrideEnabled")?.boolValue ?? false;
        bool tickIntervalOverridden = spec.FindPropertyRelative("tickIntervalOverrideEnabled")?.boolValue ?? false;
        request.OverrideTickDamage = tickDamageOverridden;
        request.OverrideTickInterval = tickIntervalOverridden;
        request.TickDamage = tickDamageOverridden
            ? spec.FindPropertyRelative("tickDamageOverride")?.floatValue ?? 0f
            : handle.Effect != null ? handle.Effect.tickDamage : 0f;
        request.TickInterval = tickIntervalOverridden
            ? spec.FindPropertyRelative("tickIntervalOverride")?.floatValue ?? 0f
            : handle.Effect != null ? handle.Effect.tickInterval : 0f;

        return request;
    }

    /// <summary>
    /// Refreshes only channels that still inherit from the selected Def. Explicit application
    /// overrides are preserved when the designer changes status identity in Edit mode.
    /// </summary>
    public static void RefreshInheritedValues(
        StatusEffectAuthoringRequest request,
        StatusEffectDef effect)
    {
        if (request == null)
            return;

        if (!request.OverrideModifiers)
            request.Modifiers = CopyModifiers(effect);
        if (!request.OverrideDuration)
            request.Duration = effect != null ? effect.duration : 0f;
        if (!request.OverrideTickDamage)
            request.TickDamage = effect != null ? effect.tickDamage : 0f;
        if (!request.OverrideTickInterval)
            request.TickInterval = effect != null ? effect.tickInterval : 0f;
    }

    static List<StatusEffectModifier> CopyModifiers(StatusEffectDef effect)
    {
        var result = new List<StatusEffectModifier>();
        if (effect?.modifiers == null)
            return result;

        for (int i = 0; i < effect.modifiers.Count; i++)
        {
            StatusEffectModifier source = effect.modifiers[i];
            if (source == null)
                continue;

            result.Add(new StatusEffectModifier
            {
                statType = source.statType,
                operation = source.operation,
                value = source.value,
            });
        }

        return result;
    }

    static List<StatusEffectModifier> ReadModifiers(
        SerializedProperty spec,
        bool overridden,
        StatusEffectDef effect)
    {
        var result = new List<StatusEffectModifier>();
        if (overridden)
        {
            SerializedProperty modifiers = spec.FindPropertyRelative("modifiers");
            for (int i = 0; modifiers != null && i < modifiers.arraySize; i++)
            {
                SerializedProperty modifier = modifiers.GetArrayElementAtIndex(i);
                result.Add(new StatusEffectModifier
                {
                    statType = (StatType)(modifier.FindPropertyRelative("statType")?.intValue ?? 0),
                    operation = (ModifierOp)(modifier.FindPropertyRelative("operation")?.intValue ?? 0),
                    value = modifier.FindPropertyRelative("value")?.floatValue ?? 0f,
                });
            }

            return result;
        }

        return CopyModifiers(effect);
    }

    /// <summary>id นี้ยังมี site ไหนในสกิลฟังอยู่ไหม — ใช้ตอบว่าควรเสนอถอด id ออกจาก node หรือไม่.</summary>
    public static bool IsUpgradeIdStillUsed(SkillGemDefinition skill, string upgradeId)
    {
        if (skill == null)
            return false;

        return IsUpgradeIdStillUsed(new List<SkillDefinitionBase> { skill }, upgradeId);
    }

    /// <summary>
    /// Shared trees grant one snapshot to every owner, so an id is removable only when no owning
    /// skill or passive still consumes it.
    /// </summary>
    public static bool IsUpgradeIdStillUsed(
        IReadOnlyList<SkillDefinitionBase> owners,
        string upgradeId)
    {
        if (owners == null || string.IsNullOrWhiteSpace(upgradeId))
            return false;

        string trimmed = upgradeId.Trim();
        for (int ownerIndex = 0; ownerIndex < owners.Count; ownerIndex++)
        {
            SkillDefinitionBase owner = owners[ownerIndex];
            if (owner == null)
                continue;

            var ids = new List<string>();
            owner.CollectUpgradeIds(ids);
            for (int idIndex = 0; idIndex < ids.Count; idIndex++)
            {
                if (string.Equals(ids[idIndex]?.Trim(), trimmed, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    #endregion

    #region Validate

    public static List<SkillUpgradeValidationIssue> Validate(StatusEffectAuthoringRequest request)
    {
        var issues = new List<SkillUpgradeValidationIssue>();
        if (request == null)
        {
            issues.Add(Error("No authoring request."));
            return issues;
        }

        if (request.Skill == null)
        {
            issues.Add(Error("Choose the owning skill before authoring a status effect."));
            return issues;
        }

        if (request.Node == null || request.Tree == null)
        {
            issues.Add(Error("Select a node in the Active Skill Tree first."));
            return issues;
        }

        string upgradeId = request.UpgradeId?.Trim();
        if (string.IsNullOrEmpty(upgradeId))
            issues.Add(Error("Upgrade gate id is required — the node has to unlock this application."));

        ValidateRoute(request, issues);
        StatusEffectDef effect = ValidateIdentity(request, issues);
        ValidateApplication(request, effect, upgradeId, issues);

        return issues;
    }

    static void ValidateRoute(StatusEffectAuthoringRequest request, List<SkillUpgradeValidationIssue> issues)
    {
        SkillStatusRouteResolutionResult resolution = SkillStatusRouteResolver.ResolveDetailed(request.Skill);

        // A route whose target metadata is wrong is a blocking authoring error: guessing "Self" here
        // would write the application to a destination that applies the status to someone else.
        for (int i = 0; i < resolution.Issues.Count; i++)
            issues.Add(Error(resolution.Issues[i]));

        List<SkillStatusRoute> routes = resolution.Routes;
        if (routes.Count == 0)
        {
            issues.Add(Error(
                $"'{request.Skill.name}' has no step or payload that accepts a conditional status " +
                "application. Add an Apply Status / Heal Area / Taunt step to the skill first."));
            return;
        }

        if (request.Route == null)
        {
            issues.Add(Error("Choose a destination for this application."));
            return;
        }

        if (SkillStatusRouteResolver.FindByKey(routes, request.Route.RouteKey) == null)
        {
            issues.Add(Error(
                $"Destination '{request.Route.DisplayLabel}' no longer exists in '{request.Skill.name}' " +
                "(the step was removed or retargeted). Pick another destination."));
        }
    }

    static StatusEffectDef ValidateIdentity(
        StatusEffectAuthoringRequest request,
        List<SkillUpgradeValidationIssue> issues)
    {
        if (!request.CreateNewEffect)
        {
            StatusEffectDef effect = request.ExistingEffect;
            if (effect == null)
            {
                issues.Add(Error("Choose an existing Status Effect Def, or switch to Create New."));
                return null;
            }

            switch (request.ExistingScopeResolution)
            {
                case ExistingStatusScopeResolution.PromoteToGlobal:
                    return effect;

                case ExistingStatusScopeResolution.MakeUniqueToSkill:
                    if (!CanMakeUnique(effect, request.Skill, out string uniqueError))
                        issues.Add(Error(uniqueError));
                    return effect;

                case ExistingStatusScopeResolution.DuplicateAsUnique:
                    if (string.IsNullOrEmpty(StatusEffectScopeUtility.GetAssetGuid(request.Skill)))
                    {
                        issues.Add(Error(
                            "Duplicating as Unique needs a saved owning skill asset. Save the skill first."));
                    }

                    if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(effect)))
                        issues.Add(Error("Only a saved Status Effect Def asset can be duplicated."));
                    return effect;
            }

            if (!StatusEffectScopeUtility.IsUsableBy(effect, request.Skill, out string reason))
                issues.Add(Error(reason));
            else if (!string.IsNullOrEmpty(reason))
                issues.Add(Warning(reason));

            return effect;
        }

        NewStatusEffectRequest newEffect = request.NewEffect;
        if (newEffect == null)
        {
            issues.Add(Error("New status effect settings are missing."));
            return null;
        }

        if (string.IsNullOrWhiteSpace(newEffect.AssetName))
            issues.Add(Error("New status effect needs an asset name."));

        string effectId = ResolveNewEffectId(newEffect);
        if (string.IsNullOrWhiteSpace(effectId))
        {
            issues.Add(Error("New status effect needs an effect id."));
        }
        else
        {
            List<StatusEffectDef> clashes = StatusEffectScopeUtility.FindEffectsWithId(effectId, null);
            if (clashes.Count > 0)
            {
                issues.Add(Error(
                    $"Effect id '{effectId}' is already used by '{clashes[0].name}'. Effect ids must be unique."));
            }
        }

        if (newEffect.Scope == StatusEffectScope.Unique &&
            string.IsNullOrEmpty(StatusEffectScopeUtility.GetAssetGuid(request.Skill)))
        {
            issues.Add(Error(
                "A unique status needs a saved owning skill asset to claim it. Save the skill first, " +
                "or create the status as Global."));
        }

        string path = ResolveNewEffectPath(request);
        if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            issues.Add(Error($"'{path}' already exists. Pick another name."));

        if (newEffect.MaxStacks < 0)
            issues.Add(Error("Max stacks cannot be negative (0 means unlimited)."));

        return null;
    }

    static void ValidateApplication(
        StatusEffectAuthoringRequest request,
        StatusEffectDef effect,
        string upgradeId,
        List<SkillUpgradeValidationIssue> issues)
    {
        if (request.Stacks < 1)
            issues.Add(Error("Stacks must be at least 1."));

        if (request.OverrideDuration && request.Duration < 0f)
            issues.Add(Error("Duration override cannot be negative (0 means permanent)."));

        if (request.OverrideTickInterval && request.TickInterval < 0f)
            issues.Add(Error("Tick interval cannot be negative (0 disables ticking)."));

        if (request.OverrideModifiers && request.Modifiers != null)
        {
            for (int i = 0; i < request.Modifiers.Count; i++)
            {
                StatusEffectModifier modifier = request.Modifiers[i];
                if (modifier == null)
                    continue;

                if (modifier.operation == ModifierOp.Multiply && modifier.value <= 0f)
                {
                    issues.Add(Warning(
                        $"Modifier #{i + 1} uses Multiply with value <= 0, which zeroes or inverts " +
                        $"{modifier.statType}."));
                }
            }
        }

        if (effect == null || request.Route == null || string.IsNullOrEmpty(upgradeId))
            return;

        // A duplicated Def becomes a new status identity during commit, so an application that
        // currently uses the source Def is not a duplicate of the pending copy.
        if (request.ExistingScopeResolution == ExistingStatusScopeResolution.DuplicateAsUnique)
            return;

        StatusEffectApplicationHandle duplicate = FindDuplicate(request, effect, upgradeId);
        if (duplicate != null)
        {
            issues.Add(Error(
                $"'{effect.name}' is already applied by upgrade '{upgradeId}' at " +
                $"{duplicate.SourceLabel}. Edit that entry instead of adding a second one."));
        }
    }

    /// <summary>ซ้ำ = Status + Upgrade Id + Target + Destination ตรงกันทั้งชุด (ไม่นับตัวที่กำลังแก้อยู่).</summary>
    static StatusEffectApplicationHandle FindDuplicate(
        StatusEffectAuthoringRequest request,
        StatusEffectDef effect,
        string upgradeId)
    {
        List<StatusEffectApplicationHandle> existing = Collect(request.Skill, new List<string> { upgradeId });
        for (int i = 0; i < existing.Count; i++)
        {
            StatusEffectApplicationHandle candidate = existing[i];
            if (!ReferenceEquals(candidate.Effect, effect))
                continue;

            if (!candidate.Route.Matches(request.Route))
                continue;

            if (request.Existing?.Route != null &&
                request.Existing.Route.Matches(candidate.Route) &&
                request.Existing.ElementIndex == candidate.ElementIndex)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    public static bool HasErrors(IReadOnlyList<SkillUpgradeValidationIssue> issues)
    {
        for (int i = 0; issues != null && i < issues.Count; i++)
        {
            if (issues[i].Severity == SkillUpgradeValidationSeverity.Error)
                return true;
        }

        return false;
    }

    #endregion

    #region Preview

    public static string BuildPreview(StatusEffectAuthoringRequest request)
    {
        if (request == null)
            return string.Empty;

        var lines = new List<string>();
        StatusEffectDef effect = request.ResolveEffect();

        if (request.CreateNewEffect)
        {
            string path = ResolveNewEffectPath(request);
            lines.Add($"Create asset: {path}");
            lines.Add($"  Scope: {request.NewEffect.Scope}" +
                      (request.NewEffect.Scope == StatusEffectScope.Unique && request.Skill != null
                          ? $" (owner {request.Skill.name})"
                          : string.Empty));
            lines.Add($"  Effect id: {ResolveNewEffectId(request.NewEffect)}");
            lines.Add($"  Category: {request.NewEffect.Category}, Duration {request.NewEffect.Duration:0.###}s, " +
                      $"{request.NewEffect.StackMode}, Max stacks {request.NewEffect.MaxStacks}");
        }
        else
        {
            lines.Add($"Use status: {(effect != null ? effect.name : "<none>")} " +
                      $"({StatusEffectScopeUtility.DescribeScope(effect)})");
            switch (request.ExistingScopeResolution)
            {
                case ExistingStatusScopeResolution.PromoteToGlobal:
                    lines.Add("  Scope change on commit: Promote to Global");
                    break;
                case ExistingStatusScopeResolution.MakeUniqueToSkill:
                    lines.Add($"  Scope change on commit: Unique to {request.Skill?.name ?? "<none>"}");
                    break;
                case ExistingStatusScopeResolution.DuplicateAsUnique:
                    lines.Add($"  Identity change on commit: Duplicate as Unique to {request.Skill?.name ?? "<none>"}");
                    break;
            }
        }

        string upgradeId = request.UpgradeId?.Trim();
        bool grantsNewId = request.Node != null &&
                           !string.IsNullOrEmpty(upgradeId) &&
                           !NodeGrants(request.Node, upgradeId);
        lines.Add(grantsNewId
            ? $"Add granted upgrade id '{upgradeId}' to node '{request.Node.RuntimeNodeId}'"
            : $"Reuse granted upgrade id '{upgradeId}' on node '{request.Node?.RuntimeNodeId}'");

        string destination = request.Route != null ? request.Route.DisplayLabel : "<no destination>";
        if (request.IsEdit && request.Existing.Route != null && !request.Existing.Route.Matches(request.Route))
            lines.Add($"Move application: {request.Existing.Route.DisplayLabel}  ->  {destination}");
        else if (request.IsEdit)
            lines.Add($"Update application at {destination}");
        else
            lines.Add($"Add conditional application at {destination}");

        lines.Add($"  Stacks: {request.Stacks}");
        lines.Add(request.OverrideModifiers
            ? $"  Modifiers: Override ({(request.Modifiers?.Count ?? 0)} entr{((request.Modifiers?.Count ?? 0) == 1 ? "y" : "ies")})"
            : "  Modifiers: From Status Effect Def");
        if (request.OverrideModifiers && request.Modifiers != null)
        {
            for (int i = 0; i < request.Modifiers.Count; i++)
            {
                StatusEffectModifier modifier = request.Modifiers[i];
                if (modifier != null)
                    lines.Add($"    - {modifier.statType} / {modifier.operation} / {modifier.value:0.###}");
            }
        }

        lines.Add(request.OverrideDuration
            ? $"  Duration: Override {(request.Duration <= 0f ? "Permanent" : $"{request.Duration:0.###}s")}" 
            : "  Duration: Skill/Taunt fallback, then Status Effect Def");
        lines.Add(request.OverrideTickDamage
            ? $"  Tick Damage: Override {request.TickDamage:0.###}"
            : "  Tick Damage: From Status Effect Def");
        lines.Add(request.OverrideTickInterval
            ? $"  Tick Interval: Override {request.TickInterval:0.###}s"
            : "  Tick Interval: From Status Effect Def");

        return string.Join("\n", lines);
    }

    #endregion

    #region Write

    /// <summary>
    /// เขียนทั้ง operation ในกลุ่ม undo เดียว. คืน false เมื่อ validate ไม่ผ่าน โดยยังไม่แตะ asset ใดๆ —
    /// เงื่อนไข "Cancel แล้วไม่มีข้อมูลค้าง" มาจากตรงนี้: ไม่มีการเขียนใดๆ เกิดก่อนบรรทัด commit.
    /// </summary>
    public static bool Apply(
        StatusEffectAuthoringRequest request,
        out List<SkillUpgradeValidationIssue> issues)
    {
        issues = Validate(request);
        if (HasErrors(issues))
            return false;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(request.IsEdit ? "Edit Skill Status Effect" : "Add Skill Status Effect");
        int group = Undo.GetCurrentGroup();

        try
        {
            StatusEffectDef effect = request.CreateNewEffect
                ? CreateStatusEffectAsset(request)
                : ResolveExistingEffectForApply(request);

            if (effect == null)
            {
                issues.Add(Error("Failed to resolve the status effect asset."));
                Undo.RevertAllDownToGroup(group);
                return false;
            }

            // ย้าย destination = ลบตัวเดิมก่อนเขียนตัวใหม่ ไม่ใช่การเขียนซ้อนสองที่
            bool moved = request.IsEdit &&
                         request.Existing.Route != null &&
                         !request.Existing.Route.Matches(request.Route);
            if (moved)
                DeleteApplication(request.Existing);

            int elementIndex = request.IsEdit && !moved
                ? request.Existing.ElementIndex
                : -1;

            WriteApplication(request, effect, elementIndex);
            GrantUpgradeId(request);
            ApplyStagedScopeChange(request, effect);

            Undo.CollapseUndoOperations(group);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Undo.RevertAllDownToGroup(group);
            issues.Add(Error($"Authoring failed and was rolled back: {exception.Message}"));
            return false;
        }
    }

    static StatusEffectDef ResolveExistingEffectForApply(StatusEffectAuthoringRequest request)
    {
        if (request.ExistingScopeResolution == ExistingStatusScopeResolution.DuplicateAsUnique)
            return DuplicateAsUniqueAsset(request.ExistingEffect, request.Skill);

        return request.ExistingEffect;
    }

    static void ApplyStagedScopeChange(
        StatusEffectAuthoringRequest request,
        StatusEffectDef resolvedEffect)
    {
        if (request.CreateNewEffect || resolvedEffect == null)
            return;

        switch (request.ExistingScopeResolution)
        {
            case ExistingStatusScopeResolution.PromoteToGlobal:
                StatusEffectScopeUtility.SetScope(resolvedEffect, StatusEffectScope.Global, null);
                AssetDatabase.SaveAssetIfDirty(resolvedEffect);
                break;

            case ExistingStatusScopeResolution.MakeUniqueToSkill:
                StatusEffectScopeUtility.SetScope(resolvedEffect, StatusEffectScope.Unique, request.Skill);
                AssetDatabase.SaveAssetIfDirty(resolvedEffect);
                break;
        }
    }

    /// <summary>ลบ application อย่างเดียว — ไม่แตะ StatusEffectDef asset ทิ้งไว้ให้ทีมตัดสินใจเอง.</summary>
    public static void Remove(StatusEffectApplicationHandle handle, SkillGemDefinition skill)
    {
        if (handle?.Route == null || handle.ElementIndex < 0)
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Remove Skill Status Effect");
        int group = Undo.GetCurrentGroup();

        DeleteApplication(handle);
        MarkSkillDirty(skill, handle.Route.Container);

        Undo.CollapseUndoOperations(group);
    }

    /// <summary>ถอด granted id ออกจาก node (เรียกหลัง Remove เมื่อผู้ใช้ยืนยันว่าไม่มีใครใช้แล้ว).</summary>
    public static void RemoveGrantedUpgradeId(
        SkillUpgradeTreeDefinition tree,
        SkillUpgradeNodeData node,
        string upgradeId)
    {
        if (tree == null || node?.grantedUpgradeIds == null || string.IsNullOrWhiteSpace(upgradeId))
            return;

        string trimmed = upgradeId.Trim();
        Undo.RecordObject(tree, "Remove Granted Upgrade Id");
        for (int i = node.grantedUpgradeIds.Count - 1; i >= 0; i--)
        {
            if (string.Equals(node.grantedUpgradeIds[i]?.Trim(), trimmed, StringComparison.Ordinal))
                node.grantedUpgradeIds.RemoveAt(i);
        }

        EditorUtility.SetDirty(tree);
    }

    static void DeleteApplication(StatusEffectApplicationHandle handle)
    {
        var serialized = handle.Route.CreateSerializedObject();
        serialized.Update();
        SerializedProperty list = handle.Route.FindList(serialized);
        if (list == null || !list.isArray || handle.ElementIndex >= list.arraySize)
            return;

        list.DeleteArrayElementAtIndex(handle.ElementIndex);
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(handle.Route.Container);
    }

    static void WriteApplication(
        StatusEffectAuthoringRequest request,
        StatusEffectDef effect,
        int elementIndex)
    {
        var serialized = request.Route.CreateSerializedObject();
        serialized.Update();
        SerializedProperty list = request.Route.FindList(serialized);
        if (list == null || !list.isArray)
            throw new InvalidOperationException(
                $"Destination '{request.Route.DisplayLabel}' has no '{request.Route.ListPropertyPath}' list.");

        int index = elementIndex;
        if (index < 0 || index >= list.arraySize)
        {
            index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
        }

        SerializedProperty element = list.GetArrayElementAtIndex(index);
        SerializedProperty idProperty = element.FindPropertyRelative(RequiredUpgradeIdField);
        if (idProperty != null)
            idProperty.stringValue = request.UpgradeId?.Trim() ?? string.Empty;

        SerializedProperty spec = element.FindPropertyRelative(SpecField);
        if (spec == null)
            throw new InvalidOperationException(
                $"Destination '{request.Route.DisplayLabel}' element has no '{SpecField}' block.");

        WriteSpec(spec, request, effect);
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(request.Route.Container);
        MarkSkillDirty(request.Skill, request.Route.Container);
    }

    /// <summary>
    /// เขียน channel ตามกติกาเดียวกับ <see cref="StatusApplicationSpecDrawer"/>: enable flag ถูกเปิด
    /// เฉพาะ channel ที่ผู้ใช้เลือก override — channel ที่ไม่ได้เปิดถูกล้างค่าให้เป็น 0 เพื่อไม่ให้เหลือ
    /// ค่าค้างที่ทำให้คนอ่าน YAML เข้าใจผิดว่ามี override.
    /// </summary>
    static void WriteSpec(
        SerializedProperty spec,
        StatusEffectAuthoringRequest request,
        StatusEffectDef effect)
    {
        SerializedProperty effectProperty = spec.FindPropertyRelative("effect");
        if (effectProperty != null)
            effectProperty.objectReferenceValue = effect;

        SerializedProperty stacks = spec.FindPropertyRelative("stacks");
        if (stacks != null)
            stacks.intValue = Mathf.Max(1, request.Stacks);

        SerializedProperty modifiers = spec.FindPropertyRelative("modifiers");
        SerializedProperty modifiersEnabled = spec.FindPropertyRelative("modifiersOverrideEnabled");
        if (modifiers != null && modifiersEnabled != null)
        {
            modifiersEnabled.boolValue = request.OverrideModifiers;
            if (!request.OverrideModifiers)
            {
                modifiers.arraySize = 0;
            }
            else
            {
                int count = request.Modifiers?.Count ?? 0;
                modifiers.arraySize = count;
                for (int i = 0; i < count; i++)
                {
                    StatusEffectModifier modifier = request.Modifiers[i];
                    SerializedProperty target = modifiers.GetArrayElementAtIndex(i);
                    target.FindPropertyRelative("statType").intValue =
                        (int)(modifier?.statType ?? StatType.Damage);
                    target.FindPropertyRelative("operation").intValue =
                        (int)(modifier?.operation ?? ModifierOp.Flat);
                    target.FindPropertyRelative("value").floatValue = modifier?.value ?? 0f;
                }
            }
        }

        SetScalar(spec, "durationOverride", "durationOverrideEnabled",
            request.OverrideDuration, Mathf.Max(0f, request.Duration));
        SetScalar(spec, "tickDamageOverride", "tickDamageOverrideEnabled",
            request.OverrideTickDamage, request.TickDamage);
        SetScalar(spec, "tickIntervalOverride", "tickIntervalOverrideEnabled",
            request.OverrideTickInterval, Mathf.Max(0f, request.TickInterval));
    }

    static void SetScalar(
        SerializedProperty spec,
        string valueField,
        string enabledField,
        bool enabled,
        float value)
    {
        SerializedProperty enabledProperty = spec.FindPropertyRelative(enabledField);
        SerializedProperty valueProperty = spec.FindPropertyRelative(valueField);
        if (enabledProperty != null)
            enabledProperty.boolValue = enabled;
        if (valueProperty != null)
            valueProperty.floatValue = enabled ? value : 0f;
    }

    static void GrantUpgradeId(StatusEffectAuthoringRequest request)
    {
        string upgradeId = request.UpgradeId?.Trim();
        if (request.Tree == null || request.Node == null || string.IsNullOrEmpty(upgradeId))
            return;

        request.Node.grantedUpgradeIds ??= new List<string>();
        if (NodeGrants(request.Node, upgradeId))
            return;

        Undo.RecordObject(request.Tree, "Grant Upgrade Id");
        request.Node.grantedUpgradeIds.Add(upgradeId);
        EditorUtility.SetDirty(request.Tree);
    }

    static bool NodeGrants(SkillUpgradeNodeData node, string upgradeId)
    {
        if (node?.grantedUpgradeIds == null)
            return false;

        for (int i = 0; i < node.grantedUpgradeIds.Count; i++)
        {
            if (string.Equals(node.grantedUpgradeIds[i]?.Trim(), upgradeId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    static StatusEffectDef CreateStatusEffectAsset(StatusEffectAuthoringRequest request)
    {
        NewStatusEffectRequest settings = request.NewEffect;
        string path = ResolveNewEffectPath(request);
        StatusEffectScopeUtility.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));

        var definition = ScriptableObject.CreateInstance<StatusEffectDef>();
        definition.name = settings.AssetName.Trim();
        definition.effectId = ResolveNewEffectId(settings);
        definition.icon = settings.Icon;
        definition.category = settings.Category;
        definition.vfxProfile = settings.VfxProfile;
        definition.duration = Mathf.Max(0f, settings.Duration);
        definition.maxStacks = Mathf.Max(0, settings.MaxStacks);
        definition.stackMode = settings.StackMode;

        // Numeric status only: control blocks, taunt tags, trigger rules and stun pushing stay at
        // their defaults. Those behaviours need an authored Def, which is exactly why the wizard
        // asks the designer to pick an existing status instead of inventing one here.
        definition.modifiers = new List<StatusEffectModifier>();

        AssetDatabase.CreateAsset(definition, path);
        Undo.RegisterCreatedObjectUndo(definition, "Create Status Effect");
        StatusEffectScopeUtility.SetScope(definition, settings.Scope, request.Skill);
        AssetDatabase.SaveAssetIfDirty(definition);
        return definition;
    }

    public static string ResolveNewEffectId(NewStatusEffectRequest settings)
    {
        if (settings == null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(settings.EffectId)
            ? settings.AssetName?.Trim() ?? string.Empty
            : settings.EffectId.Trim();
    }

    public static string ResolveNewEffectPath(StatusEffectAuthoringRequest request)
    {
        NewStatusEffectRequest settings = request?.NewEffect;
        if (settings == null || string.IsNullOrWhiteSpace(settings.AssetName))
            return string.Empty;

        string folder = string.IsNullOrWhiteSpace(settings.FolderOverride)
            ? StatusEffectScopeUtility.GetDefaultFolder(settings.Scope, settings.Category, request.Skill)
            : settings.FolderOverride.TrimEnd('/');

        return $"{folder}/{StatusEffectScopeUtility.SanitizeFolderName(settings.AssetName.Trim())}.asset";
    }

    // A conditional application lives either on the composite (HealArea) or on an embedded payload
    // sub-asset; either way the skill asset is the file that has to be written back to disk.
    static void MarkSkillDirty(SkillGemDefinition skill, UnityEngine.Object container)
    {
        if (container != null)
            EditorUtility.SetDirty(container);
        if (skill != null)
            EditorUtility.SetDirty(skill);
    }

    static SkillUpgradeValidationIssue Error(string message)
        => new(SkillUpgradeValidationSeverity.Error, message);

    static SkillUpgradeValidationIssue Warning(string message)
        => new(SkillUpgradeValidationSeverity.Warning, message);

    #endregion

    #region Scope fixes

    /// <summary>
    /// ทางแก้ "Use Existing / Edit Existing / Promote To Global / Duplicate As Unique / Choose Another".
    ///
    /// หมายเหตุ: scope เก็บเป็น asset label ซึ่งอยู่ในไฟล์ .meta — Undo ของ Unity ไม่ครอบคลุม label
    /// ดังนั้นการเปลี่ยน scope อย่างเดียวถอยด้วย Ctrl+Z ไม่ได้ ต้องกดปุ่ม scope อีกฝั่งเพื่อกลับ.
    /// </summary>
    public static void PromoteToGlobal(StatusEffectDef definition)
    {
        if (definition == null)
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Promote Status Effect To Global");
        int group = Undo.GetCurrentGroup();

        Undo.RecordObject(definition, "Promote Status Effect To Global");
        StatusEffectScopeUtility.SetScope(definition, StatusEffectScope.Global, null);
        AssetDatabase.SaveAssetIfDirty(definition);

        Undo.CollapseUndoOperations(group);
    }

    /// <summary>
    /// ลด scope เป็น Unique ได้ก็ต่อเมื่อมีสกิลเดียวใช้อยู่ — ไม่งั้นการกดปุ่มเดียวจะทำให้สกิลอื่นที่ยัง
    /// อ้าง Def ตัวนี้กลายเป็น error ทันทีโดยที่คนกดไม่รู้ตัว.
    /// </summary>
    public static bool TryMakeUnique(StatusEffectDef definition, SkillDefinitionBase owner, out string error)
    {
        if (!CanMakeUnique(definition, owner, out error))
            return false;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Make Status Effect Unique");
        int group = Undo.GetCurrentGroup();

        Undo.RecordObject(definition, "Make Status Effect Unique");
        StatusEffectScopeUtility.SetScope(definition, StatusEffectScope.Unique, owner);
        AssetDatabase.SaveAssetIfDirty(definition);

        Undo.CollapseUndoOperations(group);
        return true;
    }

    public static bool CanMakeUnique(
        StatusEffectDef definition,
        SkillDefinitionBase owner,
        out string error)
    {
        error = null;
        if (definition == null || owner == null)
        {
            error = "Both a status effect and an owning skill are required.";
            return false;
        }

        if (string.IsNullOrEmpty(StatusEffectScopeUtility.GetAssetGuid(owner)))
        {
            error = "Making a status Unique needs a saved owning skill asset.";
            return false;
        }

        List<SkillDefinitionBase> users = StatusEffectScopeValidator.FindUsingSkills(definition);
        for (int i = users.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(users[i], owner))
                users.RemoveAt(i);
        }

        if (users.Count == 0)
            return true;

        var names = new List<string>();
        for (int i = 0; i < users.Count; i++)
            names.Add(users[i].name);

        error =
            $"'{definition.name}' is still used by {users.Count} other skill(s) " +
            $"({string.Join(", ", names)}). Make it unique only after those stop using it, or " +
            "duplicate it as a unique copy instead.";
        return false;
    }

    /// <summary>คัดลอก Def เป็นตัวใหม่ที่เป็น Unique ของสกิลนี้ แล้วคืน asset ใหม่ (null = ทำไม่สำเร็จ).</summary>
    public static StatusEffectDef DuplicateAsUnique(StatusEffectDef source, SkillGemDefinition owner)
    {
        if (source == null || owner == null)
            return null;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Duplicate Status Effect As Unique");
        int group = Undo.GetCurrentGroup();

        try
        {
            StatusEffectDef copy = DuplicateAsUniqueAsset(source, owner);
            Undo.CollapseUndoOperations(group);
            return copy;
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(exception);
            return null;
        }
    }

    static StatusEffectDef DuplicateAsUniqueAsset(StatusEffectDef source, SkillGemDefinition owner)
    {
        string folder = StatusEffectScopeUtility.GetDefaultFolder(
            StatusEffectScope.Unique, source.category, owner);
        StatusEffectScopeUtility.EnsureFolder(folder);

        string sourceName = StatusEffectScopeUtility.SanitizeFolderName(source.name);
        string baseName = $"{StatusEffectScopeUtility.SanitizeFolderName(owner.name)}_{sourceName}";
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}.asset");
        if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path))
            throw new InvalidOperationException($"Could not duplicate '{source.name}' to '{path}'.");

        var copy = AssetDatabase.LoadAssetAtPath<StatusEffectDef>(path);
        if (copy == null)
            throw new InvalidOperationException($"Could not load duplicated status at '{path}'.");

        // effectId must stay unique, and the copy is a different status identity by construction.
        copy.effectId = System.IO.Path.GetFileNameWithoutExtension(path);
        StatusEffectScopeUtility.SetScope(copy, StatusEffectScope.Unique, owner);
        AssetDatabase.SaveAssetIfDirty(copy);
        Undo.RegisterCreatedObjectUndo(copy, "Duplicate Status Effect As Unique");
        return copy;
    }

    #endregion
}
#endif
