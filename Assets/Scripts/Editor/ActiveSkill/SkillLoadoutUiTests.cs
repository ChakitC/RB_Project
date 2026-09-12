#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SkillLoadoutUiTests
{
    readonly List<Object> created = new();

    T Asset<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        created.Add(value);
        return value;
    }

    [TearDown]
    public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [Test]
    public void FenoHasActiveUltimateAndPassiveTypes()
    {
        var stats = AssetDatabase.LoadAssetAtPath<CharacterStats>("Assets/Scripts/CharacterStats/Asosiation/ChaDef.Feno.asset");
        var slots = SkillLoadoutDescriptorFactory.Build(stats);
        Assert.That(slots[0].SlotKind, Is.EqualTo(CharacterSkillSlotKind.Active));
        Assert.That(slots[1].SlotKind, Is.EqualTo(CharacterSkillSlotKind.Ultimate));
        Assert.That(slots[0].Options[0].SkillAsset, Is.Not.Null);
        Assert.That(slots[1].Options[0].SkillAsset, Is.Not.Null);
        Assert.That(slots.FindAll(s => s.IsPassiveSlot).Count, Is.EqualTo(1));
        Assert.That(slots.Count, Is.EqualTo(4));
        Assert.That(slots[2].DisplayName, Is.EqualTo("PASSIVE"));
    }

    [Test]
    public void TypeButtonsShareBottomRegionAndSwitchSelectedType()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/User Interface/Active Skill/ActiveSkillScreen.prefab");
        var screen = Object.Instantiate(prefab);
        created.Add(screen);
        var controller = screen.GetComponent<ActiveSkillScreenController>();
        controller.BindLobby(AssetDatabase.LoadAssetAtPath<CharacterStats>("Assets/Scripts/CharacterStats/Asosiation/ChaDef.Feno.asset"));
        controller.Open();
        var root = screen.transform.Find("SlotTabs/Viewport/Content");
        var tabs = root.GetComponentsInChildren<ActiveSkillSlotTabView>();
        Assert.That(tabs.Length, Is.EqualTo(4));
        Assert.That(screen.transform.Find("PassiveSlots").gameObject.activeSelf, Is.False);
        Assert.That(((RectTransform)root.parent.parent).anchorMax.x, Is.EqualTo(1f));
        for (int i = 0; i < tabs.Length; i++)
        {
            Assert.That(tabs[i].transform.parent, Is.SameAs(root));
            // EditMode instantiation does not run MonoBehaviour.Awake.
            typeof(ActiveSkillSlotTabView).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tabs[i], null);
            tabs[i].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(typeof(ActiveSkillScreenController).GetField("_selectedSlotIndex", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), Is.EqualTo(i));
        }
        controller.Close();
    }

    [TestCase("Aires")]
    [TestCase("Roma")]
    public void UnmappedCharactersDoNotInventCombatOptions(string character)
    {
        var stats = AssetDatabase.LoadAssetAtPath<CharacterStats>($"Assets/Scripts/CharacterStats/Asosiation/ChaDef.{character}.asset");
        Assert.That(stats, Is.Not.Null);
        var slots = SkillLoadoutDescriptorFactory.Build(stats);
        Assert.That(slots[0].DisplayName, Is.EqualTo("ACTIVE"));
        Assert.That(slots[1].DisplayName, Is.EqualTo("ULTIMATE"));
        Assert.That(slots[0].Options.Count + slots[1].Options.Count, Is.EqualTo(0));
    }

    [Test]
    public void ComboProgressReloadsAndUpdatesOnlyItsRuntimeSnapshot()
    {
        var stats = Asset<CharacterStats>();
        stats.characterId = string.Empty;
        var combo = Asset<PartyComboSkillDef>();
        combo.comboId = "test.combo";
        combo.executionSkill = Asset<SkillGemDefinition>();
        combo.upgradeTree = Asset<SkillUpgradeTreeDefinition>();
        combo.upgradeTree.treeId = "combo.tree";
        combo.upgradeTree.nodes.Add(new SkillUpgradeNodeData
        {
            nodeId = "power", cost = 1,
            statModifiers = new List<StatModifier> { new() { stat = StatType.Damage, add = 7f, mul = 1f } }
        });
        stats.partyComboSkill = combo;
        var data = new CharacterProgressData { level = 10, skillPoints = 3, skillProgressInitialized = true };
        var model = new ActiveSkillProgressModel(stats, data, 10);
        Assert.That(model.TryUnlock(PartyComboSkillDef.ProgressSlotId, combo.RuntimeId, combo.upgradeTree, "power", out _, out _), Is.True);
        var loaded = JsonUtility.FromJson<CharacterProgressData>(JsonUtility.ToJson(data));
        model = new ActiveSkillProgressModel(stats, loaded, 10);
        Assert.That(model.AvailablePoints, Is.EqualTo(2));
        Assert.That(model.BuildSnapshot("active", combo.RuntimeId, combo.upgradeTree, out _).IsEmpty, Is.True);

        var actor = new GameObject("Combo upgrade test");
        created.Add(actor);
        var ctx = actor.AddComponent<AllyContext>();
        ctx.baseStats = stats;
        var child = new GameObject("NestedProgress");
        child.transform.SetParent(actor.transform);
        var progress = child.AddComponent<CharacterActiveSkillProgress>();
        ctx.ActiveSkillProgress = progress;
        typeof(CharacterActiveSkillProgress).GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(progress, model);
        var manager = actor.AddComponent<CharacterSkillManager>();
        Assert.That(manager.TryGetPartyComboRuntimeEntry(combo, out var entry), Is.True);
        Assert.That(entry.runtimeSkill.upgradeSnapshot.TryGetAggregate(StatType.Damage, out float add, out _), Is.True);
        Assert.That(add, Is.EqualTo(7f));
        Assert.That(entry.runtimeSkill.GetFinalStats(null).damage, Is.EqualTo(combo.executionSkill.baseDamage + 7f));
        using var session = ActiveSkillLoadoutSession.CreateRuntime(ctx);
        int index = session.Slots.Count - 1;
        Assert.That(session.Slots[index].Kind, Is.EqualTo(SkillLoadoutKind.PartyCombo));
        Assert.That(session.GetSelectedOptionIndex(index), Is.EqualTo(0));
        Assert.That(session.SelectOption(index, 0), Is.False);
        Assert.That(session.ResetTree(index, 0, out int refunded), Is.True);
        Assert.That(refunded, Is.EqualTo(1));
        Assert.That(manager.TryGetPartyComboRuntimeEntry(combo, out var reset), Is.True);
        Assert.That(reset, Is.SameAs(entry));
        Assert.That(reset.runtimeSkill.upgradeSnapshot.IsEmpty, Is.True);
        Assert.That(reset.runtimeSkill.GetFinalStats(null).damage, Is.EqualTo(combo.executionSkill.baseDamage));
        Assert.That(progress.AvailablePoints, Is.EqualTo(3));
    }

    [Test]
    public void FilteredDefaultUsesIdentityAndSemanticOrder()
    {
        var stats = Asset<CharacterStats>();
        var first = Asset<SkillGemDefinition>();
        var selected = Asset<SkillGemDefinition>();
        stats.skillSlots = new List<CharacterSkillLoadoutSlot>
        {
            new() { slotId = "ultimate", slotKind = CharacterSkillSlotKind.Ultimate },
            new() { slotId = "active", slotKind = CharacterSkillSlotKind.Active, defaultOptionIndex = 2,
                options = new List<CharacterSkillLoadoutOption>
                {
                    new() { optionId = "first", skillAsset = first },
                    new() { optionId = "missing" },
                    new() { optionId = "selected", skillAsset = selected },
                    new() { optionId = "last", skillAsset = first }
                } }
        };
        using var session = ActiveSkillLoadoutSession.CreateLobby(stats);
        Assert.That(session.Slots[0].SlotId, Is.EqualTo("active"));
        Assert.That(session.Slots[0].Options[session.GetSelectedOptionIndex(0)].OptionId, Is.EqualTo("selected"));
    }

    [Test]
    public void LobbySessionCannotBypassRunLockAndKeepsEquippedOption()
    {
        var stats = Asset<CharacterStats>();
        var skill = Asset<SkillGemDefinition>();
        stats.skillSlots = new List<CharacterSkillLoadoutSlot>
        {
            new() { slotId = "active", slotKind = CharacterSkillSlotKind.Active,
                options = new List<CharacterSkillLoadoutOption>
                {
                    new() { optionId = "a", skillAsset = skill },
                    new() { optionId = "b", skillAsset = skill }
                } }
        };
        var go = new GameObject("Loadout UI test run");
        created.Add(go);
        var run = go.AddComponent<MapRunController>();
        var state = new MapRunSession();
        state.SetGraph(new MapGraph());
        typeof(MapRunController).GetField("session", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(run, state);
        using var session = ActiveSkillLoadoutSession.CreateLobby(stats);
        Assert.That(session.CanChangeLoadout, Is.False);
        Assert.That(session.SelectOption(0, 1), Is.False);
        Assert.That(session.GetSelectedOptionIndex(0), Is.EqualTo(0));
    }

    [Test]
    public void HudRebindReadsEachActorsSemanticSkillAndChargePool()
    {
        var source = AssetDatabase.LoadAssetAtPath<CharacterStats>("Assets/Scripts/CharacterStats/Asosiation/ChaDef.Feno.asset");
        var stats = Object.Instantiate(source);
        stats.characterId = string.Empty;
        created.Add(stats);
        // Reversed authoring order proves that HUD/input resolution is semantic.
        (stats.skillSlots[0], stats.skillSlots[1]) = (stats.skillSlots[1], stats.skillSlots[0]);
        var player = new GameObject("Loadout UI player");
        created.Add(player);
        var playerContext = player.AddComponent<PlayerContext>();
        playerContext.baseStats = stats;
        player.AddComponent<SkillUserSystem>();
        var playerManager = player.AddComponent<CharacterSkillManager>();
        var ally = new GameObject("Loadout UI ally");
        created.Add(ally);
        var allyContext = ally.AddComponent<AllyContext>();
        allyContext.baseStats = stats;
        var nested = new GameObject("GamePlayStats_System");
        nested.transform.SetParent(ally.transform);
        var user = nested.AddComponent<SkillUserSystem>();
        var allyManager = nested.AddComponent<CharacterSkillManager>();

        var hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/User Interface/PlayerUI.prefab"));
        created.Add(hud);
        var slot = hud.transform.Find("UI_Manager/PlayerHUD/SkillChargeHud/Slot0");
        var presenter = slot.GetComponent<ActiveSkillChargePresenter>();
        presenter.Bind(playerContext);
        Assert.That(playerManager.TryGetSlotSkillDefinition(0, out var active), Is.True);
        Assert.That(active, Is.SameAs(stats.skillSlots[1].options[0].ActiveSkillAsset));
        Assert.That(playerManager.TryGetSlotSkillDefinition(1, out var ultimate), Is.True);
        Assert.That(ultimate, Is.SameAs(stats.skillSlots[0].options[0].ActiveSkillAsset));
        Assert.That(playerManager.HasConfiguredCommandSlot(2), Is.False);
        Assert.That(hud.transform.Find("UI_Manager/PlayerHUD/SkillChargeHud/Slot2").gameObject.activeSelf, Is.False);
        Assert.That(allyManager.TryGetSlotChargeStatus(0, out _), Is.True);
        allyManager.CommandSlots[1].runtimeSkill.TryStampCooldownOnly(user, out _);
        presenter.Bind(allyContext);
        Assert.That(allyManager.TryGetSlotChargeStatus(0, out var spent), Is.True);
        Assert.That(spent.Available, Is.EqualTo(0));
        Assert.That(slot.Find("View/ChargeLabel").gameObject.activeSelf, Is.False);
        Assert.That(slot.Find("View/SkillIcon").GetComponent<UnityEngine.UI.Image>().sprite, Is.SameAs(active.icon));
        presenter.Bind(playerContext);
        Assert.That(playerManager.TryGetSlotChargeStatus(0, out var fresh), Is.True);
        Assert.That(fresh.Available, Is.GreaterThan(0));
        Assert.That(slot.Find("View/ChargeLabel").gameObject.activeSelf, Is.True);
    }

    [Test]
    public void BasementButtonSelectsThenRunLockRestoresRealSelection()
    {
        var basement = new GameObject("Loadout UI test Basement");
        created.Add(basement);
        basement.AddComponent<BasementContext>();
        var stats = Asset<CharacterStats>();
        var first = Asset<SkillGemDefinition>();
        var second = Asset<SkillGemDefinition>();
        first.displayName = "First active";
        second.displayName = "Second active";
        stats.skillSlots = new List<CharacterSkillLoadoutSlot>
        {
            new() { slotId = "active", slotKind = CharacterSkillSlotKind.Active,
                options = new List<CharacterSkillLoadoutOption>
                {
                    new() { optionId = "first", skillAsset = first },
                    new() { optionId = "second", skillAsset = second }
                } }
        };
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefab/User Interface/Active Skill/ActiveSkillScreen.prefab"));
        created.Add(go);
        var screen = go.GetComponent<ActiveSkillScreenController>();
        screen.BindLobby(stats);
        screen.Open();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var variants = (List<ActiveSkillVariantCardView>)typeof(ActiveSkillScreenController)
            .GetField("_variantCards", flags).GetValue(screen);
        // EditMode does not call Awake on these views; initialize the actual button listener.
        typeof(ActiveSkillVariantCardView).GetMethod("Awake", flags).Invoke(variants[0], null);
        var button = variants[0].GetComponent<UnityEngine.UI.Button>();
        Assert.That(button.interactable, Is.True);
        button.onClick.Invoke();
        var session = (ActiveSkillLoadoutSession)typeof(ActiveSkillScreenController)
            .GetField("_session", flags).GetValue(screen);
        Assert.That(session.GetSelectedOptionIndex(0), Is.EqualTo(1));
        Assert.That(go.transform.Find("VariantPanel/SelectedVariantCard/TitleBackground/Title")
            .GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Second active"));

        var run = basement.AddComponent<MapRunController>();
        var state = new MapRunSession();
        state.SetGraph(new MapGraph());
        typeof(MapRunController).GetField("session", flags).SetValue(run, state);
        typeof(ActiveSkillScreenController).GetMethod("RebuildScreen", flags).Invoke(screen, null);
        Assert.That(button.interactable, Is.False);
        // Even a queued click that reaches the handler after locking must be rejected.
        typeof(ActiveSkillScreenController).GetMethod("SelectVariant", flags).Invoke(screen, new object[] { 0 });
        Assert.That(session.GetSelectedOptionIndex(0), Is.EqualTo(1));
        Assert.That(go.transform.Find("VariantPanel/SelectedVariantCard/TitleBackground/Title")
            .GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Second active"));
        Assert.That(go.transform.Find("LoadoutLockMessage").gameObject.activeSelf, Is.True);
    }
}
#endif
