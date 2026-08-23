#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Covers the Helper half of the Skill Loadout: slots with switchable variants, namespaced save
/// keys, per-variant Skill Tree progress drawn from one shared point pool, and the runtime
/// resolution that decides which proc is actually equipped.
///
/// Everything here is Edit Mode. The cast itself - animation acceptance, cast-point timing, the
/// visible cancellation of a running assist - needs a live Animancer driver and stays a Play Mode
/// check; what is pinned here is the loadout state every one of those paths reads.
/// </summary>
public sealed class HelperSkillLoadoutSmokeTests
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

    // ---- Descriptors ----------------------------------------------------------------------------

    [Test]
    public void HelperDescriptorsListTheCommandSlotBeforeItsProcSlots()
    {
        CharacterStats stats = CreateHelperStats(
            CreateSkill("Command"),
            CreateProc("Proc A"),
            CreateProc("Proc B"));

        List<SkillLoadoutSlotDescriptor> slots = SkillLoadoutDescriptorFactory.Build(stats);

        Assert.That(slots.Count, Is.EqualTo(3));
        Assert.That(slots[0].Kind, Is.EqualTo(SkillLoadoutKind.HelperCommand));
        Assert.That(slots[1].Kind, Is.EqualTo(SkillLoadoutKind.HelperProc));
        Assert.That(slots[2].Kind, Is.EqualTo(SkillLoadoutKind.HelperProc));
    }

    [Test]
    public void HelperWithNoCommandOptionGetsNoCommandTab()
    {
        CharacterStats stats = CreateHelperStats(null, CreateProc("Proc"));

        List<SkillLoadoutSlotDescriptor> slots = SkillLoadoutDescriptorFactory.Build(stats);

        Assert.That(slots.Count, Is.EqualTo(1),
            "A Helper with no manual command must not be offered an empty command tab.");
        Assert.That(slots[0].Kind, Is.EqualTo(SkillLoadoutKind.HelperProc));
    }

    [Test]
    public void HelperSlotKeysAreNamespacedByKind()
    {
        CharacterStats stats = CreateHelperStats(CreateSkill("Command"), CreateProc("Proc"));

        List<SkillLoadoutSlotDescriptor> slots = SkillLoadoutDescriptorFactory.Build(stats);

        Assert.That(slots[0].SlotId, Does.StartWith(CharacterSkillLoadoutKeys.HelperCommandPrefix));
        Assert.That(slots[1].SlotId, Does.StartWith(CharacterSkillLoadoutKeys.HelperProcPrefix));
    }

    [Test]
    public void ProcDescriptorTakesItsIconAndTreeFromTheExecutionSkill()
    {
        SkillHelperDef proc = CreateProc("Proc");
        proc.executionSkill.icon = CreateSprite();
        proc.executionSkill.upgradeTree = CreateTree("proc.tree");

        CharacterStats stats = CreateHelperStats(null, proc);
        SkillLoadoutOptionDescriptor option = SkillLoadoutDescriptorFactory.Build(stats)[0].Options[0];

        Assert.That(option.SkillAsset, Is.SameAs(proc.executionSkill));
        Assert.That(option.Icon, Is.SameAs(proc.executionSkill.icon));
        Assert.That(option.UpgradeTree, Is.SameAs(proc.executionSkill.upgradeTree));
        Assert.That(option.HelperProc, Is.SameAs(proc));
        Assert.That(option.TriggerSummary, Is.Not.Empty,
            "A proc card has to say what makes it fire.");
    }

    [Test]
    public void StrykerDescriptorsStillComeFromSkillSlots()
    {
        SkillGemDefinition skill = CreateSkill("Stryker Skill");
        var stats = ScriptableObject.CreateInstance<CharacterStats>();
        stats.name = "TestStrykerStats";
        stats.partyRole = CharacterPartyRole.Stryker;
        stats.skillSlots = new List<CharacterSkillLoadoutSlot>
        {
            new()
            {
                slotId = "skill.1",
                options = new List<CharacterSkillLoadoutOption>
                {
                    new() { optionId = "skill.1.a", skillAsset = skill },
                },
            },
        };
        createdObjects.Add(stats);

        List<SkillLoadoutSlotDescriptor> slots = SkillLoadoutDescriptorFactory.Build(stats);

        Assert.That(slots.Count, Is.EqualTo(1));
        Assert.That(slots[0].Kind, Is.EqualTo(SkillLoadoutKind.Stryker));
        Assert.That(slots[0].SlotId, Is.EqualTo("skill.1"),
            "Stryker slot keys must stay unprefixed so existing progress still resolves.");
        Assert.That(slots[0].Options[0].SkillAsset, Is.SameAs(skill));
    }

    // ---- Progress -------------------------------------------------------------------------------

    [Test]
    public void EachVariantKeepsItsOwnUnlocksFromOneSharedPointPool()
    {
        // One shared tree on purpose: what has to separate the two variants is the option id in
        // the save key, not the fact that they happen to point at different tree assets.
        SkillUpgradeTreeDefinition tree = CreateTree("tree.shared");
        ActiveSkillProgressModel model = CreateModel(points: 4);

        const string SlotId = CharacterSkillLoadoutKeys.HelperProcPrefix + "proc.0";

        Assert.That(model.TryUnlock(SlotId, "variant.a", tree, "root", out _, out _), Is.True);
        Assert.That(model.AvailablePoints, Is.EqualTo(3));

        Assert.That(model.IsUnlocked(SlotId, "variant.b", tree, "root", out _), Is.False,
            "A node unlocked in one variant must not appear unlocked in another.");

        Assert.That(model.TryUnlock(SlotId, "variant.b", tree, "root", out _, out _), Is.True,
            "Both variants spend from the same pool, so the second unlock is affordable.");
        Assert.That(model.AvailablePoints, Is.EqualTo(2),
            "Switching variants never refunds; each unlock costs its own point.");
        Assert.That(model.IsUnlocked(SlotId, "variant.a", tree, "root", out _), Is.True,
            "Progress in the variant that was switched away from must survive.");
    }

    [Test]
    public void ResetRefundsOnlyTheTreeItWasAskedFor()
    {
        SkillUpgradeTreeDefinition tree = CreateTree("tree.shared");
        ActiveSkillProgressModel model = CreateModel(points: 4);

        const string SlotId = CharacterSkillLoadoutKeys.HelperProcPrefix + "proc.0";
        model.TryUnlock(SlotId, "variant.a", tree, "root", out _, out _);
        model.TryUnlock(SlotId, "variant.b", tree, "root", out _, out _);

        Assert.That(model.ResetTree(SlotId, "variant.a", tree, out int refunded, out _), Is.True);
        Assert.That(refunded, Is.EqualTo(1));
        Assert.That(model.AvailablePoints, Is.EqualTo(3));
        Assert.That(model.IsUnlocked(SlotId, "variant.b", tree, "root", out _), Is.True,
            "Resetting one variant must leave the other variant's progress alone.");
    }

    // ---- Runtime resolution ---------------------------------------------------------------------

    [Test]
    public void OnlyTheSelectedProcVariantIsEquipped()
    {
        SkillHelperDef equipped = CreateProc("Equipped");
        SkillHelperDef alternative = CreateProc("Alternative");
        CharacterStats stats = CreateHelperStats(null);
        stats.helperProcSlots.Add(CreateProcSlot("proc.0", equipped, alternative));

        CharacterSkillManager manager = CreateActor(stats);
        var buffer = new List<SkillHelperDef>();
        manager.AppendConfiguredHelperChainDefinitions(buffer);

        Assert.That(buffer, Is.EquivalentTo(new[] { equipped }),
            "An authored-but-unselected variant must never reach the proc controller.");
    }

    [Test]
    public void SwitchingAProcVariantSwapsWhichDefinitionIsEquipped()
    {
        SkillHelperDef first = CreateProc("First");
        SkillHelperDef second = CreateProc("Second");
        CharacterStats stats = CreateHelperStats(null);
        stats.helperProcSlots.Add(CreateProcSlot("proc.0", first, second));

        CharacterSkillManager manager = CreateActor(stats);
        string slotId = CharacterSkillLoadoutKeys.HelperProcSlotKey(stats.helperProcSlots[0], 0);

        Assert.That(manager.TrySelectLoadoutOption(slotId, "proc.0.option.1", persist: false), Is.True);

        var buffer = new List<SkillHelperDef>();
        manager.AppendConfiguredHelperChainDefinitions(buffer);

        Assert.That(buffer, Is.EquivalentTo(new[] { second }));
        Assert.That(manager.TryGetSelectedLoadoutOptionId(slotId, out string selected), Is.True);
        Assert.That(selected, Is.EqualTo("proc.0.option.1"));
    }

    [Test]
    public void ManualCommandResolvesTheSelectedVariant()
    {
        SkillGemDefinition first = CreateSkill("Command A");
        SkillGemDefinition second = CreateSkill("Command B");
        CharacterStats stats = CreateHelperStats(null);
        stats.helperCommandSlot.options.Add(new CharacterSkillLoadoutOption
        {
            optionId = "command.a",
            skillAsset = first,
        });
        stats.helperCommandSlot.options.Add(new CharacterSkillLoadoutOption
        {
            optionId = "command.b",
            skillAsset = second,
        });

        CharacterSkillManager manager = CreateActor(stats);
        string slotId = CharacterSkillLoadoutKeys.HelperCommandSlotKey(stats.helperCommandSlot);

        Assert.That(manager.PlayerCommandSkill?.skillAsset, Is.SameAs(first));
        Assert.That(manager.TrySelectLoadoutOption(slotId, "command.b", persist: false), Is.True);
        Assert.That(manager.PlayerCommandSkill?.skillAsset, Is.SameAs(second),
            "The party command must follow the variant the player selected.");
    }

    [Test]
    public void StrykerNeverContributesHelperProcs()
    {
        SkillHelperDef proc = CreateProc("Leftover");
        CharacterStats stats = CreateHelperStats(null);
        stats.helperProcSlots.Add(CreateProcSlot("proc.0", proc));
        stats.partyRole = CharacterPartyRole.Stryker;

        CharacterSkillManager manager = CreateActor(stats);
        var buffer = new List<SkillHelperDef>();
        manager.AppendConfiguredHelperChainDefinitions(buffer);

        Assert.That(buffer, Is.Empty,
            "Helper Proc Slots on a Stryker are leftover authoring and must stay inert.");
    }

    // ---- Authored content -----------------------------------------------------------------------

    [Test]
    public void AuthoredHelpersPassLoadoutValidation()
    {
        AssertLoadoutValid("Assets/Character/Abbygail/Chadef.Abbygail.asset");
        AssertLoadoutValid("Assets/Character/Milano/ChaDef.Milano.asset");
    }

    [Test]
    public void MilanoHasNoManualCommandTab()
    {
        CharacterStats milano = AssetDatabase.LoadAssetAtPath<CharacterStats>(
            "Assets/Character/Milano/ChaDef.Milano.asset");
        Assert.That(milano, Is.Not.Null);

        List<SkillLoadoutSlotDescriptor> slots = SkillLoadoutDescriptorFactory.Build(milano);

        Assert.That(slots.Count, Is.EqualTo(1));
        Assert.That(slots[0].Kind, Is.EqualTo(SkillLoadoutKind.HelperProc));
    }

    [Test]
    public void AbbygailShowsHerCommandAndThreeProcTabs()
    {
        CharacterStats abbygail = AssetDatabase.LoadAssetAtPath<CharacterStats>(
            "Assets/Character/Abbygail/Chadef.Abbygail.asset");
        Assert.That(abbygail, Is.Not.Null);

        List<SkillLoadoutSlotDescriptor> slots = SkillLoadoutDescriptorFactory.Build(abbygail);

        Assert.That(slots.Count, Is.EqualTo(4));
        Assert.That(slots[0].Kind, Is.EqualTo(SkillLoadoutKind.HelperCommand));
        for (int i = 1; i < slots.Count; i++)
        {
            Assert.That(slots[i].Kind, Is.EqualTo(SkillLoadoutKind.HelperProc));
            Assert.That(slots[i].Options.Count, Is.GreaterThan(0));
        }
    }

    static void AssertLoadoutValid(string assetPath)
    {
        CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(assetPath);
        Assert.That(stats, Is.Not.Null, $"'{assetPath}' must exist.");

        List<SkillUpgradeValidationIssue> issues = SkillUpgradeTreeValidator.ValidateCharacterLoadout(stats);
        for (int i = 0; i < issues.Count; i++)
        {
            Assert.That(issues[i].Severity, Is.Not.EqualTo(SkillUpgradeValidationSeverity.Error),
                $"'{assetPath}': {issues[i].Message}");
        }
    }

    // ---- Fixtures -------------------------------------------------------------------------------

    CharacterStats CreateHelperStats(SkillGemDefinition manualCommand, params SkillHelperDef[] procs)
    {
        var stats = ScriptableObject.CreateInstance<CharacterStats>();
        stats.name = "TestHelperStats";
        stats.characterId = string.Empty;
        stats.partyRole = CharacterPartyRole.Helper;
        stats.helperCommandSlot = new CharacterSkillLoadoutSlot { slotId = "command" };

        if (manualCommand != null)
        {
            stats.helperCommandSlot.options.Add(new CharacterSkillLoadoutOption
            {
                optionId = "command.default",
                skillAsset = manualCommand,
            });
        }

        stats.helperProcSlots = new List<HelperProcLoadoutSlot>();
        if (procs != null)
        {
            for (int i = 0; i < procs.Length; i++)
                stats.helperProcSlots.Add(CreateProcSlot($"proc.{i}", procs[i]));
        }

        createdObjects.Add(stats);
        return stats;
    }

    static HelperProcLoadoutSlot CreateProcSlot(string slotId, params SkillHelperDef[] procs)
    {
        var slot = new HelperProcLoadoutSlot { slotId = slotId };
        for (int i = 0; i < procs.Length; i++)
        {
            slot.options.Add(new HelperProcLoadoutOption
            {
                optionId = $"{slotId}.option.{i}",
                helperProc = procs[i],
            });
        }

        return slot;
    }

    CharacterSkillManager CreateActor(CharacterStats stats)
    {
        var actor = new GameObject("TestHelperActor");
        createdObjects.Add(actor);

        AllyContext context = actor.AddComponent<AllyContext>();
        context.baseStats = stats;

        return actor.AddComponent<CharacterSkillManager>();
    }

    SkillGemDefinition CreateSkill(string skillName)
    {
        var skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        skill.name = skillName;
        createdObjects.Add(skill);
        return skill;
    }

    SkillHelperDef CreateProc(string procName)
    {
        var proc = ScriptableObject.CreateInstance<SkillHelperDef>();
        proc.name = procName;
        proc.helperId = procName;

        // A proc with no execution skill is not a configurable variant, so every fixture proc gets
        // one - otherwise the loadout would drop it before the test could observe anything.
        proc.executionSkill = CreateSkill($"{procName} Execution");
        createdObjects.Add(proc);
        return proc;
    }

    SkillUpgradeTreeDefinition CreateTree(string treeId)
    {
        var tree = ScriptableObject.CreateInstance<SkillUpgradeTreeDefinition>();
        tree.name = treeId;
        tree.treeId = treeId;
        tree.nodes = new List<SkillUpgradeNodeData>
        {
            new() { nodeId = "root", cost = 1, requiredCharacterLevel = 1 },
        };
        createdObjects.Add(tree);
        return tree;
    }

    static ActiveSkillProgressModel CreateModel(int points)
    {
        var data = new CharacterProgressData
        {
            level = 10,
            skillPoints = points,
            skillProgressInitialized = true,
        };

        return new ActiveSkillProgressModel(null, data, data.level);
    }

    Sprite CreateSprite()
    {
        var texture = new Texture2D(4, 4);
        createdObjects.Add(texture);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
        createdObjects.Add(sprite);
        return sprite;
    }
}
#endif
