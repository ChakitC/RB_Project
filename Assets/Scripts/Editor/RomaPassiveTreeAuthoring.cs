using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RomaPassiveTreeAuthoring
{
    public const string TreePath = "Assets/Character/Roma/Passive/Roma_StaminaMomentum_UpgradeTree.asset";
    public const string Prefix = "roma.passive.";
    public const string Recovery = Prefix + "perfect-recovery", Reserve = Prefix + "stamina-reserve", Slip = Prefix + "slip-reload";
    public const string Hands = Prefix + "fast-hands", Aim = Prefix + "mobile-aim", Pressure = Prefix + "keep-the-pressure";
    public const string Deep = Prefix + "deep-reserve", Power = Prefix + "desperate-power", Redline = Prefix + "redline";

    public static void InstallTree(PassiveDefinition definition)
    {
        var tree = AssetDatabase.LoadAssetAtPath<SkillUpgradeTreeDefinition>(TreePath);
        if (tree == null) { tree = ScriptableObject.CreateInstance<SkillUpgradeTreeDefinition>(); AssetDatabase.CreateAsset(tree, TreePath); }
        tree.treeId = "roma.passive.stamina-momentum.tree.v1";
        tree.displayName = "Roma / Combat Surge";
        tree.description = "Three independent paths. Unlock all nine nodes for 12 shared skill points.";
        var nodes = new List<SkillUpgradeNodeData>();
        void Node(string id, string name, string description, int column, int row, string previous = null)
        {
            nodes.Add(new SkillUpgradeNodeData { nodeId = id, displayName = name, description = description,
                icon = LoadNodeIcon(id), cost = row == 2 ? 2 : 1, requiredCharacterLevel = 1,
                visualScale = row == 2 ? 1.5f : 1f, uiPosition = new Vector2((column - 1) * 320, 190 - row * 190),
                requiredNodeIds = previous == null ? new() : new() { previous }, grantedUpgradeIds = new() { id } });
        }
        Node(Recovery, "Perfect Recovery", "Perfect Dash restores 15 Stamina instead of 10 (once per Dash).", 0, 0);
        Node(Reserve, "Stamina Reserve", "+10 maximum Stamina.", 0, 1, Recovery);
        Node(Slip, "Slip Reload", "During Combat Surge, Perfect Dash transfers 25% magazine capacity (rounded up) from reserve. Once per Dash; up to 2 successful transfers per Surge. Does not trigger Reload effects.", 0, 2, Reserve);
        Node(Hands, "Fast Hands", "Combat Surge grants +40% reload speed instead of +30%.", 1, 0);
        Node(Aim, "Mobile Aim", "Combat Surge reduces aim movement penalty by 65% instead of 50%.", 1, 1, Hands);
        Node(Pressure, "Keep the Pressure", "During Combat Surge, every 5 normal shots that damage enemies extend Surge by 1 second, up to +3 seconds. Each shot counts once, including piercing hits.", 1, 2, Aim);
        Node(Deep, "Deep Reserve", "Convert each +1 Stamina Regen bonus into +5 Max Stamina instead of +4.", 2, 0);
        Node(Power, "Desperate Power", "Below 70% Stamina, gain +0.6% Damage per 1% missing Stamina instead of +0.5%.", 2, 1, Deep);
        Node(Redline, "Redline", "Opening Combat Surge locks your current low-Stamina Damage bonus as a minimum until Surge ends. Recovering Stamina cannot reduce it; a higher live bonus still applies without stacking.", 2, 2, Power);
        tree.nodes = nodes; definition.upgradeTree = tree;
        EditorUtility.SetDirty(tree); AssetDatabase.SaveAssetIfDirty(tree);
    }

    static Sprite LoadNodeIcon(string id)
    {
        string icon = id switch
        {
            Recovery => "15600", Reserve => "4050", Slip => "5100",
            Hands => "1090", Aim => "5040", Pressure => "1000",
            Deep => "4020", Power => "13260", Redline => "2080",
            _ => throw new System.ArgumentOutOfRangeException(nameof(id), id, "Unknown Roma node")
        };
        string path = $"Assets/Spain/Skill_Icon/Global/Skill_{icon}.png";
        return AssetDatabase.LoadAssetAtPath<Sprite>(path)
            ?? throw new System.InvalidOperationException($"Missing Roma node icon: {path}");
    }

    static PassiveCondition Upgrade(string id) => new() { kind = PassiveConditionKind.HasUpgrade, id = id };
    static PassiveNumber Variable(string id) => new() { source = PassiveValueSource.Variable, reference = id };
    static PassiveCondition Compare(string id, PassiveConditionKind kind, float threshold) => new() { kind = kind, number = Variable(id), threshold = new(threshold) };
    static PassiveCondition Active() => Compare("surge-active", PassiveConditionKind.NumberGreater, 0);
    static PassiveCondition All(params PassiveCondition[] values) => new() { kind = PassiveConditionKind.All, children = new(values) };
    static PassiveNumber Improved(float value, string upgrade, float bonus) => new(value) { upgrades = new() { new() { upgradeId = upgrade, operation = ModifierOp.Flat, value = bonus } } };
    static PassiveNumber Damage() => new() { source = PassiveValueSource.MissingStaminaRatio, multiply = 50,
        upgrades = new() { new() { upgradeId = Power, operation = ModifierOp.Multiply, value = 1.2f } } };
    static PassiveAccumulatorStep Set(string id, string variable, float value) => new() { id = id, variable = variable, operation = PassiveAccumulatorOperation.Set, amount = new(value) };

    public static void Configure(PassiveRecipe recipe)
    {
        recipe.rules.RemoveAll(r => r.id == "perfect-dash" || r.id == "conversion" || r.id == "missing-stamina" ||
            r.id == "stamina-reserve" || r.id == "slip-reload" || r.id == "keep-the-pressure");
        foreach (string id in new[] { "slip-uses", "pressure-hits", "redline-floor" }) recipe.variables.RemoveAll(v => v.id == id);
        recipe.variables.Add(new() { id = "slip-uses", maximum = 2 });
        recipe.variables.Add(new() { id = "pressure-hits", maximum = 15 });
        recipe.variables.Add(new() { id = "redline-floor", maximum = 100 });
        recipe.rules.Add(new() { id = "perfect-dash", eventType = PassiveEventType.PerfectDodge, once = PassiveOnceScope.Dash,
            steps = new() { new PassiveResourceStep { id = "restore", amount = Improved(10, Recovery, 5) } } });
        recipe.rules.Add(new() { id = "conversion", trigger = PassiveRuleTrigger.Equipped,
            steps = new() { new PassiveConversionStep { id = "convert", ratio = Improved(4, Deep, 1) } } });
        recipe.rules.Add(new() { id = "stamina-reserve", trigger = PassiveRuleTrigger.Equipped,
            steps = new() { new PassiveDynamicModifierStep { id = "maximum", stat = StatType.MaxStamina, operation = ModifierOp.Flat,
                condition = Upgrade(Reserve), value = new(10) } } });
        recipe.rules.Add(new() { id = "missing-stamina", trigger = PassiveRuleTrigger.Equipped,
            steps = new() { new PassiveDynamicModifierStep { id = "damage", stat = StatType.Damage, operation = ModifierOp.AddPercent,
                condition = new() { kind = PassiveConditionKind.StaminaBelow, threshold = new(.7f) }, value = Damage(), minimumVariable = "redline-floor" } } });
        var equipped = recipe.rules.Single(r => r.id == "momentum-ready");
        ((PassiveDynamicModifierStep)equipped.steps[0]).value = Improved(30, Hands, 10);
        ((PassiveDynamicModifierStep)equipped.steps[1]).value = Improved(50, Aim, 15);
        var start = recipe.rules.Single(r => r.id == "combat-surge");
        start.steps.Insert(0, Set("clear-floor", "redline-floor", 0));
        start.steps.Insert(1, Set("clear-slips", "slip-uses", 0));
        start.steps.Insert(2, Set("clear-pressure", "pressure-hits", 0));
        start.steps.Insert(3, new PassiveBranchStep { id = "redline", condition = All(Upgrade(Redline),
            new PassiveCondition { kind = PassiveConditionKind.StaminaBelow, threshold = new(.7f) }),
            whenTrue = new() { new PassiveAccumulatorStep { id = "capture", variable = "redline-floor", operation = PassiveAccumulatorOperation.Set, amount = Damage() } } });
        var expiry = (PassiveExpiryStep)start.steps.Last();
        expiry.cancelOnUpgradeChange = true;
        expiry.variables = new() { "surge-active", "redline-floor", "slip-uses", "pressure-hits" };

        recipe.rules.Add(new() { id = "slip-reload", eventType = PassiveEventType.PerfectDodge,
            condition = All(Upgrade(Slip), Active(), Compare("slip-uses", PassiveConditionKind.NumberAtMost, 1)),
            steps = new() { new PassiveExpiryEventStep { id = "dash", timerVariable = "surge-active", identity = PassiveOnceScope.Dash,
                steps = new()
                {
                    new PassiveReserveAmmoStep { id = "transfer", amount = new() { source = PassiveValueSource.CurrentMagazineSize, multiply = .25f, ceilToInteger = true }, successSignal = "slip-reload" },
                    new PassiveBranchStep { id = "count-success", condition = new() { kind = PassiveConditionKind.LastSucceeded },
                        whenTrue = new() { new PassiveAccumulatorStep { id = "count", variable = "slip-uses", amount = new(1) } } }
                } } } });
        recipe.rules.Add(new() { id = "keep-the-pressure", eventType = PassiveEventType.Hit, requireTarget = true, requireAttackId = true,
            condition = All(Upgrade(Pressure), Active(), Compare("pressure-hits", PassiveConditionKind.NumberAtMost, 14),
                new PassiveCondition { kind = PassiveConditionKind.NormalWeaponHit },
                new PassiveCondition { kind = PassiveConditionKind.TargetIdentity, targetIdentity = AITargetIdentity.Enemy }),
            steps = new() { new PassiveExpiryEventStep { id = "shot", timerVariable = "surge-active",
                steps = PressureSteps() } } });
    }

    static List<PassiveStep> PressureSteps()
    {
        var steps = new List<PassiveStep> { new PassiveAccumulatorStep { id = "count", variable = "pressure-hits", amount = new(1) } };
        foreach (int hit in new[] { 5, 10, 15 })
            steps.Add(new PassiveBranchStep { id = "at-" + hit,
                condition = All(Compare("pressure-hits", PassiveConditionKind.NumberAtLeast, hit), Compare("pressure-hits", PassiveConditionKind.NumberAtMost, hit)),
                whenTrue = new() { new PassiveExtendExpiryStep { id = "extend-" + hit, timerVariable = "surge-active", seconds = new(1), maximumExtension = new(3), successSignal = "pressure-extension" } } });
        return steps;
    }
}
