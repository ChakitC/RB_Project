using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Authoring-only translation. Runtime never interprets legacy affix kinds.</summary>
public static class WeaponPassiveRecipeMigration
{
    public static PassiveNumber Roll(float multiply = 1, string variable = null) => new()
    { source = PassiveValueSource.Parameter, reference = "roll", multiply = multiply, multiplyVariable = variable };
    static PassiveNumber Variable(string id) => new() { source = PassiveValueSource.Variable, reference = id };
    static PassiveNumber Fact(PassiveValueSource source, float multiply = 1, bool roll = false) => new()
    { source = source, multiply = multiply, multiplyParameter = roll ? "roll" : null };
    static PassiveCondition Flag(PassiveConditionKind kind) => new() { kind = kind };
    static PassiveCondition All(params PassiveCondition[] conditions) => new() { kind = PassiveConditionKind.All, children = new(conditions) };
    static PassiveCondition PositiveDamage() => new() { kind = PassiveConditionKind.NumberGreater, number = Fact(PassiveValueSource.AppliedDamage), threshold = new(0) };
    static PassiveRecipeRule Hit(params PassiveStep[] steps)
    { var rule = Rule(PassiveRuleTrigger.Combat, PassiveEventType.Hit, steps); rule.condition = PositiveDamage(); return rule; }
    static PassiveRecipeRule Kill(params PassiveStep[] steps)
    { var rule = Rule(PassiveRuleTrigger.Combat, PassiveEventType.Kill, steps); rule.condition = PositiveDamage(); return rule; }
    static PassiveAmmoStep MagazinePercent(PassiveValueSource source)
    { var amount = Fact(source, .01f, true); amount.ceilToInteger = true; amount.minimum = 1; return new() { amount = amount }; }
    static PassiveCondition AtLeast(string id, PassiveNumber value) => new()
    { kind = PassiveConditionKind.NumberAtLeast, number = Variable(id), threshold = value };
    static PassiveAccumulatorStep Set(string variable, float value) => new()
    { variable = variable, operation = PassiveAccumulatorOperation.Set, amount = new(value) };
    static PassiveBranchStep If(PassiveCondition condition, params PassiveStep[] steps) => new()
    { condition = condition, whenTrue = new(steps) };
    static PassiveRecipeRule Rule(PassiveRuleTrigger trigger, PassiveEventType type, params PassiveStep[] steps) => new()
    { trigger = trigger, eventType = type, steps = new(steps), oncePerTargetPerChain = true };
    static PassiveExpiryStep Expire(float duration, params string[] variables) => new()
    { duration = new(Mathf.Max(.01f, duration)), variables = new(variables) };
    static void Variable(PassiveRecipe recipe, string id, float maximum, bool persistent = false) =>
        recipe.variables.Add(new() { id = id, maximum = maximum, persistent = persistent });
    static void Modifier(PassiveRecipe recipe, StatType stat, ModifierOp operation, PassiveNumber number, PassiveCondition condition = null) =>
        recipe.rules.Add(Rule(PassiveRuleTrigger.Equipped, PassiveEventType.None,
            new PassiveDynamicModifierStep { stat = stat, scope = StatModifierRules.SupportsWeaponScope(stat) ? StatModifierScope.WeaponOnly : StatModifierScope.Global,
                operation = operation, value = number, condition = condition ?? new() }));

