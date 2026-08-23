#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Covers the one rule that makes runtime-helper skills work: helper procs and the manual command
/// come from the helper's own <c>ctx.baseStats</c> and from nowhere else.
///
/// The helper actor and every party-slot rig are shared prefabs, so anything authored on them
/// would belong to whoever happens to be loaded into that rig. These checks pin the source, the
/// "empty means none" rule, and what happens when the character is swapped underneath a live actor.
///
/// Everything here is Edit Mode. The cast itself - animation acceptance, cast-point timing, the
/// actual cancellation of a running cast - needs a live Animancer driver and stays a Play Mode
/// check; what is verified here is the loadout state each of those paths reads.
/// </summary>
public sealed class HelperBaseStatsLoadoutSmokeTests
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

    // ---- Source of truth ------------------------------------------------------------------------

    [Test]
    public void HelperRoleStatsProvideTheManualCommand()
    {
        SkillGemDefinition manualCommand = CreateSkill("Manual Command");
        CharacterStats stats = CreateHelperStats(manualCommand);
        CharacterSkillManager manager = CreateActor(stats);

        Assert.That(manager.HasConfiguredPlayerCommandSkill, Is.True,
            "A Helper-role character with a command skill must expose it.");
        Assert.That(manager.PlayerCommandSkill, Is.Not.Null);
        Assert.That(manager.PlayerCommandSkill.skillAsset, Is.SameAs(manualCommand),
            "The manual command must be the one authored on the character asset.");
    }

    [Test]
    public void HelperRoleStatsProvideOnlyTheirOwnProcs()
    {
        SkillHelperDef ownProc = CreateHelperProc("Own Proc");
        CharacterStats stats = CreateHelperStats(null, ownProc);
        CharacterSkillManager manager = CreateActor(stats);

        var buffer = new List<SkillHelperDef>();
        manager.AppendConfiguredHelperChainDefinitions(buffer);

        Assert.That(buffer, Is.EquivalentTo(new[] { ownProc }),
            "Helper procs must come from this character's stats and nothing else.");
    }

    [Test]
    public void StrykerRoleStatsProvideNeitherProcsNorManualCommand()
    {
        SkillGemDefinition manualCommand = CreateSkill("Manual Command");
        SkillHelperDef proc = CreateHelperProc("Proc");

        CharacterStats stats = CreateHelperStats(manualCommand, proc);
        stats.partyRole = CharacterPartyRole.Stryker;

        CharacterSkillManager manager = CreateActor(stats);

        var buffer = new List<SkillHelperDef>();
        manager.AppendConfiguredHelperChainDefinitions(buffer);

        Assert.That(buffer, Is.Empty,
            "A Stryker fights in the field; leftover Helper Procs authoring must not fire.");
        Assert.That(manager.HasConfiguredPlayerCommandSkill, Is.False,
            "A Stryker has command slots, not a helper manual command.");
        Assert.That(manager.PlayerCommandSkill, Is.Null);
    }

    [Test]
    public void MissingBaseStatsProvideNoSkills()
    {
        CharacterSkillManager manager = CreateActor(null);

        var buffer = new List<SkillHelperDef>();
        manager.AppendConfiguredHelperChainDefinitions(buffer);

        Assert.That(buffer, Is.Empty);
        Assert.That(manager.HasConfiguredPlayerCommandSkill, Is.False);
        Assert.That(manager.PlayerCommandSkill, Is.Null);
    }

    [Test]
    public void EmptyHelperSlotsMeanNoSkillRatherThanAFallback()
    {
        CharacterStats stats = CreateHelperStats(null);
        CharacterSkillManager manager = CreateActor(stats);

        var buffer = new List<SkillHelperDef>();
        manager.AppendConfiguredHelperChainDefinitions(buffer);

        Assert.That(buffer, Is.Empty,
            "An empty Helper Procs list is an authoring choice, not a prompt to look elsewhere.");
        Assert.That(manager.HasConfiguredPlayerCommandSkill, Is.False,
            "An empty Helper Command Skill means this helper has no manual command.");
        Assert.That(manager.TryStartPlayerCommandSkill().Started, Is.False);
    }

    [Test]
    public void EmptyHelperCommandDoesNotFallBackToAnAutonomousSlot()
    {
        SkillGemDefinition straySlotSkill = CreateSkill("Stray Slot Skill");
        CharacterStats stats = CreateHelperStats(null);
        PartyCommandController controller = CreatePlayerWithHelper(stats, straySlotSkill);
        AllyHelperManager helperManager = controller.GetComponent<PlayerContext>().allyHelper;

        Assert.That(helperManager.HelperSkillManager.HasConfiguredCommandSlot(0), Is.True,
            "The fixture must prove that a separate Stryker command slot exists on the shared rig.");
        Assert.That(helperManager.HasConfiguredCommandSlot(0), Is.False,
            "The runtime helper must not treat a Stryker command slot as its manual command.");

        var command = new PartyCommandDefinition
        {
            executionKind = PartyCommandExecutionKind.HelperCommandSlot,
            helperCommandSlotIndex = 0,
        };

        Assert.That(controller.ResolveCommandDisplayName(command), Is.EqualTo("Helper Command 1"));
        Assert.That(controller.ResolveCommandIcon(command), Is.Null,
            "Presentation must not leak from the autonomous slot when Helper Command Skill is empty.");
    }

    // ---- Swapping the loaded character ----------------------------------------------------------

    [Test]
    public void SwappingBaseStatsSwapsBothTheManualCommandAndTheProcs()
    {
        SkillGemDefinition commandA = CreateSkill("Command A");
        SkillGemDefinition commandB = CreateSkill("Command B");
        SkillHelperDef procA = CreateHelperProc("Proc A");
        SkillHelperDef procB = CreateHelperProc("Proc B");

        CharacterStats statsA = CreateHelperStats(commandA, procA);
        CharacterStats statsB = CreateHelperStats(commandB, procB);

        CharacterSkillManager manager = CreateActor(statsA);
        Assert.That(manager.PlayerCommandSkill.skillAsset, Is.SameAs(commandA));

        manager.GetComponent<AllyContext>().baseStats = statsB;
        manager.RefreshCharacterOwnedLoadout();

        Assert.That(manager.PlayerCommandSkill.skillAsset, Is.SameAs(commandB),
            "Loading a different character must replace the manual command.");

        var buffer = new List<SkillHelperDef>();
        manager.AppendConfiguredHelperChainDefinitions(buffer);
        Assert.That(buffer, Is.EquivalentTo(new[] { procB }),
            "Procs must follow the character out of the rig, not linger on it.");
    }

    [Test]
    public void SwappingTheDefinitionDropsTheRuntimeInstanceTheOldCastWasRunning()
    {
        SkillGemDefinition commandA = CreateSkill("Command A");
        SkillGemDefinition commandB = CreateSkill("Command B");

        CharacterStats statsA = CreateHelperStats(commandA);
        CharacterStats statsB = CreateHelperStats(commandB);

        CharacterSkillManager manager = CreateActor(statsA);
        SkillInstance runtimeA = manager.PlayerCommandSkill.runtimeSkill;
        Assert.That(runtimeA, Is.Not.Null);

        manager.GetComponent<AllyContext>().baseStats = statsB;
        manager.RefreshCharacterOwnedLoadout();

        SkillInstance runtimeB = manager.PlayerCommandSkill.runtimeSkill;
        Assert.That(runtimeB, Is.Not.SameAs(runtimeA),
            "A cast still holding the old instance must fail its validity check instead of finishing " +
            "with a skill this character no longer owns.");
        Assert.That(runtimeB.def, Is.SameAs(commandB));
    }

    [Test]
    public void TheRuntimeEntryKeepsOneInstanceAndOneSharedChargePool()
    {
        SkillGemDefinition manualCommand = CreateSkill("Manual Command");
        CharacterStats stats = CreateHelperStats(manualCommand);
        CharacterSkillManager manager = CreateActor(stats);

        SkillInstance first = manager.PlayerCommandSkill.runtimeSkill;
        SkillInstance second = manager.PlayerCommandSkill.runtimeSkill;

        Assert.That(second, Is.SameAs(first), "Reading the manual command must not rebuild it each time.");
        Assert.That(first.HasBoundCharges, Is.True,
            "The runtime entry must draw from the same shared charge pool as any other slot holding this skill.");
    }

    // ---- Chain attack is a separate system ------------------------------------------------------

    [Test]
    public void HelperChainAttackIsUnaffectedByTheManualCommand()
    {
        SkillGemDefinition manualCommand = CreateSkill("Manual Command");
        SkillGemDefinition chainSkill = CreateSkill("Chain Skill");

        CharacterStats stats = CreateHelperStats(manualCommand);
        stats.chainAttackSkill = chainSkill;

        CharacterSkillManager manager = CreateActor(stats);

        Assert.That(manager.HasConfiguredChainAttackSkill, Is.False,
            "chainAttackSkill stays prefab-authored on the skill manager; the character asset field " +
            "feeds FieldAllyMember, not this entry.");
        Assert.That(manager.PlayerCommandSkill.skillAsset, Is.SameAs(manualCommand),
            "The manual command must not be confused with the chain attack skill.");
    }

    // ---- Party command label --------------------------------------------------------------------

    [Test]
    public void HelperCommandSlotLabelUsesTheRuntimeSkillWhenTheCommandDoesNotOverrideIt()
    {
        SkillGemDefinition manualCommand = CreateSkill("Manual Command");
        manualCommand.displayName = "Pick Up";
        manualCommand.icon = CreateSprite();

        PartyCommandController controller = CreatePlayerWithHelper(CreateHelperStats(manualCommand));
        var command = new PartyCommandDefinition
        {
            executionKind = PartyCommandExecutionKind.HelperCommandSlot,
        };

        Assert.That(controller.ResolveCommandDisplayName(command), Is.EqualTo("Pick Up"),
            "With no authored name the command must read the helper's own skill.");
        Assert.That(controller.ResolveCommandIcon(command), Is.SameAs(manualCommand.icon));
    }

    [Test]
    public void AnAuthoredNameAndIconStillOverrideTheRuntimeSkill()
    {
        SkillGemDefinition manualCommand = CreateSkill("Manual Command");
        manualCommand.displayName = "Pick Up";
        manualCommand.icon = CreateSprite();

        PartyCommandController controller = CreatePlayerWithHelper(CreateHelperStats(manualCommand));
        Sprite overrideIcon = CreateSprite();
        var command = new PartyCommandDefinition
        {
            executionKind = PartyCommandExecutionKind.HelperCommandSlot,
            displayName = "Assist",
            icon = overrideIcon,
        };

        Assert.That(controller.ResolveCommandDisplayName(command), Is.EqualTo("Assist"));
        Assert.That(controller.ResolveCommandIcon(command), Is.SameAs(overrideIcon));
    }

    [Test]
    public void ScriptedHelperSkillOverrideKeepsItsOwnSemantics()
    {
        SkillGemDefinition manualCommand = CreateSkill("Manual Command");
        manualCommand.displayName = "Pick Up";

        SkillGemDefinition scriptedSkill = CreateSkill("Scripted Skill");
        scriptedSkill.displayName = "Scripted";

        PartyCommandController controller = CreatePlayerWithHelper(CreateHelperStats(manualCommand));
        var command = new PartyCommandDefinition
        {
            executionKind = PartyCommandExecutionKind.HelperSkill,
            helperSkill = scriptedSkill,
        };

        Assert.That(command.HasExecutionConfigured, Is.True,
            "HelperSkill remains available for scripted overrides.");
        Assert.That(controller.ResolveCommandDisplayName(command), Is.EqualTo("Scripted"),
            "A scripted override names itself from its own skill, not from the loaded helper.");
    }

    // ---- Authored content -----------------------------------------------------------------------

    [Test]
    public void AbbygailOwnsHerManualCommandAndAllThreeProcs()
    {
        CharacterStats abbygail = AssetDatabase.LoadAssetAtPath<CharacterStats>(
            "Assets/Character/Abbygail/Chadef.Abbygail.asset");

        Assert.That(abbygail, Is.Not.Null, "Abbygail's character asset must exist.");
        Assert.That(abbygail.partyRole, Is.EqualTo(CharacterPartyRole.Helper));
        Assert.That(abbygail.helperCommandSlot, Is.Not.Null,
            "Abbygail's manual command must live on her character asset.");
        Assert.That(abbygail.helperCommandSlot.TryGetDefaultOption(out _, out CharacterSkillLoadoutOption command), Is.True,
            "Abbygail's command slot must resolve a default option.");
        Assert.That(command.ActiveSkillAsset, Is.Not.Null,
            "The manual command option must reference a castable gem.");
        Assert.That(abbygail.helperProcSlots.Count, Is.EqualTo(3));
        for (int i = 0; i < abbygail.helperProcSlots.Count; i++)
        {
            Assert.That(abbygail.helperProcSlots[i].TryGetDefaultOption(out _, out HelperProcLoadoutOption option), Is.True,
                $"Helper proc slot {i} must resolve a default option.");
            Assert.That(option.helperProc, Is.Not.Null,
                $"Helper proc slot {i} must reference a definition.");
            Assert.That(option.ExecutionSkill, Is.Not.Null,
                $"Helper proc slot {i} must reference a proc with an execution skill.");
        }
    }

    // ---- Fixtures -------------------------------------------------------------------------------

    CharacterStats CreateHelperStats(SkillGemDefinition manualCommand, params SkillHelperDef[] procs)
    {
        var stats = ScriptableObject.CreateInstance<CharacterStats>();
        stats.name = "TestHelperStats";
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
            {
                var slot = new HelperProcLoadoutSlot { slotId = $"proc.{i}" };
                slot.options.Add(new HelperProcLoadoutOption
                {
                    optionId = $"proc.{i}.default",
                    helperProc = procs[i],
                });
                stats.helperProcSlots.Add(slot);
            }
        }

        createdObjects.Add(stats);
        return stats;
    }

    SkillGemDefinition CreateSkill(string skillName)
    {
        var skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        skill.name = skillName;
        createdObjects.Add(skill);
        return skill;
    }

    SkillHelperDef CreateHelperProc(string procName)
    {
        var proc = ScriptableObject.CreateInstance<SkillHelperDef>();
        proc.name = procName;

        // A proc with no execution skill is not a configurable variant, so every fixture proc
        // gets one - otherwise the loadout would drop it before the test could observe anything.
        proc.executionSkill = CreateSkill($"{procName} Execution");
        createdObjects.Add(proc);
        return proc;
    }

    Sprite CreateSprite()
    {
        var texture = new Texture2D(4, 4);
        createdObjects.Add(texture);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
        createdObjects.Add(sprite);
        return sprite;
    }

    CharacterSkillManager CreateActor(CharacterStats stats)
    {
        var actor = new GameObject("TestHelperActor");
        createdObjects.Add(actor);

        AllyContext context = actor.AddComponent<AllyContext>();
        context.baseStats = stats;

        return actor.AddComponent<CharacterSkillManager>();
    }

    PartyCommandController CreatePlayerWithHelper(
        CharacterStats helperStats,
        SkillGemDefinition autonomousSkill = null)
    {
        CharacterSkillManager helperSkillManager = CreateActor(helperStats);
        if (autonomousSkill != null)
        {
            var helperManagerSerialized = new SerializedObject(helperSkillManager);
            SerializedProperty slots = helperManagerSerialized.FindProperty("autonomousSlots");
            slots.arraySize = 1;
            slots.GetArrayElementAtIndex(0).FindPropertyRelative("skillAsset").objectReferenceValue = autonomousSkill;
            helperManagerSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        var player = new GameObject("TestPlayer");
        createdObjects.Add(player);

        PlayerContext playerContext = player.AddComponent<PlayerContext>();
        AllyHelperManager helperManager = player.AddComponent<AllyHelperManager>();
        helperManager.BindHelper(helperSkillManager.GetComponent<AllyContext>());
        playerContext.allyHelper = helperManager;

        PartyCommandController controller = player.AddComponent<PartyCommandController>();
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("playerContext").objectReferenceValue = playerContext;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return controller;
    }
}
#endif
