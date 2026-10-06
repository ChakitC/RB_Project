using System.Collections.Generic;
using UnityEditor;

public static class RomaCombatSurgeAuthoring
{
    public const string Path = "Assets/Character/Roma/Passive/Passive.Roma_StaminaMomentum.asset";

    public static void Install()
    {
        var definition = AssetDatabase.LoadAssetAtPath<PassiveDefinition>(Path);
        Configure(definition.recipe);
        definition.description = "Perfect Dash restores 10 Stamina once per Dash. Convert each +1 Stamina Regen bonus into +4 Max Stamina. Below 70% Stamina, gain +0.5% Damage per 1% missing Stamina. Spending Stamina builds Momentum to 100. The next successful normal shot consumes 100 to activate Combat Surge for 5 seconds: +30% reload speed and 50% less aim movement penalty. Momentum cannot build during Combat Surge.";
        EditorUtility.SetDirty(definition.recipe);
        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssetIfDirty(definition);
    }

    public static void Configure(PassiveRecipe recipe)
    {
        recipe.rules.RemoveAll(r => r.id == "momentum-ready" || r.id == "momentum-spent" || r.id == "combat-surge");
        recipe.variables.RemoveAll(v => v.id == "surge-active");
        recipe.variables.Add(new PassiveVariable { id = "surge-active", maximum = 1 });
        recipe.rules.Add(new PassiveRecipeRule
        {
            id = "momentum-spent", trigger = PassiveRuleTrigger.ResourceSpent,
            condition = VariableCondition("surge-active", PassiveConditionKind.NumberAtMost, 0),
            steps = new() { new PassiveAccumulatorStep { id = "accumulate", variable = "momentum", amount = new PassiveNumber { source = PassiveValueSource.EventValue } } }
        });
        recipe.rules.Add(new PassiveRecipeRule
        {
            id = "momentum-ready", trigger = PassiveRuleTrigger.Equipped,
            steps = new()
            {
                new PassiveDynamicModifierStep { id = "reload-speed", stat = StatType.ReloadSpeed, operation = ModifierOp.AddPercent,
                    value = new(30), condition = VariableCondition("surge-active", PassiveConditionKind.NumberGreater, 0) },
                new PassiveDynamicModifierStep { id = "aim-mobility", stat = StatType.AimMovementPenaltyReduction, operation = ModifierOp.Flat,
                    value = new(50), condition = VariableCondition("surge-active", PassiveConditionKind.NumberGreater, 0) }
            }
        });
        recipe.rules.Add(new PassiveRecipeRule
        {
            id = "combat-surge", trigger = PassiveRuleTrigger.ShotCommitted,
            condition = new PassiveCondition { kind = PassiveConditionKind.All, children = new()
            {
                VariableCondition("momentum", PassiveConditionKind.NumberAtLeast, 100),
                VariableCondition("surge-active", PassiveConditionKind.NumberAtMost, 0)
            } },
            steps = new()
            {
                new PassiveAccumulatorStep { id = "consume", variable = "momentum", operation = PassiveAccumulatorOperation.Set, amount = new(0) },
                new PassiveAccumulatorStep { id = "activate", variable = "surge-active", operation = PassiveAccumulatorOperation.Set, amount = new(1) },
                new PassiveExpiryStep { id = "duration", duration = new(5), variables = new List<string> { "surge-active" } }
            }
        });
        var errors = PassiveRecipeValidator.Validate(recipe);
        if (errors.Count > 0) throw new System.InvalidOperationException(string.Join("\n", errors));
    }

    static PassiveCondition VariableCondition(string id, PassiveConditionKind kind, float threshold) => new()
    { kind = kind, number = new PassiveNumber { source = PassiveValueSource.Variable, reference = id }, threshold = new(threshold) };
}
