using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class GameplayFollowUpHudTests
{
    readonly List<Object> cleanup = new();
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
            if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
        cleanup.Clear();
    }

    [Test]
    public void ReplacementShowsOrderedLiveOffersAndClearsOnExpiryAndRebind()
    {
        GameObject ui = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefab/User Interface/PlayerUI_PartyHud.prefab")));
        GameObject hud = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Spain/UI/GameplayHUD.prefab")));
        PartyHudPresenter partyHud = ui.GetComponentInChildren<PartyHudPresenter>(true);
        GameObject oldRoot = (GameObject)new SerializedObject(partyHud)
            .FindProperty("followUpRoot").objectReferenceValue;

        GameObject actor = Track(new GameObject("FollowUpHudPlayer"));
        PlayerContext player = actor.AddComponent<PlayerContext>();
        player.fieldAllyManager = actor.AddComponent<FieldAllyManager>();
        PartyFollowUpOpportunityController controller = actor.AddComponent<PartyFollowUpOpportunityController>();
        player.partyFollowUpController = controller;
        var flags = Track(ScriptableObject.CreateInstance<PartyFollowUpFeatureFlags>());
        flags.runtimeEnabled = true;
        Set(controller, "featureFlags", flags);
        var runtime = new PartyRuntime(actor);
        Set(controller, "party", runtime);

        Sprite firstPortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Spain/Charactor_Icon/Aires_Icon.png");
        Sprite secondPortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Spain/Charactor_Icon/Roma_Icon.png");
        Assert.That(firstPortrait, Is.Not.Null);
        Assert.That(secondPortrait, Is.Not.Null);
        PartyFollowUpSkillDef firstFollowUp = AddAlly(runtime, player, ChainActorRole.PartySlot1, firstPortrait);
        PartyFollowUpSkillDef secondFollowUp = AddAlly(runtime, player, ChainActorRole.PartySlot2, secondPortrait);
        var offers = (Dictionary<ChainActorRole, PartyFollowUpOpportunity>)typeof(PartyFollowUpOpportunityController)
            .GetField("offers", PrivateInstance).GetValue(controller);
        offers[ChainActorRole.PartySlot1] = Offer(1, ChainActorRole.PartySlot1, firstFollowUp, 8f);
        offers[ChainActorRole.PartySlot2] = Offer(2, ChainActorRole.PartySlot2, secondFollowUp, 4f);

        GameplayHudPresenter presenter = hud.AddComponent<GameplayHudPresenter>();
        presenter.Bind(player);
        partyHud.Bind(player);
        Assert.That(presenter.TryBindFollowUpDisplay(partyHud), Is.True);
        Transform display = hud.transform.Find("FollowUpDisplay");
        Transform small = display.Find("AllyIconSmall");
        Transform medium = display.Find("AllyIconMedium");
        foreach (TMP_Text label in display.GetComponentsInChildren<TMP_Text>(true))
        {
            Assert.That(label.font, Is.Not.Null, "Countdowns need a serialized font.");
            Assert.That(label.fontSharedMaterial, Is.Not.Null);
        }
        Assert.That(oldRoot.activeSelf, Is.False);
        Assert.That(display.gameObject.activeSelf, Is.True);
        Assert.That(small.Find("PortraitMask/PortraitIcon").GetComponent<UnityEngine.UI.Image>().sprite,
            Is.SameAs(secondPortrait), "E consumes the offer with the earliest expiry.");
        Assert.That(medium.Find("PortraitMask/PortraitIcon").GetComponent<UnityEngine.UI.Image>().sprite,
            Is.SameAs(firstPortrait));
        Assert.That(small.Find("Remaining").GetComponent<TMP_Text>().text, Is.EqualTo("4"));
        Assert.That(display.Find("AllyIconLarge").gameObject.activeSelf, Is.False);

        // Gameplay refresh must not override the followUp presenter's visibility.
        Set(controller, "followUpClock", 5f);
        partyHud.Refresh();
        Assert.That(small.Find("Remaining").GetComponent<TMP_Text>().text, Is.EqualTo("3"));
        Assert.That(medium.gameObject.activeSelf, Is.False);
        Assert.That(oldRoot.activeSelf, Is.False);
        flags.runtimeEnabled = false;
        partyHud.Refresh();
        typeof(GameplayHudPresenter).GetMethod("Refresh", PrivateInstance).Invoke(presenter, null);
        Assert.That(display.gameObject.activeSelf, Is.False);
        flags.runtimeEnabled = true;
        partyHud.Refresh();
        Assert.That(display.gameObject.activeSelf, Is.True);
        partyHud.Bind(null);
        Assert.That(display.gameObject.activeSelf, Is.False);
        Assert.That(presenter.TryBindFollowUpDisplay(partyHud), Is.True, "Rebinding must be idempotent.");
        Assert.That(oldRoot.activeSelf, Is.False);
    }

    [Test]
    public void RuntimeBinderUsesOneFollowUpViewAndKeepsInputRouter()
    {
        GameObject ui = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefab/User Interface/PlayerUI_PartyHud.prefab")));
        GameObject actor = Track(new GameObject("FollowUpBinderPlayer"));
        PlayerContext player = actor.AddComponent<PlayerContext>();
        player.inventory = actor.AddComponent<PlayerInventory>();
        PlayerUIRuntimeBinder binder = ui.GetComponent<PlayerUIRuntimeBinder>();
        Assert.That(binder.TryBind(player, out string error), Is.True, error);
        Assert.That(binder.TryBind(player, out error), Is.True, error);
        PartyHudPresenter partyHud = ui.GetComponentInChildren<PartyHudPresenter>(true);
        var root = (GameObject)new SerializedObject(partyHud).FindProperty("followUpRoot").objectReferenceValue;
        Assert.That(root.name, Is.EqualTo("FollowUpDisplay"));
        Assert.That(ui.GetComponentsInChildren<GameplayHudPresenter>(true).Length, Is.EqualTo(1));
        Assert.That(ui.transform.Find("UI_Manager/PartyHUD/FollowUpOffers").gameObject.activeSelf, Is.False);
        Assert.That(ui.transform.Find("UI_Manager/PlayerHUD/PartyFollowUpHud").gameObject.activeSelf, Is.False);
        Assert.That(ui.GetComponentInChildren<PartyHudInputRouter>(true).isActiveAndEnabled, Is.True);
    }

    [Test]
    public void IncompleteReplacementKeepsExistingFollowUpView()
    {
        GameObject ui = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefab/User Interface/PlayerUI_PartyHud.prefab")));
        PartyHudPresenter presenter = ui.GetComponentInChildren<PartyHudPresenter>(true);
        GameObject oldRoot = (GameObject)new SerializedObject(presenter)
            .FindProperty("followUpRoot").objectReferenceValue;
        oldRoot.SetActive(true);
        GameObject replacement = Track(new GameObject("IncompleteFollowUpView"));
        Assert.That(presenter.BindFollowUpView(replacement, new RectTransform[3],
            new UnityEngine.UI.Image[3], new TMP_Text[3]), Is.False);
        Assert.That(oldRoot.activeSelf, Is.True);
    }

    PartyFollowUpSkillDef AddAlly(PartyRuntime runtime, PlayerContext player, ChainActorRole role, Sprite portrait)
    {
        GameObject go = Track(new GameObject(role.ToString()));
        AllyContext context = go.AddComponent<AllyContext>();
        go.AddComponent<SkillUserSystem>();
        context.SkillManager = go.AddComponent<CharacterSkillManager>();
        CharacterStats stats = Track(ScriptableObject.CreateInstance<CharacterStats>());
        stats.icon = portrait;
        context.baseStats = stats;
        var followUp = Track(ScriptableObject.CreateInstance<PartyFollowUpSkillDef>());
        followUp.executionSkill = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        stats.partyFollowUpSkill = followUp;
        FieldAllyMember member = go.AddComponent<FieldAllyMember>();
        Set(member, "actorRole", role);
        Set(member, "actorContext", context);
        player.fieldAllyManager.Register(member);
        typeof(PartyRuntime).GetMethod("AddActor", PrivateInstance).Invoke(runtime,
            new object[] { new PartyRuntimeActor(role, 0, go, context, null, member) });
        return followUp;
    }

    static PartyFollowUpOpportunity Offer(int id, ChainActorRole role, PartyFollowUpSkillDef definition, float expiry)
    {
        return new PartyFollowUpOpportunity { OfferId = id, OwnerRole = role, Definition = definition,
            ExpiresAt = expiry, State = PartyFollowUpOpportunityState.Offered };
    }

    T Track<T>(T value) where T : Object { cleanup.Add(value); return value; }
    static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
}