    public static PassiveRecipe Build(ConfiguredWeaponAffixBehavior behavior)
    {
        var recipe = ScriptableObject.CreateInstance<PassiveRecipe>();
        recipe.name = "Passive recipe";
        recipe.parameters.Add(new PassiveParameter { id = "roll", number = behavior.RollSpec.minimum });
        switch (behavior.kind)
        {
            case WeaponAffixRuntimeKind.StaticStat:
            case WeaponAffixRuntimeKind.ImpactCore:
                Modifier(recipe, behavior.statType, behavior.modifierOp, Roll()); break;
            case WeaponAffixRuntimeKind.ReloadBuff:
            case WeaponAffixRuntimeKind.KillClip:
                Variable(recipe, "active", 1);
                Modifier(recipe, behavior.statType, behavior.modifierOp, Roll(), AtLeast("active", new(1)));
                recipe.rules.Add(behavior.kind == WeaponAffixRuntimeKind.ReloadBuff
                    ? Rule(PassiveRuleTrigger.Combat, PassiveEventType.Reload, Set("active", 1), Expire(behavior.duration, "active"))
                    : Kill(Set("active", 1), Expire(behavior.duration, "active")));
                break;
            case WeaponAffixRuntimeKind.FreshChamber:
                Variable(recipe, "charges", 3);
                recipe.rules.Add(Rule(PassiveRuleTrigger.Combat, PassiveEventType.Reload, Set("charges", 3)));
                var charged = Rule(PassiveRuleTrigger.BeforeShot, PassiveEventType.None,
                    new PassiveReserveVariableStep { variable = "charges" },
                    new PassiveShotModifierStep { field = PassiveShotField.DamagePercent, value = Roll() });
                charged.condition = new() { kind = PassiveConditionKind.All, children = new()
                    { new() { kind = PassiveConditionKind.AmmoConsumed }, AtLeast("charges", new(1)) } };
                recipe.rules.Add(charged); break;
            case WeaponAffixRuntimeKind.EchoChamber:
            case WeaponAffixRuntimeKind.BreachChamber:
                Variable(recipe, "progress", 1000000, true);
                var shot = Rule(PassiveRuleTrigger.ShotCommitted, PassiveEventType.None,
                    new PassiveAccumulatorStep { variable = "progress" },
                    If(AtLeast("progress", new(Mathf.Max(1, behavior.threshold))), Set("progress", 0),
                        If(new() { kind = PassiveConditionKind.Chance, threshold = new(behavior.procChance <= 0 ? 1 : behavior.procChance) },
                            new PassiveProjectileStep { config = behavior.projectileConfig, prefab = behavior.projectilePrefab,
                                damageMultiplier = new(behavior.fixedValue), speedMultiplier = new(behavior.secondaryValue) })));
                shot.condition = new() { kind = PassiveConditionKind.AmmoConsumed };
                recipe.rules.Add(shot); break;
            case WeaponAffixRuntimeKind.ExecutionFeed:
                recipe.rules.Add(Kill(MagazinePercent(PassiveValueSource.CurrentMagazineSize))); break;
            case WeaponAffixRuntimeKind.LastRound:
                var lastRound = Rule(PassiveRuleTrigger.BeforeShot, PassiveEventType.None,
                    new PassiveShotModifierStep { field = PassiveShotField.ImpactDamagePercent, radius = 2.5f, value = Roll() },
                    new PassiveShotModifierStep { field = PassiveShotField.DamagePercent, value = new(25) });
                lastRound.condition = All(Flag(PassiveConditionKind.AmmoConsumed), Flag(PassiveConditionKind.LastRound));
                recipe.rules.Add(lastRound); break;
            case WeaponAffixRuntimeKind.HeadHunter:
                Variable(recipe, "stacks", 5);
                Modifier(recipe, StatType.CritMultiplier, ModifierOp.Flat, Roll(1, "stacks"), AtLeast("stacks", new(1)));
                var head = If(Flag(PassiveConditionKind.HeadHit), new PassiveAccumulatorStep { variable = "stacks" }, Expire(behavior.duration, "stacks"));
                head.whenFalse.Add(Set("stacks", 0));
                var headRule = Hit(head); headRule.once = PassiveOnceScope.Attack; recipe.rules.Add(headRule); break;
            case WeaponAffixRuntimeKind.HotStreak:
                Variable(recipe, "stacks", 3);
                Modifier(recipe, StatType.FireInterval, ModifierOp.AddPercent, Roll(-1, "stacks"), AtLeast("stacks", new(1)));
                recipe.rules.Add(Kill(new PassiveAccumulatorStep { variable = "stacks" }, Expire(behavior.duration, "stacks"))); break;
            case WeaponAffixRuntimeKind.PressurePoint:
            case WeaponAffixRuntimeKind.MarkedQuarry:
                Variable(recipe, "stacks", 5);
                var tracking = Hit(new PassiveTrackTargetStep { resetVariable = "stacks" }, new PassiveAccumulatorStep { variable = "stacks" },
                    Expire(behavior.duration > 0 ? behavior.duration : 3, "stacks"));
                tracking.once = PassiveOnceScope.Attack; recipe.rules.Add(tracking);
                bool marked = behavior.kind == WeaponAffixRuntimeKind.MarkedQuarry;
                var damage = Rule(PassiveRuleTrigger.BeforeDamage, PassiveEventType.None,
                    new PassiveShotModifierStep { field = marked ? PassiveShotField.DamagePercent : PassiveShotField.StaggerPercent,
                        value = Roll(1, marked ? null : "stacks") });
                damage.condition = All(new() { kind = PassiveConditionKind.TrackedTarget, id = "target" }, AtLeast("stacks", new(marked ? 5 : 1)));
                recipe.rules.Add(damage); break;
            case WeaponAffixRuntimeKind.ConservationRound:
                Variable(recipe, "progress", 1000000, true);
                var threshold = Roll(); threshold.roundToInteger = true; threshold.minimum = 1;
                var conservation = Hit(new PassiveAccumulatorStep { variable = "progress" },
                    If(AtLeast("progress", threshold), new PassiveAmmoStep(), If(Flag(PassiveConditionKind.LastSucceeded), Set("progress", 0))));
                conservation.once = PassiveOnceScope.Attack; recipe.rules.Add(conservation); break;
            case WeaponAffixRuntimeKind.BrokenGuard:
                Variable(recipe, "charges", 1);
                var broken = Hit(Set("charges", 1), Expire(5, "charges"));
                broken.condition = All(PositiveDamage(), Flag(PassiveConditionKind.EnteredChainReady)); recipe.rules.Add(broken);
                var critical = Rule(PassiveRuleTrigger.BeforeShot, PassiveEventType.None,
                    new PassiveReserveVariableStep { variable = "charges" },
                    new PassiveShotModifierStep { field = PassiveShotField.CritRate, value = new(100) },
                    new PassiveShotModifierStep { field = PassiveShotField.CritMultiplier, value = Roll() });
                critical.condition = All(Flag(PassiveConditionKind.AmmoConsumed), AtLeast("charges", new(1))); recipe.rules.Add(critical); break;
            case WeaponAffixRuntimeKind.BloodMagazine:
                Variable(recipe, "active", 1);
                Modifier(recipe, StatType.Stability, ModifierOp.Flat, new(15), AtLeast("active", new(1)));
                var blood = Kill(MagazinePercent(PassiveValueSource.MaxMagazine), Set("active", 1), Expire(behavior.duration, "active"));
                blood.once = PassiveOnceScope.Attack;
                blood.condition = All(PositiveDamage(), new() { kind = PassiveConditionKind.NumberAtMost,
                    number = Fact(PassiveValueSource.AmmoAfter), threshold = Fact(PassiveValueSource.MaxMagazine, .25f) });
                recipe.rules.Add(blood); break;
            case WeaponAffixRuntimeKind.Overkill:
                var overkill = Kill(new PassiveAreaDamageStep { damage = Fact(PassiveValueSource.Overkill, .01f, true),
                    maximumDamage = Fact(PassiveValueSource.ResolvedDamage), radius = new(3) });
                overkill.condition = All(PositiveDamage(), new() { kind = PassiveConditionKind.NumberGreater,
                    number = Fact(PassiveValueSource.MaxHealth), threshold = new(0) },
                    new() { kind = PassiveConditionKind.NumberAtLeast, number = Fact(PassiveValueSource.Overkill), threshold = Fact(PassiveValueSource.MaxHealth, .2f) });
                overkill.requireTarget = true; recipe.rules.Add(overkill); break;
            default: UnityEngine.Object.DestroyImmediate(recipe); throw new NotSupportedException(behavior.kind.ToString());
        }
        AddFeedback(recipe, behavior.kind);
        StableIds(recipe);
        return recipe;
    }
    static void AddFeedback(PassiveRecipe recipe, WeaponAffixRuntimeKind kind)
    {
        foreach (var rule in recipe.rules) Visit(rule.steps);
        void Visit(List<PassiveStep> steps)
        {
            foreach (var step in steps)
            {
                if (step is PassiveAccumulatorStep accumulator && accumulator.variable == "charges" && accumulator.operation == PassiveAccumulatorOperation.Set && accumulator.amount.value > 0)
                { step.successSignal = "ChargeReady"; step.successValueVariable = "charges"; }
                if (step is PassiveReserveVariableStep reserve) reserve.commitSignal = "Consumed";
                if (step is PassiveExpiryStep expiry)
                {
                    expiry.expirySignal = "Expired";
                    if (kind != WeaponAffixRuntimeKind.BrokenGuard)
                    {
                        step.successSignal = kind == WeaponAffixRuntimeKind.PressurePoint || kind == WeaponAffixRuntimeKind.MarkedQuarry ? "StackChanged" : "Activated";
                        step.successValueVariable = expiry.variables.Contains("stacks") ? "stacks" : null;
                    }
                }
                if (step is PassiveBranchStep branch) { Visit(branch.whenTrue); Visit(branch.whenFalse); }
            }
        }
    }

