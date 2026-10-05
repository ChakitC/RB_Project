using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class RomaPassiveExample
{
    public static ComposedPassiveDef Create(List<Object> owned)
    {
        T Make<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); owned.Add(asset); return asset; }
        var definition = Make<ComposedPassiveDef>();
        definition.name = "Roma Stamina Momentum Example";
        definition.passiveId = "test.roma.stamina-momentum";
        definition.description = "Test parameters, not a production loadout.";
        definition.recipe = Make<PassiveRecipe>();
        var recipe = definition.recipe;
        recipe.name = "Roma Recipe";
        recipe.variables.Add(new PassiveVariable { id = "momentum", maximum = 100 });
        var restore = Make<PassiveFragment>(); restore.name = "Perfect Dash Restore";
        restore.parameters.Add(new PassiveParameter { id = "amount", number = 10 });
        restore.steps.Add(new PassiveResourceStep { id = "restore", amount = new PassiveNumber { source = PassiveValueSource.Parameter, reference = "amount" } });
        recipe.rules.Add(new PassiveRecipeRule { id = "perfect-dash", eventType = PassiveEventType.PerfectDodge,
            once = PassiveOnceScope.Dash, steps = new() { new PassiveFragmentStep { id = "restore", fragment = restore } } });
        var conversion = Make<PassiveFragment>(); conversion.name = "Regen To Maximum";
        conversion.parameters.Add(new PassiveParameter { id = "ratio", number = 4 });
        conversion.steps.Add(new PassiveConversionStep { id = "convert", ratio = new PassiveNumber { source = PassiveValueSource.Parameter, reference = "ratio" } });
        recipe.rules.Add(new PassiveRecipeRule { id = "conversion", trigger = PassiveRuleTrigger.Equipped,
            steps = new() { new PassiveFragmentStep { id = "conversion", fragment = conversion } } });
        var missing = Make<PassiveFragment>(); missing.name = "Missing Stamina Damage";
        missing.steps.Add(new PassiveDynamicModifierStep { id = "damage", condition = new PassiveCondition { kind = PassiveConditionKind.StaminaBelow, threshold = new(0.7f) },
            value = new PassiveNumber { source = PassiveValueSource.MissingStaminaRatio, multiply = 50 } });
        recipe.rules.Add(new PassiveRecipeRule { id = "missing-stamina", trigger = PassiveRuleTrigger.Equipped,
            steps = new() { new PassiveFragmentStep { id = "damage", fragment = missing } } });
        var momentum = Make<PassiveFragment>(); momentum.name = "Spent Stamina Momentum";
        momentum.variables.Add(new PassiveVariable { id = "charge", maximum = 100 });
        momentum.steps.Add(new PassiveBranchStep { id = "mode", condition = new PassiveCondition { kind = PassiveConditionKind.EventType, eventType = PassiveEventType.ResourceSpent },
            whenTrue = new() { new PassiveAccumulatorStep { id = "add", variable = "charge", amount = new PassiveNumber { source = PassiveValueSource.EventValue } } },
            whenFalse = new() { new PassiveNextAttackStep { id = "next-attack", variable = "charge", damagePercentPerUnit = new(2) } } });
        PassiveFragmentStep Call(string id) => new() { id = id, fragment = momentum,
            bindings = new() { new PassiveVariableBinding { local = "charge", parent = "momentum" } } };
        recipe.rules.Add(new PassiveRecipeRule { id = "momentum-ready", trigger = PassiveRuleTrigger.Equipped, steps = new() { Call("register") } });
        recipe.rules.Add(new PassiveRecipeRule { id = "momentum-spent", trigger = PassiveRuleTrigger.ResourceSpent, steps = new() { Call("accumulate") } });
        return definition;
    }

    public static string Save()
    {
        const string path = "Assets/Tests/PassiveComposition/Fixtures/RomaStaminaMomentum.asset";
        if (AssetDatabase.LoadAssetAtPath<PassiveDefinition>(path) != null) return path;
        if (!AssetDatabase.IsValidFolder("Assets/Tests/PassiveComposition/Fixtures")) AssetDatabase.CreateFolder("Assets/Tests/PassiveComposition", "Fixtures");
        var owned = new List<Object>(); var definition = Create(owned);
        AssetDatabase.CreateAsset(definition, path);
        foreach (var asset in owned) if (asset != definition) AssetDatabase.AddObjectToAsset(asset, definition);
        definition.SetDefinitionIdForMigration(AssetDatabase.AssetPathToGUID(path));
        EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
        return path;
    }
}
