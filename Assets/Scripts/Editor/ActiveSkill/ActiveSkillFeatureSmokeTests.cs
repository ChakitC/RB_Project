using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public static class ActiveSkillFeatureSmokeTests
{
    [MenuItem("Tools/RB/Skills/Run Active Skill Core Smoke Tests")]
    public static void RunFromMenu() => RunFromCommandLine();

    public static void RunFromCommandLine()
    {
        var assets = new List<ScriptableObject>();
        try
        {
            TestCatchUpGrantsPointsOnce();
            TestCatchUpSubtractsSpentPoints();
            TestPassiveOptionSharesSkillPool(assets);
            TestRequiredUpgradeIdGatesRule();
            TestMixedKindSlotFailsValidation(assets);
            TestPassiveSlotMustBeLast(assets);
            TestPassiveDefinitionDeclaresTreeGrantedIds();
            TestPrerequisitesSharedPoolAndVariantIsolation(assets);
            TestResetUsesPaidCost(assets);
            TestTreeMismatchRefundsRemovedNodes(assets);
            TestRemovedNodeRefundsWithoutTreeIdChange(assets);
            TestFailedUnlockStillReportsReconciliation(assets);
            TestDeterministicStatStacking(assets);
            TestGrantedUpgradeIdsSnapshotAggregation(assets);
            TestUnsupportedStatAggregatesWithoutAffectingActiveSkillOutput();
            TestMutuallyExclusiveNodesRejectCanUnlock(assets);
            TestOneWayExclusionStillBlocksBothOrders(assets);
            TestEffectDurationAndHealPowerStatStacking();
            TestProjectileCountRoundingIsConsistent();
            TestSkillTreeDefaultAndVariantOverride(assets);
            TestVisualScaleMetrics();
            TestRuntimeNodeVisualScaleAndFrameFallback();
            TestGraphDisplaysAndRefreshesNodeIcon(assets);
            TestGraphVisualScaleUsesCenteredPosition(assets);
            TestGraphAllowsOneUnlocksPortToReachMultipleRequiresPorts(assets);
            TestVisualScaleValidationAndBounds(assets);
            TestValidationIssuesCarryOwningNodeId(assets);
            TestGrantSeveritiesAndCrossNodeDuplicates(assets);
            TestUsageScannerReadsPassiveRuleSites(assets);
            TestUsageScannerResolvesSkillStatusSites();
            TestRequiredPathPreviewAggregatesPrerequisiteChain(assets);
            TestRequiredPathPreviewZeroMultiplierZeroesLaterAdds(assets);
            TestRequiredPathPreviewExcludesOptionalNodes(assets);
            TestRequiredPathPreviewIsSafeAgainstCyclesAndMissingPrerequisites(assets);
            TestUnlockedAbilitiesHidesUsagesAlreadyOnStatusEffectsCard();
            TestTreeNavigationMath();
            TestUpgradeUiSkillTreeButtonWiring();
            Debug.Log("[ActiveSkillTests] All core smoke tests passed.");
        }
        finally
        {
            for (int i = 0; i < assets.Count; i++)
                UnityEngine.Object.DestroyImmediate(assets[i]);
        }
    }

    static void TestCatchUpGrantsPointsOnce()
    {
        var data = new CharacterProgressData
        {
            level = 6,
        };
        var model = new ActiveSkillProgressModel(null, data, data.level);

        Expect(model.EnsureInitialized(), "Old progress must be initialized once.");
        Equal(5, model.AvailablePoints, "Catch-up must grant one point for levels after level 1.");
        Expect(!model.EnsureInitialized(), "Catch-up must not run twice.");
        Equal(5, model.AvailablePoints, "Repeated initialization must not duplicate points.");
    }

    static void TestPassiveOptionSharesSkillPool(List<ScriptableObject> assets)
    {
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.passive-shared", Node("extra_drop", 2));
        var data = InitializedData(5);
        var model = new ActiveSkillProgressModel(null, data, 10);

        Expect(model.TryUnlock("passive.feno.bag", "passive.feno.bulletbag", tree, "extra_drop", out string reason), reason);
        Equal(3, model.AvailablePoints,
            "Unlocking a node in a passive-owned tree must drain the same shared skillPoints pool a skill tree uses.");
    }

    static void TestCatchUpSubtractsSpentPoints()
    {
        var data = new CharacterProgressData { level = 10 };
        data.activeSkillTrees.Add(new CharacterSkillTreeProgressSaveData
        {
            slotId = "slot",
            optionId = "variant",
            treeId = "tree.spent",
            unlockedNodes = new List<CharacterSkillUpgradeNodeSaveData>
            {
                new() { nodeId = "root", paidCost = 4 },
            },
        });
        var model = new ActiveSkillProgressModel(null, data, data.level);

        Expect(model.EnsureInitialized(), "Uninitialized progress with points already spent must still initialize once.");
        Equal(5, model.AvailablePoints,
            "Catch-up must grant (levels earned - already spent), not the full lifetime grant a second time.");
    }

    static void TestRequiredUpgradeIdGatesRule()
    {
        var gatedRule = new TriggeredPassiveRule { requiredUpgradeId = "passive.feno.bulletbag.extra_drop" };
        var emptySnapshot = new SkillUpgradeStatSnapshot();
        Expect(!PassiveUpgradeGate.IsRuleEnabled(gatedRule, emptySnapshot),
            "A rule with a required upgrade id must stay disabled until the snapshot grants it.");
        Expect(!PassiveUpgradeGate.IsRuleEnabled(gatedRule, null),
            "A rule with a required upgrade id must stay disabled against a null snapshot.");

        SkillUpgradeNodeData node = Node("extra_drop", 1);
        node.grantedUpgradeIds.Add("passive.feno.bulletbag.extra_drop");
        var grantingSnapshot = new SkillUpgradeStatSnapshot();
        grantingSnapshot.AddNode(node);
        Expect(PassiveUpgradeGate.IsRuleEnabled(gatedRule, grantingSnapshot),
            "A rule must be enabled once the snapshot grants its required upgrade id.");

        var blankRule = new TriggeredPassiveRule { requiredUpgradeId = "" };
        Expect(PassiveUpgradeGate.IsRuleEnabled(blankRule, null),
            "A rule with no required upgrade id must always be enabled, even against a null snapshot.");
    }

    static void TestMixedKindSlotFailsValidation(List<ScriptableObject> assets)
    {
        CharacterStats stats = ScriptableObject.CreateInstance<CharacterStats>();
        assets.Add(stats);
        SkillGemDefinition activeSkill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        assets.Add(activeSkill);
        PassiveDefinition passive = ScriptableObject.CreateInstance<AlwaysOnPassiveDef>();
        assets.Add(passive);

        stats.skillSlots = new List<CharacterSkillLoadoutSlot>
        {
            new()
            {
                slotId = "slot.mixed",
                options = new List<CharacterSkillLoadoutOption>
                {
                    new() { optionId = "active", skillAsset = activeSkill },
                    new() { optionId = "passive", skillAsset = passive },
                },
            },
        };

        List<SkillUpgradeValidationIssue> issues = SkillUpgradeTreeValidator.ValidateCharacterLoadout(stats);
        Expect(issues.Any(issue =>
                issue.Severity == SkillUpgradeValidationSeverity.Error &&
                issue.Message.Contains("mixes active and passive", StringComparison.OrdinalIgnoreCase)),
            "Validator must reject a slot that mixes active and passive options.");
    }

    static void TestPassiveSlotMustBeLast(List<ScriptableObject> assets)
    {
        CharacterStats stats = ScriptableObject.CreateInstance<CharacterStats>();
        assets.Add(stats);
        SkillGemDefinition activeSkill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        assets.Add(activeSkill);
        PassiveDefinition passive = ScriptableObject.CreateInstance<AlwaysOnPassiveDef>();
        assets.Add(passive);

        stats.skillSlots = new List<CharacterSkillLoadoutSlot>
        {
            new()
            {
                slotId = "slot.passive",
                options = new List<CharacterSkillLoadoutOption> { new() { optionId = "passive", skillAsset = passive } },
            },
            new()
            {
                slotId = "slot.active",
                options = new List<CharacterSkillLoadoutOption> { new() { optionId = "active", skillAsset = activeSkill } },
            },
        };

        List<SkillUpgradeValidationIssue> issues = SkillUpgradeTreeValidator.ValidateCharacterLoadout(stats);
        Expect(issues.Any(issue =>
                issue.Severity == SkillUpgradeValidationSeverity.Error &&
                issue.Message.Contains("must be last", StringComparison.OrdinalIgnoreCase)),
            "Validator must reject an active slot that appears after a passive slot.");
    }

    static void TestPassiveDefinitionDeclaresTreeGrantedIds()
    {
        const string passivePath = "Assets/Data/Combat/Passives/Passive.Feno_ForgottenBulletBag.asset";
        var passive = AssetDatabase.LoadAssetAtPath<PassiveDefinition>(passivePath);
        Expect(passive != null, "Feno's ForgottenBulletBag passive asset must exist.");

        var declaredIds = new List<string>();
        passive.CollectUpgradeIds(declaredIds);
        Expect(declaredIds.Count > 0,
            "CustomPassiveDef.CollectUpgradeIds must forward the DropAmmoOnShotPassiveBehavior's declared upgrade id.");

        SkillUpgradeTreeDefinition tree = passive.UpgradeTree;
        Expect(tree != null, "Feno's ForgottenBulletBag passive must have an upgrade tree assigned.");

        var grantedIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < tree.nodes.Count; i++)
        {
            List<string> nodeGrants = tree.nodes[i]?.grantedUpgradeIds;
            if (nodeGrants == null)
                continue;

            for (int j = 0; j < nodeGrants.Count; j++)
                grantedIds.Add(nodeGrants[j]);
        }

        foreach (string id in declaredIds)
        {
            Expect(grantedIds.Contains(id),
                $"Declared upgrade id '{id}' must be granted by a node in the passive's tree.");
        }

        foreach (string id in grantedIds)
        {
            Expect(declaredIds.Contains(id),
                $"Tree-granted upgrade id '{id}' must be declared by the passive definition's CollectUpgradeIds.");
        }
    }

    static void TestPrerequisitesSharedPoolAndVariantIsolation(List<ScriptableObject> assets)
    {
        SkillUpgradeTreeDefinition firstTree = CreateTree(assets, "tree.first",
            Node("root", 1), Node("child", 2, "root"));
        SkillUpgradeTreeDefinition secondTree = CreateTree(assets, "tree.second", Node("other", 1));
        var data = InitializedData(4);
        var model = new ActiveSkillProgressModel(null, data, 10);

        Expect(!model.TryUnlock("slot.1", "variant.a", firstTree, "child", out _),
            "All prerequisite nodes must be unlocked first.");
        Expect(model.TryUnlock("slot.1", "variant.a", firstTree, "root", out string rootReason), rootReason);
        Equal(3, model.AvailablePoints, "Every variant must spend from the shared character pool.");
        Expect(model.TryUnlock("slot.2", "variant.b", secondTree, "other", out string otherReason), otherReason);
        Equal(2, model.AvailablePoints, "A different slot must use the same shared pool.");
        Expect(!model.IsUnlocked("slot.1", "variant.a", firstTree, "other", out _),
            "Progress from another variant must remain isolated.");
        Expect(model.TryUnlock("slot.1", "variant.a", firstTree, "child", out string childReason), childReason);
        Equal(0, model.AvailablePoints, "Child cost must be charged after its prerequisite is met.");
    }

    static void TestResetUsesPaidCost(List<ScriptableObject> assets)
    {
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.reset", Node("root", 3));
        var data = InitializedData(5);
        var model = new ActiveSkillProgressModel(null, data, 10);
        Expect(model.TryUnlock("slot", "variant", tree, "root", out string reason), reason);

        tree.nodes[0].cost = 99;
        Expect(model.ResetTree("slot", "variant", tree, out int refund), "Reset must clear an unlocked tree.");
        Equal(3, refund, "Reset must use the cost paid at unlock time, not the edited asset cost.");
        Equal(5, model.AvailablePoints, "Reset must fully restore the paid points.");
    }

    static void TestTreeMismatchRefundsRemovedNodes(List<ScriptableObject> assets)
    {
        SkillUpgradeTreeDefinition replacement = CreateTree(assets, "tree.new", Node("new", 1));
        var data = InitializedData(2);
        data.activeSkillTrees.Add(new CharacterSkillTreeProgressSaveData
        {
            slotId = "slot",
            optionId = "variant",
            treeId = "tree.old",
            unlockedNodes = new List<CharacterSkillUpgradeNodeSaveData>
            {
                new() { nodeId = "removed", paidCost = 4 },
            },
        });
        var model = new ActiveSkillProgressModel(null, data, 10);

        SkillUpgradeStatSnapshot snapshot = model.BuildSnapshot("slot", "variant", replacement, out bool changed);
        Expect(changed, "Changing tree ID must reconcile old progress.");
        Expect(snapshot.IsEmpty, "Nodes missing from the replacement tree must have no runtime effect.");
        Equal(6, model.AvailablePoints, "Tree replacement must refund the originally paid cost.");
        Equal("tree.new", data.activeSkillTrees[0].treeId, "Progress must move to the replacement tree ID.");
        Equal(0, data.activeSkillTrees[0].unlockedNodes.Count, "Replacement tree must start empty.");
    }

    static void TestRemovedNodeRefundsWithoutTreeIdChange(List<ScriptableObject> assets)
    {
        SkillUpgradeNodeData keptNode = Node("kept", 1);
        keptNode.statModifiers.Add(new StatModifier { stat = StatType.Damage, add = 5f, mul = 1f });
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.pruned", keptNode);
        var data = InitializedData(2);
        data.activeSkillTrees.Add(new CharacterSkillTreeProgressSaveData
        {
            slotId = "slot",
            optionId = "variant",
            treeId = "tree.pruned",
            unlockedNodes = new List<CharacterSkillUpgradeNodeSaveData>
            {
                new() { nodeId = "kept", paidCost = 1 },
                new() { nodeId = "deleted", paidCost = 4 },
            },
        });
        var model = new ActiveSkillProgressModel(null, data, 10);

        SkillUpgradeStatSnapshot snapshot = model.BuildSnapshot("slot", "variant", tree, out bool changed);
        Expect(changed,
            "A node absent from the asset (same treeId) must be pruned and reported as a change.");
        Expect(!snapshot.IsEmpty, "The surviving node must still contribute to the snapshot.");
        Equal(6, model.AvailablePoints,
            "The deleted node's paid cost must be refunded even though treeId did not change.");
        Equal(1, data.activeSkillTrees[0].unlockedNodes.Count,
            "Only the node still present in the asset should remain unlocked.");
        Equal("kept", data.activeSkillTrees[0].unlockedNodes[0].nodeId,
            "The remaining unlocked entry must be the node that still exists in the tree.");
    }

    static void TestFailedUnlockStillReportsReconciliation(List<ScriptableObject> assets)
    {
        SkillUpgradeNodeData gatedNode = Node("gated", 1);
        gatedNode.requiredCharacterLevel = 99;
        SkillUpgradeTreeDefinition replacement = CreateTree(assets, "tree.new", gatedNode);
        var data = InitializedData(0);
        data.activeSkillTrees.Add(new CharacterSkillTreeProgressSaveData
        {
            slotId = "slot",
            optionId = "variant",
            treeId = "tree.old",
            unlockedNodes = new List<CharacterSkillUpgradeNodeSaveData>
            {
                new() { nodeId = "stale", paidCost = 3 },
            },
        });
        var model = new ActiveSkillProgressModel(null, data, 1);

        bool result = model.TryUnlock("slot", "variant", replacement, "gated", out string reason, out bool changed);
        Expect(!result, "Unlock must fail once the reconciled progress still doesn't meet the level requirement.");
        Expect(changed,
            "CanUnlock reconciling stale tree progress (refunding the old tree's paid cost) must be reported " +
            "even though the unlock itself was rejected, so the caller persists the refund.");
        Equal(3, model.AvailablePoints, "The stale tree's paid cost must be refunded despite the failed unlock.");
        Expect(!string.IsNullOrEmpty(reason), "A failed unlock must still surface a readable reason.");
    }

    static void TestDeterministicStatStacking(List<ScriptableObject> assets)
    {
        SkillUpgradeNodeData first = Node("first", 1);
        first.statModifiers.Add(new StatModifier { stat = StatType.Damage, add = 10f, mul = 1.5f });
        SkillUpgradeNodeData second = Node("second", 1);
        second.statModifiers.Add(new StatModifier { stat = StatType.Damage, add = 5f, mul = 2f });
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.stats", first, second);
        var data = InitializedData(2);
        var model = new ActiveSkillProgressModel(null, data, 10);
        Expect(model.TryUnlock("slot", "variant", tree, "first", out string firstReason), firstReason);
        Expect(model.TryUnlock("slot", "variant", tree, "second", out string secondReason), secondReason);

        SkillUpgradeStatSnapshot snapshot = model.BuildSnapshot("slot", "variant", tree, out _);
        var stats = new FinalSkillStats { damage = 100f };
        snapshot.Apply(stats);
        Approximately(345f, stats.damage, "Stat stacking must be (base + sum(add)) * product(mul).");
    }

    static void TestGrantedUpgradeIdsSnapshotAggregation(List<ScriptableObject> assets)
    {
        SkillUpgradeNodeData first = Node("granter.first", 1);
        first.grantedUpgradeIds.Add("aires3.self_guard");
        SkillUpgradeNodeData second = Node("granter.second", 1, "granter.first");
        second.grantedUpgradeIds.Add("aires3.unshaken");
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.granted-ids", first, second);
        var data = InitializedData(2);
        var model = new ActiveSkillProgressModel(null, data, 10);
        Expect(model.TryUnlock("slot", "variant", tree, "granter.first", out string firstReason), firstReason);
        Expect(model.TryUnlock("slot", "variant", tree, "granter.second", out string secondReason), secondReason);

        SkillUpgradeStatSnapshot snapshot = model.BuildSnapshot("slot", "variant", tree, out _);
        Expect(snapshot.HasUpgrade("aires3.self_guard"),
            "Snapshot must aggregate upgrade ids granted by an unlocked node.");
        Expect(snapshot.HasUpgrade("aires3.unshaken"),
            "Snapshot must aggregate upgrade ids from every unlocked node, not just the first.");
        Expect(!snapshot.HasUpgrade("aires3.ally_support"),
            "Snapshot must not report an upgrade id that was never granted.");
    }

    static void TestUnsupportedStatAggregatesWithoutAffectingActiveSkillOutput()
    {
        var snapshot = new SkillUpgradeStatSnapshot();
        SkillUpgradeNodeData node = Node("armor_node", 1);
        node.statModifiers.Add(new StatModifier { stat = StatType.Armor, add = 50f, mul = 2f });
        snapshot.AddNode(node);

        Expect(snapshot.TryGetAggregate(StatType.Armor, out float add, out float multiply),
            "AddNode must aggregate a stat outside the FinalSkillStats whitelist so passive rules/behaviors can read it.");
        Approximately(50f, add, "Aggregate add must match the node's authored value.");
        Approximately(2f, multiply, "Aggregate multiply must match the node's authored value.");

        var stats = new FinalSkillStats { damage = 100f };
        snapshot.Apply(stats);
        Approximately(100f, stats.damage,
            "A stat outside Apply(FinalSkillStats)'s whitelist must remain a no-op for active-skill output.");
    }

    static void TestMutuallyExclusiveNodesRejectCanUnlock(List<ScriptableObject> assets)
    {
        SkillUpgradeNodeData gate = Node("gate", 1);
        SkillUpgradeNodeData branchA = Node("branch.a", 1, "gate");
        branchA.mutuallyExclusiveNodeIds.Add("branch.b");
        SkillUpgradeNodeData branchB = Node("branch.b", 1, "gate");
        branchB.mutuallyExclusiveNodeIds.Add("branch.a");
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.exclusive", gate, branchA, branchB);
        var data = InitializedData(3);
        var model = new ActiveSkillProgressModel(null, data, 10);
        Expect(model.TryUnlock("slot", "variant", tree, "gate", out string gateReason), gateReason);
        Expect(model.TryUnlock("slot", "variant", tree, "branch.a", out string branchAReason), branchAReason);

        Expect(!model.CanUnlock("slot", "variant", tree, "branch.b", out string reason, out _),
            "A node must be rejected once a mutually exclusive sibling is already unlocked.");
        Expect(!string.IsNullOrEmpty(reason), "Rejection must surface a readable reason for the detail panel.");
    }

    static void TestOneWayExclusionStillBlocksBothOrders(List<ScriptableObject> assets)
    {
        // branch.a declares the exclusion; branch.b does not declare it back. The validator
        // would flag this as an authoring error, but CanUnlock must not depend on authors
        // remembering to mirror the list -- the outcome must not depend on unlock order.
        SkillUpgradeNodeData gate = Node("gate", 1);
        SkillUpgradeNodeData branchA = Node("branch.a", 1, "gate");
        branchA.mutuallyExclusiveNodeIds.Add("branch.b");
        SkillUpgradeNodeData branchB = Node("branch.b", 1, "gate");
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.one-way-exclusive", gate, branchA, branchB);

        var dataAFirst = InitializedData(3);
        var modelAFirst = new ActiveSkillProgressModel(null, dataAFirst, 10);
        Expect(modelAFirst.TryUnlock("slot", "variant", tree, "gate", out string gateReasonA), gateReasonA);
        Expect(modelAFirst.TryUnlock("slot", "variant", tree, "branch.a", out string branchAReason), branchAReason);
        Expect(!modelAFirst.CanUnlock("slot", "variant", tree, "branch.b", out _, out _),
            "The declaring node's own list must still block the reverse pick.");

        var dataBFirst = InitializedData(3);
        var modelBFirst = new ActiveSkillProgressModel(null, dataBFirst, 10);
        Expect(modelBFirst.TryUnlock("slot", "variant", tree, "gate", out string gateReasonB), gateReasonB);
        Expect(modelBFirst.TryUnlock("slot", "variant", tree, "branch.b", out string branchBReason), branchBReason);
        Expect(!modelBFirst.CanUnlock("slot", "variant", tree, "branch.a", out _, out _),
            "Unlocking the node whose exclusion list is empty first must not bypass a one-way exclusion " +
            "declared on the other node.");
    }

    static void TestEffectDurationAndHealPowerStatStacking()
    {
        var snapshot = new SkillUpgradeStatSnapshot();
        SkillUpgradeNodeData durationNode = Node("duration", 1);
        durationNode.statModifiers.Add(new StatModifier { stat = StatType.EffectDuration, add = 2f, mul = 1.3f });
        SkillUpgradeNodeData healNode = Node("heal", 1);
        healNode.statModifiers.Add(new StatModifier { stat = StatType.HealPower, add = 5f, mul = 1.4f });
        snapshot.AddNode(durationNode);
        snapshot.AddNode(healNode);

        var stats = new FinalSkillStats { effectDuration = 10f, healPower = 20f };
        snapshot.Apply(stats);
        Approximately(15.6f, stats.effectDuration, "EffectDuration must apply as (base + add) * mul.");
        Approximately(35f, stats.healPower, "HealPower must apply as (base + add) * mul.");
    }

    static void TestProjectileCountRoundingIsConsistent()
    {
        // Mathf.RoundToInt is banker's rounding: 1.5 -> 2 but 2.5 -> 2. A x1.5 modifier must not
        // grant a projectile on an odd base count while granting nothing on an even one.
        SkillUpgradeNodeData node = Node("projectiles", 1);
        node.statModifiers.Add(new StatModifier { stat = StatType.ProjectileCount, add = 0f, mul = 1.5f });
        var snapshot = new SkillUpgradeStatSnapshot();
        snapshot.AddNode(node);

        var oneProjectile = new FinalSkillStats { projectileCount = 1 };
        snapshot.Apply(oneProjectile);
        Equal(2, oneProjectile.projectileCount, "1 projectile at x1.5 must round up to 2, not stay at 1.");

        var twoProjectiles = new FinalSkillStats { projectileCount = 2 };
        snapshot.Apply(twoProjectiles);
        Equal(3, twoProjectiles.projectileCount, "2 projectiles at x1.5 must round up to 3, not stay at 2.");
    }

    static void TestSkillTreeDefaultAndVariantOverride(List<ScriptableObject> assets)
    {
        SkillUpgradeTreeDefinition defaultTree = CreateTree(assets, "tree.default", Node("default", 1));
        SkillUpgradeTreeDefinition overrideTree = CreateTree(assets, "tree.override", Node("override", 1));
        SkillGemDefinition skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        skill.upgradeTree = defaultTree;
        assets.Add(skill);

        var option = new CharacterSkillLoadoutOption { skillAsset = skill };
        Equal(defaultTree, option.ResolvedUpgradeTree,
            "A variant without an override must use its Skill Asset tree.");

        option.upgradeTreeOverride = overrideTree;
        Equal(overrideTree, option.ResolvedUpgradeTree,
            "A variant override must take precedence over the Skill Asset tree.");
    }

    static void TestVisualScaleMetrics()
    {
        SkillUpgradeNodeData node = Node("scale", 1);
        Approximately(1f, node.ResolvedVisualScale, "A new node must use the normal visual scale.");
        Approximately(96f, node.ResolvedRuntimeSize, "Normal visual scale must resolve to 96 pixels.");

        node.visualScale = 1.5f;
        Approximately(1.5f, node.ResolvedVisualScale, "An authored visual scale must be preserved.");
        Approximately(144f, node.ResolvedRuntimeSize, "Runtime size must use the authored visual scale.");

        node.visualScale = 3f;
        Approximately(2f, node.ResolvedVisualScale, "Visual scale must clamp to the maximum.");
        Approximately(192f, node.ResolvedRuntimeSize, "Maximum visual scale must resolve to 192 pixels.");

        node.visualScale = float.NaN;
        Approximately(1f, node.ResolvedVisualScale, "A non-finite visual scale must fall back to normal.");
    }

    static void TestRuntimeNodeVisualScaleAndFrameFallback()
    {
        var root = new GameObject(
            "VisualScaleTestNode",
            typeof(RectTransform),
            typeof(UnityEngine.UI.Image),
            typeof(ActiveSkillUpgradeNodeView));
        var texture = new Texture2D(2, 2);
        Sprite normalFrame = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
        Sprite importantFrame = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
        SkillScreenTheme theme = ScriptableObject.CreateInstance<SkillScreenTheme>();
        try
        {
            ActiveSkillUpgradeNodeView view = root.GetComponent<ActiveSkillUpgradeNodeView>();
            UnityEngine.UI.Image frame = root.GetComponent<UnityEngine.UI.Image>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("frame").objectReferenceValue = frame;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            theme.nodeFrame = normalFrame;
            theme.importantNodeFrame = importantFrame;
            SkillUpgradeNodeData node = Node("visual", 1);
            node.visualScale = 1.5f;

            view.Bind(node, ActiveSkillNodeVisualState.Locked, false, theme, null);
            Equal(new Vector2(144f, 144f), root.GetComponent<RectTransform>().sizeDelta,
                "Runtime node RectTransform must follow visual scale.");
            Equal(importantFrame, frame.sprite,
                "A scaled node must use the important frame when one is assigned.");

            theme.importantNodeFrame = null;
            view.Bind(node, ActiveSkillNodeVisualState.Locked, false, theme, null);
            Equal(normalFrame, frame.sprite,
                "A scaled node must fall back to the normal frame.");

            node.visualScale = 1f;
            theme.importantNodeFrame = importantFrame;
            view.Bind(node, ActiveSkillNodeVisualState.Locked, false, theme, null);
            Equal(normalFrame, frame.sprite,
                "A normal-sized node must keep the normal frame.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(theme);
            UnityEngine.Object.DestroyImmediate(normalFrame);
            UnityEngine.Object.DestroyImmediate(importantFrame);
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void TestGraphAllowsOneUnlocksPortToReachMultipleRequiresPorts(List<ScriptableObject> assets)
    {
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.graph",
            Node("root", 1), Node("first", 1, "root"), Node("second", 1));
        var graph = new ActiveSkillTreeEditorWindow.SkillTreeGraphView();
        graph.Load(tree);

        List<Port> allPorts = graph.ports.ToList();
        Port rootOutput = FindPort(allPorts, "root", Direction.Output);
        Port firstInput = FindPort(allPorts, "first", Direction.Input);
        Port secondInput = FindPort(allPorts, "second", Direction.Input);
        List<Port> compatible = graph.GetCompatiblePorts(rootOutput, null);

        Expect(!compatible.Contains(firstInput),
            "The editor must not offer a duplicate connection to the same Requires port.");
        Expect(compatible.Contains(secondInput),
            "A connected Unlocks port must remain available for another Requires port.");
    }

    static void TestGraphDisplaysAndRefreshesNodeIcon(List<ScriptableObject> assets)
    {
        var texture = new Texture2D(2, 2);
        Sprite icon = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
        try
        {
            SkillUpgradeNodeData node = Node("icon", 1);
            node.icon = icon;
            SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.icon", node);
            var graph = new ActiveSkillTreeEditorWindow.SkillTreeGraphView();
            graph.Load(tree);

            Image image = graph.Q<Image>("skill-node-icon");
            Expect(image != null, "The graph node must contain an icon element.");
            Equal(icon, image.sprite, "The graph node must display its authored Sprite.");

            node.icon = null;
            graph.RefreshTitles();
            Equal(null, image.sprite, "The graph node icon must refresh after Inspector changes.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(icon);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    static void TestGraphVisualScaleUsesCenteredPosition(List<ScriptableObject> assets)
    {
        SkillUpgradeNodeData node = Node("scaled", 1);
        node.uiPosition = new Vector2(180f, 90f);
        node.visualScale = 1.5f;
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.scaled-graph", node);
        var graph = new ActiveSkillTreeEditorWindow.SkillTreeGraphView();
        graph.Load(tree);

        Node graphNode = graph.nodes.Single();
        Rect rect = GetAuthoredRect(graphNode);
        Equal(node.uiPosition, rect.center,
            "Graph node position must treat uiPosition as the node center.");
        Equal(new Vector2(330f, 180f), rect.size,
            "Graph node size must use the authored visual scale.");

        node.visualScale = 2f;
        graph.RefreshTitles();
        rect = GetAuthoredRect(graphNode);
        Equal(node.uiPosition, rect.center,
            "Changing visual scale must preserve the graph node center.");
        Equal(new Vector2(440f, 240f), rect.size,
            "Graph node size must refresh after Inspector changes.");

        graphNode.SetPosition(new Rect(new Vector2(40f, 60f), rect.size));
        graph.HandleGraphChange(new GraphViewChange
        {
            movedElements = new List<GraphElement> { graphNode },
        });
        Equal(GetAuthoredRect(graphNode).center, node.uiPosition,
            "Moving a graph node must save its center position.");

        // A real drag hands SetPosition a rect sized from the node's *resolved* UIToolkit
        // layout (title length, icon, ports), not the authored BaseSize * visualScale. Feeding
        // a mismatched size here reproduces that and must not corrupt uiPosition.
        var resolvedSizeRect = new Rect(new Vector2(100f, 120f), new Vector2(311f, 148f));
        graphNode.SetPosition(resolvedSizeRect);
        graph.HandleGraphChange(new GraphViewChange
        {
            movedElements = new List<GraphElement> { graphNode },
        });
        Vector2 expectedCenter = resolvedSizeRect.position + new Vector2(440f, 240f) * 0.5f;
        Equal(expectedCenter, node.uiPosition,
            "Write-back must re-derive the center using the authored size, not the rect's own resolved size.");

        graph.RefreshTitles();
        Equal(resolvedSizeRect.position, GetAuthoredRect(graphNode).position,
            "Refreshing layout right after a move must not shift the node again.");
    }

    static Port FindPort(List<Port> ports, string nodeTitle, Direction direction)
    {
        Port port = ports.FirstOrDefault(candidate =>
            candidate.direction == direction && candidate.node?.title == nodeTitle);
        if (port == null)
            throw new InvalidOperationException($"Missing {direction} port for node '{nodeTitle}'.");
        return port;
    }

    static void TestUpgradeUiSkillTreeButtonWiring()
    {
        const string prefabPath = "Assets/Prefab/User Interface/UpgradUI.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Expect(prefab != null, "UpgradUI prefab must exist.");

        UILoadLaval controller = prefab.GetComponent<UILoadLaval>();
        Expect(controller != null, "UpgradUI prefab must contain UILoadLaval.");

        UnityEngine.UI.Button button = prefab.GetComponentsInChildren<UnityEngine.UI.Button>(true)
            .FirstOrDefault(candidate => candidate.name == "Skill_Tree_Button");
        Expect(button != null, "UpgradUI prefab must contain Skill_Tree_Button.");
        Equal(1, button.onClick.GetPersistentEventCount(),
            "Skill_Tree_Button must have one persistent listener.");
        Expect(button.onClick.GetPersistentTarget(0) == controller,
            "Skill_Tree_Button must target its UILoadLaval controller.");
        Equal(nameof(UILoadLaval.OpenActiveSkillTree), button.onClick.GetPersistentMethodName(0),
            "Skill_Tree_Button must open the Active Skill Tree.");

        var serializedController = new SerializedObject(controller);
        SerializedProperty screenPrefab = serializedController.FindProperty("activeSkillScreenPrefab");
        Expect(screenPrefab?.objectReferenceValue is ActiveSkillScreenController,
            "UpgradUI must reference the ActiveSkillScreen prefab.");

        var activeSkillScreen = screenPrefab.objectReferenceValue as ActiveSkillScreenController;
        Equal(Vector3.one, activeSkillScreen.transform.localScale,
            "ActiveSkillScreen prefab root scale must remain visible when instantiated.");
        Canvas screenCanvas = activeSkillScreen.GetComponent<Canvas>();
        Expect(screenCanvas != null && screenCanvas.sortingOrder >= 100,
            "ActiveSkillScreen must render above the lobby UI.");

        var serializedActiveSkillScreen = new SerializedObject(activeSkillScreen);
        var fitTreeButton = serializedActiveSkillScreen.FindProperty("fitTreeButton")?.objectReferenceValue
            as UnityEngine.UI.Button;
        Expect(fitTreeButton != null && fitTreeButton.name == "FitTreeButton",
            "ActiveSkillScreen must wire the FIT TREE button.");

        GameObject tooltipRoot =
            serializedActiveSkillScreen.FindProperty("treeNavigationTooltip")?.objectReferenceValue as GameObject;
        Expect(tooltipRoot != null && !tooltipRoot.activeSelf,
            "The tree navigation tooltip must start hidden.");
        Expect(tooltipRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)
                .All(graphic => !graphic.raycastTarget),
            "The tree navigation tooltip must not block pointer input.");
        EventTrigger fitTreeTrigger = fitTreeButton.GetComponent<EventTrigger>();
        Expect(fitTreeTrigger != null &&
                fitTreeTrigger.triggers.Any(entry => entry.eventID == EventTriggerType.PointerEnter) &&
                fitTreeTrigger.triggers.Any(entry => entry.eventID == EventTriggerType.PointerExit) &&
                fitTreeTrigger.triggers.Any(entry => entry.eventID == EventTriggerType.Scroll),
            "FIT TREE must show/hide its tooltip and forward wheel zoom to the tree.");

        ActiveSkillTreeView treeView = activeSkillScreen.GetComponentInChildren<ActiveSkillTreeView>(true);
        Expect(treeView != null, "ActiveSkillScreen must contain ActiveSkillTreeView.");
        var serializedTreeView = new SerializedObject(treeView);
        Equal(2f, serializedTreeView.FindProperty("maxZoomScale").floatValue,
            "Tree view max zoom must default to 2.0.");
        Equal(1.1f, serializedTreeView.FindProperty("zoomFactorPerNotch").floatValue,
            "Tree view zoom factor must default to 1.1 per wheel notch.");
        Expect(treeView.GetComponent<UnityEngine.UI.ScrollRect>() == null,
            "ActiveSkillTreeView must not use ScrollRect for pan and zoom.");

        ActiveSkillNodeDetailPanel detailPanel =
            activeSkillScreen.GetComponentInChildren<ActiveSkillNodeDetailPanel>(true);
        Expect(detailPanel != null && detailPanel.gameObject.activeSelf,
            "The node detail drawer must remain active so it can animate on screen.");
        var serializedDetailPanel = new SerializedObject(detailPanel);
        Equal(treeView, serializedDetailPanel.FindProperty("treeView")?.objectReferenceValue,
            "The detail drawer must notify the tree view while resizing its viewport.");
        Equal(treeView.transform as RectTransform,
            serializedDetailPanel.FindProperty("treeViewport")?.objectReferenceValue,
            "The detail drawer must resize the active tree viewport.");
        var closeButton = serializedDetailPanel.FindProperty("closeButton")?.objectReferenceValue
            as UnityEngine.UI.Button;
        Expect(closeButton != null && closeButton.name == "CloseButton",
            "The node detail drawer must expose a close button.");
        Approximately(440f, serializedDetailPanel.FindProperty("drawerWidth").floatValue,
            "The node detail drawer must reserve 440 logical pixels.");
        Approximately(0.2f, serializedDetailPanel.FindProperty("transitionDuration").floatValue,
            "The node detail drawer transition must take 0.2 seconds.");
    }

    static void TestTreeNavigationMath()
    {
        MethodInfo zoomMethod = typeof(ActiveSkillTreeView).GetMethod(
            "CalculateZoomedPosition",
            BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo clampMethod = typeof(ActiveSkillTreeView).GetMethod(
            "CalculateClampedPan",
            BindingFlags.NonPublic | BindingFlags.Static);
        Expect(zoomMethod != null && clampMethod != null,
            "ActiveSkillTreeView navigation math helpers must remain testable.");

        Vector2 currentPosition = new(30f, -10f);
        Vector2 pointer = new(100f, 50f);
        const float oldScale = 1f;
        const float newScale = 1.5f;
        Vector2 contentPoint = (pointer - currentPosition) / oldScale;
        Vector2 zoomedPosition = (Vector2)zoomMethod.Invoke(
            null,
            new object[] { currentPosition, pointer, oldScale, newScale });
        Vector2 pointerAfterZoom = zoomedPosition + contentPoint * newScale;
        Approximately(pointer.x, pointerAfterZoom.x,
            "Zooming must keep the horizontal content point under the pointer.");
        Approximately(pointer.y, pointerAfterZoom.y,
            "Zooming must keep the vertical content point under the pointer.");

        Vector2 centered = (Vector2)clampMethod.Invoke(
            null,
            new object[] { new Vector2(50f, -50f), new Vector2(500f, 300f), new Vector2(800f, 600f), 1f });
        Equal(Vector2.zero, centered,
            "Graph axes smaller than the viewport must remain centered.");

        Vector2 clamped = (Vector2)clampMethod.Invoke(
            null,
            new object[] { new Vector2(500f, -500f), new Vector2(1000f, 900f), new Vector2(800f, 600f), 1f });
        Equal(new Vector2(100f, -150f), clamped,
            "Pan must hard-clamp to the scaled graph bounds.");
    }

    static void TestVisualScaleValidationAndBounds(List<ScriptableObject> assets)
    {
        SkillUpgradeNodeData first = Node("first", 1);
        SkillUpgradeNodeData second = Node("second", 1);
        first.uiPosition = Vector2.zero;
        second.uiPosition = new Vector2(80f, 0f);
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.visual-validation", first, second);

        first.visualScale = 0.5f;
        List<SkillUpgradeValidationIssue> issues = SkillUpgradeTreeValidator.Validate(tree);
        Expect(issues.Any(issue =>
                issue.Severity == SkillUpgradeValidationSeverity.Error &&
                issue.Message.Contains("visual scale", StringComparison.OrdinalIgnoreCase)),
            "Validator must reject an authored visual scale outside the supported range.");
        Expect(issues.Any(issue =>
                issue.Severity == SkillUpgradeValidationSeverity.Warning &&
                issue.Message.Contains("overlap", StringComparison.OrdinalIgnoreCase)),
            "Validator must warn when runtime node bounds overlap.");

        first.visualScale = 2f;
        second.visualScale = 2f;
        first.uiPosition = new Vector2(0f, -420f);
        second.uiPosition = new Vector2(0f, 420f);
        issues = SkillUpgradeTreeValidator.Validate(tree);
        Expect(!issues.Any(issue => issue.Message.Contains("overlap", StringComparison.OrdinalIgnoreCase)),
            "Separated runtime node bounds must not report an overlap.");
        Expect(issues.Any(issue =>
                issue.Severity == SkillUpgradeValidationSeverity.Warning &&
                issue.Message.Contains("auto-fit scale", StringComparison.OrdinalIgnoreCase)),
            "Readable bounds validation must include scaled node extents.");
    }

    static void TestValidationIssuesCarryOwningNodeId(List<ScriptableObject> assets)
    {
        SkillUpgradeNodeData child = Node("issue.child", 1, "issue.ghost");
        child.statModifiers.Add(new StatModifier { stat = StatType.Armor, add = 1f });
        child.grantedUpgradeIds.Add("   ");
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.node-ids", child);

        List<SkillUpgradeValidationIssue> issues = SkillUpgradeTreeValidator.Validate(
            tree, new List<SkillDefinitionBase>());

        Expect(issues.Any(issue =>
                issue.Severity == SkillUpgradeValidationSeverity.Error &&
                issue.BelongsTo("issue.child")),
            "A missing prerequisite must be reported against the node that declares it.");
        Expect(!issues.Any(issue => issue.BelongsTo("issue.ghost")),
            "The missing prerequisite id must not receive issues of its own -- it is not a node.");
        Expect(issues.Any(issue =>
                issue.BelongsTo("issue.child") && issue.Message.Contains("blank granted upgrade id")),
            "A blank granted upgrade id must be attributed to its node, not left tree-level.");

        // Both directions: a node-scoped issue names its own node, and an issue that opens with
        // "Node '<id>'" is never left unattributed -- that is how the badge loses a real error.
        Expect(issues.All(issue => issue.NodeId == null || issue.Message.Contains($"'{issue.NodeId}'")),
            "Every node-scoped issue must name its own node in the message.");
        Expect(issues.All(issue => !issue.Message.StartsWith("Node '", StringComparison.Ordinal) ||
                                   issue.NodeId != null),
            "An issue that opens by naming a node must carry that node's id.");
    }

    static void TestGrantSeveritiesAndCrossNodeDuplicates(List<ScriptableObject> assets)
    {
        var owner = ScriptableObject.CreateInstance<TriggeredPassiveDef>();
        owner.rules.Add(new TriggeredPassiveRule { requiredUpgradeId = "grant.declared" });
        assets.Add(owner);
        var owners = new List<SkillDefinitionBase> { owner };

        SkillUpgradeNodeData first = Node("grant.first", 1);
        first.grantedUpgradeIds.Add("grant.extra");
        SkillUpgradeNodeData second = Node("grant.second", 1);
        second.grantedUpgradeIds.Add("grant.extra");
        first.uiPosition = new Vector2(0f, -400f);
        second.uiPosition = new Vector2(0f, 400f);
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.grant-severity", first, second);

        List<SkillUpgradeValidationIssue> issues = SkillUpgradeTreeValidator.Validate(tree, owners);

        Expect(issues.Any(issue =>
                issue.Severity == SkillUpgradeValidationSeverity.Warning &&
                issue.BelongsTo("grant.first") &&
                issue.Message.Contains("no owning skill declares")),
            "An id nothing consumes must be a warning, not an error -- it is the normal WIP state.");
        Expect(issues.Any(issue =>
                issue.Severity == SkillUpgradeValidationSeverity.Error &&
                issue.NodeId == null &&
                issue.Message.Contains("grant.declared")),
            "A declared id no node grants must be a tree-level error -- that feature is unreachable.");
        Expect(issues.Count(issue => issue.Message.Contains("also grants")) == 2,
            "A duplicated grant must be reported once against each of the two granting nodes.");

        first.mutuallyExclusiveNodeIds.Add("grant.second");
        second.mutuallyExclusiveNodeIds.Add("grant.first");
        issues = SkillUpgradeTreeValidator.Validate(tree, owners);
        Expect(!issues.Any(issue => issue.Message.Contains("also grants")),
            "Mutually exclusive nodes may grant the same id -- that is how a branch choice works.");
    }

    static void TestUsageScannerReadsPassiveRuleSites(List<ScriptableObject> assets)
    {
        var passive = ScriptableObject.CreateInstance<TriggeredPassiveDef>();
        var rule = new TriggeredPassiveRule { requiredUpgradeId = "scan.gate" };
        rule.upgradeOverrides.Add(new PassiveRuleFieldOverride { upgradeId = "scan.tuning" });
        passive.rules.Add(rule);
        assets.Add(passive);

        Dictionary<string, List<UpgradeIdUsage>> usages =
            UpgradeIdUsageScanner.Scan(new List<SkillDefinitionBase> { passive });

        Expect(usages.ContainsKey("scan.gate"), "The scanner must find a passive rule's requiredUpgradeId.");
        Expect(usages.ContainsKey("scan.tuning"),
            "The scanner must find a numeric override's upgradeId inside upgradeOverrides.");
        Equal(-1, usages["scan.gate"][0].StepIndex, "A passive rule site belongs to no composite step.");
        Expect(usages["scan.gate"][0].PropertyPath.EndsWith("requiredUpgradeId", StringComparison.Ordinal),
            "The usage must record the exact property path so navigation can reach it.");
    }

    // Runs against the real skill because the interesting cases -- embedded payload sub-assets and
    // a step gate sitting beside a conditional application -- only exist once the asset is saved,
    // and an in-memory SkillGemDefinition has no sub-assets to walk.
    static void TestUsageScannerResolvesSkillStatusSites()
    {
        const string skillPath = "Assets/Data/Skills/Aires/Aires_Skill_3.asset";
        var skill = AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(skillPath);
        Expect(skill != null, $"'{skillPath}' must exist for the usage scanner smoke test.");

        Dictionary<string, List<UpgradeIdUsage>> usages =
            UpgradeIdUsageScanner.Scan(new List<SkillDefinitionBase> { skill });

        Expect(usages.TryGetValue("aires3.self_guard", out List<UpgradeIdUsage> selfGuard),
            "The scanner must reach conditional applications inside an embedded payload sub-asset.");
        Equal(2, selfGuard.Count,
            "One upgrade id used by two conditional applications must produce two separate usages.");
        for (int i = 0; i < selfGuard.Count; i++)
        {
            Expect(selfGuard[i].Summary.EndsWith(" to Self", StringComparison.Ordinal),
                "An Apply Status payload must identify Self as its application target.");
            Expect(selfGuard[i].Details.Count > 0,
                "A status application must resolve at least a duration line.");
            Expect(selfGuard[i].StepIndex >= 0,
                "A payload wrapped by a PayloadStep must resolve back to that step for navigation.");
            Expect(selfGuard[i].Owner is ApplyStatusSkillPayloadDef,
                "A conditional status inside Apply Status must keep the embedded payload as its edit owner.");
            Equal($"Configured In: Apply Status Payload (Step {selfGuard[i].StepIndex})", selfGuard[i].SourceLabel,
                "A payload usage source must name the payload first and put the step index in parentheses.");
            Expect(!ActiveSkillTreeEditorWindow.CanEditUsageInSkillSteps(selfGuard[i]),
                "An Apply Status payload usage must open its payload Inspector, not the wrapper Skill Step.");
        }

        Expect(usages.TryGetValue("aires3.ally_support", out List<UpgradeIdUsage> allySupport),
            "The scanner must find a step's own requiredUpgradeId gate.");
        Equal(1, allySupport.Count, "A bare step gate must resolve to exactly one usage.");
        Equal("Heal nearby Allies", allySupport[0].Summary,
            "A Heal Area gate must describe its actual target mode instead of a generic 'Enable' fallback.");
        Expect(allySupport[0].ReadsHealPowerStat,
            "A Heal Area gate must flag that it reads the skill's Heal Power stat.");
        Expect(allySupport[0].ReadsAreaRadiusStat,
            "An Allies-mode Heal Area gate must flag that it reads the skill's Area Radius stat.");
        Expect(allySupport[0].Details.Any(detail => detail.Contains("Aires3_AllyGuard")),
            "A Heal Area gate must report the unconditional status it always applies once unlocked.");
        Expect(allySupport[0].Details.Any(detail => detail.Contains("Armor") && detail.Contains("+20")),
            "A Heal Area gate must report its unconditional status' modifiers.");
        Expect(allySupport[0].StepIndex >= 0, "A step gate must resolve its composite step index.");
        Expect(ActiveSkillTreeEditorWindow.CanEditUsageInSkillSteps(allySupport[0]),
            "A gate stored on the composite step itself must remain editable in Skill Steps.");

        Expect(usages.TryGetValue("aires3.thorns", out List<UpgradeIdUsage> thorns),
            "The scanner must find the conditional Aires3_Thorns application inside Apply Status.");
        Expect(thorns.Any(usage => usage.Details.Any(detail =>
                detail.Contains("On Take Damage") &&
                detail.Contains("+1 Stack") &&
                detail.Contains("Max 5"))),
            "A triggered status summary must show its trigger, granted stacks, and stack cap.");

        Expect(usages.TryGetValue("aires3.def_shred", out List<UpgradeIdUsage> defenseShred) &&
               defenseShred.All(usage => usage.Summary.EndsWith(" to Taunted Enemies", StringComparison.Ordinal)),
            "A Taunt payload conditional status must identify Taunted Enemies as its target.");
        Expect(defenseShred.All(usage =>
                usage.SourceLabel == $"Configured In: Taunt Payload (Step {usage.StepIndex})"),
            "A Taunt usage source must read as a configured location, not another gameplay effect.");
        Expect(usages.TryGetValue("aires3.ally_regen", out List<UpgradeIdUsage> allyRegen) &&
               allyRegen.All(usage => usage.Summary.EndsWith(" to Allies", StringComparison.Ordinal)),
            "A HealArea conditional status must identify the authored HealArea target.");
        Equal(1, allyRegen.Count,
            "A conditional status inside the Heal Area payload must remain a separate usage from the step's own gate.");
    }

    static void TestRequiredPathPreviewAggregatesPrerequisiteChain(List<ScriptableObject> assets)
    {
        var owner = ScriptableObject.CreateInstance<SkillGemDefinition>();
        owner.baseHealPower = 25f;
        owner.baseRadius = 30f;
        assets.Add(owner);

        SkillUpgradeNodeData root = Node("chain.root", 1);
        root.statModifiers.Add(new StatModifier { stat = StatType.HealPower, add = 10f, mul = 1f });

        SkillUpgradeNodeData mid = Node("chain.mid", 1, "chain.root");
        mid.statModifiers.Add(new StatModifier { stat = StatType.HealPower, add = 5f, mul = 1.2f });
        mid.statModifiers.Add(new StatModifier { stat = StatType.AreaRadius, add = 0f, mul = 1.25f });

        SkillUpgradeNodeData selected = Node("chain.selected", 1, "chain.mid");

        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.chain", root, mid, selected);

        FinalSkillStats preview = RequiredPathPreviewResolver.Resolve(tree, selected, owner);
        Expect(preview != null, "A node reachable from the tree root must resolve a preview.");
        // (25 + 10 + 5) * (1 * 1.2) = 48
        Approximately(48f, preview.healPower,
            "The preview must fold every prerequisite layer's Heal Power modifier, not just the direct parent.");
        Approximately(37.5f, preview.areaRadius,
            "The preview must fold a prerequisite layer's Area Radius multiplier too.");
    }

    static void TestRequiredPathPreviewZeroMultiplierZeroesLaterAdds(List<ScriptableObject> assets)
    {
        // Mirrors a real authoring bug: a trunk node with `mul: 0` silently zeroes every Heal Power
        // add from every node above it, because the runtime formula is (base + sum(add)) * product(mul).
        var owner = ScriptableObject.CreateInstance<SkillGemDefinition>();
        owner.baseHealPower = 25f;
        assets.Add(owner);

        SkillUpgradeNodeData trunkGate = Node("zero.trunk", 1);
        trunkGate.statModifiers.Add(new StatModifier { stat = StatType.HealPower, add = 100f, mul = 0f });

        SkillUpgradeNodeData selected = Node("zero.selected", 1, "zero.trunk");

        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.zero-mul", trunkGate, selected);

        FinalSkillStats preview = RequiredPathPreviewResolver.Resolve(tree, selected, owner);
        Approximately(0f, preview.healPower,
            "A prerequisite with mul: 0 must zero Heal Power in the preview, exactly like it does at runtime.");
    }

    static void TestRequiredPathPreviewExcludesOptionalNodes(List<ScriptableObject> assets)
    {
        var owner = ScriptableObject.CreateInstance<SkillGemDefinition>();
        owner.baseHealPower = 25f;
        assets.Add(owner);

        SkillUpgradeNodeData root = Node("optional.root", 1);
        SkillUpgradeNodeData selected = Node("optional.selected", 1, "optional.root");
        SkillUpgradeNodeData optionalSibling = Node("optional.sibling", 1, "optional.root");
        optionalSibling.statModifiers.Add(new StatModifier { stat = StatType.HealPower, add = 500f, mul = 1f });

        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.optional", root, selected, optionalSibling);

        FinalSkillStats preview = RequiredPathPreviewResolver.Resolve(tree, selected, owner);
        Approximately(25f, preview.healPower,
            "A sibling node that is not a prerequisite of the selected node must not affect its preview, " +
            "even though both share the same parent.");
    }

    static void TestRequiredPathPreviewIsSafeAgainstCyclesAndMissingPrerequisites(List<ScriptableObject> assets)
    {
        SkillUpgradeNodeData nodeA = Node("cycle.a", 1, "cycle.b", "cycle.missing");
        SkillUpgradeNodeData nodeB = Node("cycle.b", 1, "cycle.a");
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.cycle", nodeA, nodeB);

        var owner = ScriptableObject.CreateInstance<SkillGemDefinition>();
        owner.baseHealPower = 10f;
        assets.Add(owner);

        FinalSkillStats preview = RequiredPathPreviewResolver.Resolve(tree, nodeA, owner);
        Expect(preview != null,
            "A prerequisite cycle and a dangling prerequisite id must not stop the preview from resolving.");
    }

    // Runs against the real skill for the same reason TestUsageScannerResolvesSkillStatusSites does:
    // a route application only exists once the asset is saved.
    static void TestUnlockedAbilitiesHidesUsagesAlreadyOnStatusEffectsCard()
    {
        const string skillPath = "Assets/Data/Skills/Aires/Aires_Skill_3.asset";
        var skill = AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(skillPath);
        Expect(skill != null, $"'{skillPath}' must exist for this smoke test.");

        Dictionary<string, List<UpgradeIdUsage>> usages =
            UpgradeIdUsageScanner.Scan(new List<SkillDefinitionBase> { skill });

        Expect(usages.TryGetValue("aires3.self_guard", out List<UpgradeIdUsage> selfGuardUsages),
            "The scanner must still find the conditional applications gated by aires3.self_guard.");
        List<StatusEffectApplicationHandle> selfGuardHandles =
            ActiveSkillStatusEffectAuthoringService.Collect(skill, new List<string> { "aires3.self_guard" });
        Equal(2, selfGuardHandles.Count,
            "Both conditional applications gated by aires3.self_guard must resolve as status route handles.");
        for (int i = 0; i < selfGuardHandles.Count; i++)
        {
            Expect(selfGuardHandles[i].Details.Count > 0,
                "A status route handle must resolve the same modifier/duration lines the scanner reports.");
        }

        List<UpgradeIdUsage> remaining =
            ActiveSkillTreeEditorWindow.FilterNonStatusUsages(selfGuardUsages, selfGuardHandles);
        Equal(0, remaining.Count,
            "A granted id whose only usages are status route applications must have nothing left to " +
            "show under Unlocked Abilities once the Status Effects card already covers it.");

        Expect(usages.TryGetValue("aires3.ally_support", out List<UpgradeIdUsage> allySupportUsages),
            "The scanner must still find the Heal Area payload's own gate.");
        List<StatusEffectApplicationHandle> allySupportHandles =
            ActiveSkillStatusEffectAuthoringService.Collect(skill, new List<string> { "aires3.ally_support" });
        Equal(0, allySupportHandles.Count,
            "A bare step gate is not a status route application, so Collect must not resolve a handle for it.");

        List<UpgradeIdUsage> remainingAllySupport =
            ActiveSkillTreeEditorWindow.FilterNonStatusUsages(allySupportUsages, allySupportHandles);
        Equal(1, remainingAllySupport.Count,
            "A non-status behavior (a Heal Area gate) must stay visible in Unlocked Abilities even " +
            "though a different id on the same node might be a pure status gate.");
    }

    static CharacterProgressData InitializedData(int points)
    {
        return new CharacterProgressData
        {
            level = 10,
            skillPoints = points,
            skillProgressInitialized = true,
        };
    }

    static SkillUpgradeTreeDefinition CreateTree(
        List<ScriptableObject> assets,
        string treeId,
        params SkillUpgradeNodeData[] nodes)
    {
        SkillUpgradeTreeDefinition tree = ScriptableObject.CreateInstance<SkillUpgradeTreeDefinition>();
        tree.treeId = treeId;
        tree.nodes = new List<SkillUpgradeNodeData>(nodes);
        assets.Add(tree);
        return tree;
    }

    static SkillUpgradeNodeData Node(string nodeId, int cost, params string[] prerequisites)
    {
        return new SkillUpgradeNodeData
        {
            nodeId = nodeId,
            cost = cost,
            requiredCharacterLevel = 1,
            requiredNodeIds = new List<string>(prerequisites),
        };
    }

    static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
    }

    static void Approximately(float expected, float actual, string message)
    {
        if (!Mathf.Approximately(expected, actual))
            throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
    }

    static Rect GetAuthoredRect(VisualElement element)
    {
        if (element.userData is Rect authoredRect)
            return authoredRect;

        return new Rect(
            element.style.left.value.value,
            element.style.top.value.value,
            element.style.width.value.value,
            element.style.height.value.value);
    }
}