    public static string MigrateAll(bool dryRun = true)
    {
        var database = AssetDatabase.LoadAssetAtPath<WeaponAffixDatabase>("Assets/Scripts/Weapons/WeaponAffixDatabase.asset");
        int count = 0;
        foreach (var definition in database.Affixes)
        {
            if (definition == null || definition.rootBehavior is not ConfiguredWeaponAffixBehavior behavior)
                throw new InvalidOperationException("Unsupported affix; no asset was silently skipped.");
            var recipe = Build(behavior);
            var errors = PassiveRecipeValidator.Validate(recipe);
            if (errors.Count > 0) { UnityEngine.Object.DestroyImmediate(recipe); throw new InvalidOperationException(definition.name + ": " + string.Join("; ", errors)); }
            if (!dryRun && definition.passiveSchemaVersion == 0)
            {
                definition.rollSpec = behavior.RollSpec; definition.tooltip = behavior.Tooltip;
                definition.hasTimedEffect = behavior.HasTimedEffect; definition.rolledValueIsStat = behavior.RolledValueModifierOp.HasValue;
                definition.rolledValueOperation = behavior.RolledValueModifierOp ?? ModifierOp.Flat;
                definition.minimumBaseMagazine = behavior.minimumBaseMagazine; definition.passiveSchemaVersion = 1;
                EditorUtility.SetDirty(definition);
            }
            if (!dryRun && definition.passiveSchemaVersion < 2)
            {
                var current = definition.passiveDefinition != null ? definition.passiveDefinition.recipe : recipe;
                AddFeedback(current, behavior.kind); definition.passiveSchemaVersion = 2;
                EditorUtility.SetDirty(current); EditorUtility.SetDirty(definition);
            }
            if (dryRun || definition.passiveDefinition != null) { UnityEngine.Object.DestroyImmediate(recipe); count++; continue; }
            string path = AssetDatabase.GetAssetPath(definition);
            var passive = ScriptableObject.CreateInstance<ComposedPassiveDef>();
            passive.name = definition.displayName + " Passive"; passive.passiveId = definition.affixId;
            passive.description = definition.description; passive.recipe = recipe;
            AssetDatabase.AddObjectToAsset(passive, definition); AssetDatabase.AddObjectToAsset(recipe, definition);
            passive.SetDefinitionIdForMigration(AssetDatabase.AssetPathToGUID(path));
            definition.passiveDefinition = passive;
            EditorUtility.SetDirty(definition); EditorUtility.SetDirty(passive); EditorUtility.SetDirty(recipe); count++;
        }
        if (!dryRun) AssetDatabase.SaveAssets();
        return (dryRun ? "Validated " : "Migrated ") + count + " affixes";
    }

    static void StableIds(PassiveRecipe recipe)
    {
        for (int i = 0; i < recipe.rules.Count; i++)
        { recipe.rules[i].id = "rule-" + i; SetIds(recipe.rules[i].steps, "step"); }
    }
    static void SetIds(List<PassiveStep> steps, string prefix)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i]; step.id = prefix + "-" + i;
            if (step is PassiveBranchStep branch) { SetIds(branch.whenTrue, step.id + "-yes"); SetIds(branch.whenFalse, step.id + "-no"); }
        }
    }
}
