using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PassiveRecipeMigration
{
    [MenuItem("Tools/RB/Passives/Dry Run Recipe Migration")]
    static void DryRunMenu() => Debug.Log(JsonUtility.ToJson(Run(true), true));
    [MenuItem("Tools/RB/Passives/Migrate Recipes")]
    static void MigrateMenu() => Debug.Log(JsonUtility.ToJson(Run(false), true));

    [Serializable]
    public sealed class Report
    {
        public int total, pending, migrated;
        public List<string> errors = new();
        public List<string> assets = new();
    }
    public static Report Run(bool dryRun)
    {
        var report = new Report();
        foreach (string guid in AssetDatabase.FindAssets("t:PassiveDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var definition = AssetDatabase.LoadAssetAtPath<PassiveDefinition>(path);
            if (definition == null) continue;
            report.total++;
            if (definition.recipe != null)
            {
                foreach (var error in PassiveRecipeValidator.Validate(definition.recipe)) report.errors.Add(path + ": " + error);
                continue;
            }
            report.pending++; report.assets.Add(path);
            PassiveRecipe recipe = null;
            try
            {
                recipe = Convert(definition);
                var errors = PassiveRecipeValidator.Validate(recipe);
                if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors));
                if (!dryRun)
                {
                    recipe.name = definition.name + " Recipe";
                    AssetDatabase.AddObjectToAsset(recipe, definition);
                    definition.recipe = recipe;
                    definition.SetDefinitionIdForMigration(guid);
                    EditorUtility.SetDirty(recipe); EditorUtility.SetDirty(definition);
                    AssetDatabase.SaveAssetIfDirty(definition);
                    report.migrated++;
                }
            }
            catch (Exception exception) { report.errors.Add(path + ": " + exception.Message); }
            finally { if (recipe != null && !AssetDatabase.Contains(recipe)) UnityEngine.Object.DestroyImmediate(recipe); }
        }
        return report;
    }

    public static PassiveRecipe Convert(PassiveDefinition definition)
    {
        var recipe = ScriptableObject.CreateInstance<PassiveRecipe>();
        if (definition is AlwaysOnPassiveDef always)
        {
            var rule = new PassiveRecipeRule { id = "equipped", trigger = PassiveRuleTrigger.Equipped };
            rule.steps.Add(new PassiveModifierStep { id = "modifiers", whileEquipped = true, modifier = new PassiveActionDefinition { modifiers = always.modifiers } });
            recipe.rules.Add(rule);
        }
        else if (definition is TriggeredPassiveDef triggered)
        {
            foreach (var old in triggered.rules)
            {
                var rule = new PassiveRecipeRule
                {
                    id = old.RuntimeRuleId, eventType = old.trigger, originFilter = old.originFilter,
                    count = new(old.requiredCount), window = new(old.countWindowSeconds), cooldown = new(old.cooldownSeconds),
                    requireTarget = old.requireTarget, requireAttackId = old.requireAttackId,
                    oncePerTargetPerChain = old.oncePerTargetPerChain, counterConsumeMode = old.counterConsumeMode,
                    optionalSource = old.eventSourceKind, eventSourceId = old.RuntimeEventSourceId,
                    sourceDistance = old.eventSourceFloatValue, sourceValue = old.eventSourceIntValue
                };
                if (!string.IsNullOrWhiteSpace(old.requiredUpgradeId)) rule.condition = new PassiveCondition { kind = PassiveConditionKind.HasUpgrade, id = old.requiredUpgradeId };
                foreach (var upgrade in old.upgradeOverrides)
                {
                    var number = upgrade.field == PassiveRuleField.RequiredCount ? rule.count : upgrade.field == PassiveRuleField.CountWindowSeconds ? rule.window : rule.cooldown;
                    number.upgrades.Add(new PassiveNumericUpgrade { upgradeId = upgrade.upgradeId, operation = upgrade.operation, value = upgrade.value });
                }
                foreach (var action in old.actions)
                {
                    var target = new PassiveTarget { mode = action.targetSelector == PassiveTargetSelector.EventTarget ? PassiveTargetMode.EventTarget : PassiveTargetMode.Self };
                    switch (action.actionType)
                    {
                        case PassiveActionType.GrantModifier:
                            rule.steps.Add(new PassiveModifierStep { id = action.actionId, target = target, modifier = action }); break;
                        case PassiveActionType.ApplyStatusEffect:
                            rule.steps.Add(new PassiveStatusStep { id = action.actionId, target = target, spec = action.statusSpec, appliedById = action.appliedByIdOverride }); break;
                        case PassiveActionType.EmitEvent:
                            if (action.emittedEventType != PassiveEventType.PassiveSignal) throw new InvalidOperationException("Legacy synthetic combat events require explicit signal conversion.");
                            rule.steps.Add(new PassiveSignalStep { id = action.actionId, signal = action.emittedEventSourceIdOverride, value = new(action.emittedValue) }); break;
                    }
                }
                recipe.rules.Add(rule);
            }
        }
        else if (definition is CustomPassiveDef custom)
        {
            int index = 0;
            foreach (var behavior in custom.behaviors)
            {
                if (behavior == null) throw new InvalidOperationException("Missing custom behavior");
                var serialized = new SerializedObject(behavior);
                var rule = new PassiveRecipeRule { id = "behavior-" + index++, consume = PassiveConsumePolicy.OnSuccess };
                rule.chance = new(serialized.FindProperty("procChance").floatValue);
                if (behavior is RestoreMagazineOnDashPassiveBehavior)
                {
                    rule.eventType = PassiveEventType.DashStarted; rule.originFilter = PassiveOriginFilter.Any;
                    rule.steps.Add(new PassiveMagazineStep { id = "restore", ratio = new(serialized.FindProperty("magazineRestoreRatio").floatValue), minimum = serialized.FindProperty("minimumRestoreAmount").intValue });
                }
                else if (behavior is DropAmmoOnShotPassiveBehavior)
                {
                    rule.eventType = PassiveEventType.ShotFired;
                    rule.originFilter = (PassiveOriginFilter)serialized.FindProperty("originFilter").enumValueIndex;
                    rule.cooldown = new(serialized.FindProperty("internalCooldownSeconds").floatValue);
                    Upgrade(rule.chance, "procChanceBoostUpgradeId", "procChanceBonus", 1);
                    Upgrade(rule.cooldown, "cooldownReductionUpgradeId", "cooldownReductionSeconds", -1);
                    var pickup = new PassivePickupStep { id = "pickup" };
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(behavior), pickup);
                    rule.steps.Add(pickup);
                }
                else throw new InvalidOperationException("Unsupported behavior: " + behavior.GetType().Name);
                recipe.rules.Add(rule);
                void Upgrade(PassiveNumber number, string idField, string valueField, float sign)
                {
                    string id = serialized.FindProperty(idField).stringValue;
                    if (!string.IsNullOrWhiteSpace(id)) number.upgrades.Add(new PassiveNumericUpgrade { upgradeId = id, operation = ModifierOp.Flat, value = serialized.FindProperty(valueField).floatValue * sign });
                }
            }
        }
        else throw new InvalidOperationException("Definition has no recipe and no migration adapter");
        return recipe;
    }
}
