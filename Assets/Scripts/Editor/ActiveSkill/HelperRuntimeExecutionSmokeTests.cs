#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Regression coverage for the boundary between the Helper loadout and live execution. These
/// tests intentionally inspect the cached runtime entry rather than constructing a throwaway
/// SkillInstance, because the entry owns the selected variant snapshot while its definition still
/// shares the character's charge pool.
/// </summary>
public sealed class HelperRuntimeExecutionSmokeTests
{
    readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
    }

    [Test]
    public void SwitchingProcVariantReplacesTheRuntimeSnapshotAndUpgradeId()
    {
        SkillUpgradeTreeDefinition firstTree = CreateTree("first.tree", "upgrade.first");
        SkillUpgradeTreeDefinition secondTree = CreateTree("second.tree", "upgrade.second");
        SkillHelperDef first = CreateProc("First", firstTree);
        SkillHelperDef second = CreateProc("Second", secondTree);

        CharacterStats stats = CreateHelperStats(first, second);
        GameObject actor = new GameObject("HelperRuntimeTestActor");
        createdObjects.Add(actor);
        AllyContext context = actor.AddComponent<AllyContext>();
        context.baseStats = stats;
        CharacterActiveSkillProgress progress = actor.AddComponent<CharacterActiveSkillProgress>();
        progress.ReloadFromSave();
        AddUnlockedNode(progress.Model.Data, CharacterSkillLoadoutKeys.HelperProcSlotKey(stats.helperProcSlots[0], 0),
            "proc.0.option.0", firstTree);
        AddUnlockedNode(progress.Model.Data, CharacterSkillLoadoutKeys.HelperProcSlotKey(stats.helperProcSlots[0], 0),
            "proc.0.option.1", secondTree);
        CharacterSkillManager manager = actor.AddComponent<CharacterSkillManager>();

        Assert.That(manager.TryGetHelperProcRuntimeSkill(first, out SkillInstance firstRuntime), Is.True);
        Assert.That(firstRuntime.upgradeSnapshot.HasUpgrade("upgrade.first"), Is.True);
        Assert.That(firstRuntime.upgradeSnapshot.HasUpgrade("upgrade.second"), Is.False);

        string slotId = CharacterSkillLoadoutKeys.HelperProcSlotKey(stats.helperProcSlots[0], 0);
        Assert.That(manager.TrySelectLoadoutOption(slotId, "proc.0.option.1", persist: false), Is.True);

        Assert.That(manager.TryGetHelperProcRuntimeSkill(second, out SkillInstance secondRuntime), Is.True);
        Assert.That(secondRuntime.upgradeSnapshot.HasUpgrade("upgrade.second"), Is.True);
        Assert.That(secondRuntime.upgradeSnapshot.HasUpgrade("upgrade.first"), Is.False);
    }

    [Test]
    public void DuplicateSelectedProcDefinitionsStillResolveOneSharedDefinitionEntry()
    {
        SkillUpgradeTreeDefinition tree = CreateTree("shared.tree", "upgrade.shared");
        SkillHelperDef proc = CreateProc("Shared", tree);
        CharacterStats stats = CreateHelperStats(proc, proc);
        GameObject actor = new GameObject("HelperSharedEntryTestActor");
        createdObjects.Add(actor);
        AllyContext context = actor.AddComponent<AllyContext>();
        context.baseStats = stats;
        CharacterActiveSkillProgress progress = actor.AddComponent<CharacterActiveSkillProgress>();
        progress.ReloadFromSave();
        AddUnlockedNode(progress.Model.Data, CharacterSkillLoadoutKeys.HelperProcSlotKey(stats.helperProcSlots[0], 0),
            "proc.0.option.0", tree);
        CharacterSkillManager manager = actor.AddComponent<CharacterSkillManager>();

        Assert.That(manager.TryGetHelperProcRuntimeSkill(proc, out SkillInstance runtimeSkill), Is.True);
        Assert.That(runtimeSkill, Is.Not.Null);
        Assert.That(runtimeSkill.upgradeSnapshot.HasUpgrade("upgrade.shared"), Is.True);
    }

    CharacterStats CreateHelperStats(params SkillHelperDef[] procs)
    {
        var stats = ScriptableObject.CreateInstance<CharacterStats>();
        stats.name = "HelperRuntimeTestStats";
        stats.partyRole = CharacterPartyRole.Helper;
        stats.helperProcSlots = new List<HelperProcLoadoutSlot>
        {
            new()
            {
                slotId = "proc.0",
                options = new List<HelperProcLoadoutOption>
                {
                    new() { optionId = "proc.0.option.0", helperProc = procs[0] },
                    new() { optionId = "proc.0.option.1", helperProc = procs[1] },
                },
            },
        };
        createdObjects.Add(stats);
        return stats;
    }

    SkillHelperDef CreateProc(string id, SkillUpgradeTreeDefinition tree)
    {
        var proc = ScriptableObject.CreateInstance<SkillHelperDef>();
        proc.name = id;
        proc.helperId = id;
        proc.executionSkill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        proc.executionSkill.name = id + " Execution";
        proc.executionSkill.upgradeTree = tree;
        createdObjects.Add(proc);
        createdObjects.Add(proc.executionSkill);
        return proc;
    }

    static void AddUnlockedNode(
        CharacterProgressData data,
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree)
    {
        data.skillProgressInitialized = true;
        data.activeSkillTrees.Add(new CharacterSkillTreeProgressSaveData
        {
            slotId = slotId,
            optionId = optionId,
            treeId = tree.RuntimeTreeId,
            unlockedNodes = new List<CharacterSkillUpgradeNodeSaveData>
            {
                new() { nodeId = "root", paidCost = 1 },
            },
        });
    }

    SkillUpgradeTreeDefinition CreateTree(string id, string upgradeId)
    {
        var tree = ScriptableObject.CreateInstance<SkillUpgradeTreeDefinition>();
        tree.name = id;
        tree.treeId = id;
        tree.nodes = new List<SkillUpgradeNodeData>
        {
            new()
            {
                nodeId = "root",
                cost = 1,
                requiredCharacterLevel = 1,
                grantedUpgradeIds = new List<string> { upgradeId },
            },
        };
        createdObjects.Add(tree);
        return tree;
    }
}
#endif
