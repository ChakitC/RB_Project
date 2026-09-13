using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Separate HUD layout; reads the same actors and skill managers that execute skills.</summary>
public sealed class PartyHudPresenter : MonoBehaviour
{
    [SerializeField] Image[] portraits;
    [SerializeField] Image[] healthFills;
    [SerializeField] Image playerHealth;
    [SerializeField] TMP_Text playerHealthLabel;
    [SerializeField] Image[] commandSegments;
    [SerializeField] TMP_Text ammo;
    [SerializeField] Image[] skillFrames;
    [SerializeField] Image[] skillIcons;
    [SerializeField] TMP_Text[] skillLabels;
    [SerializeField] Sprite readyFrame;
    [SerializeField] Sprite unavailableFrame;
    [SerializeField] GameObject comboRoot;
    [SerializeField] RectTransform[] comboCards;
    [SerializeField] Image[] comboPortraits;
    [SerializeField] TMP_Text[] comboTimes;
    [SerializeField] TMP_Text feedback;
    readonly CharacteContext[] actors = new CharacteContext[4];
    readonly List<PartyComboOfferViewData> offers = new();
    float refreshAt;
    float feedbackUntil;
    public PlayerContext Player { get; private set; }
    static readonly ChainActorRole[] Roles = { ChainActorRole.Player, ChainActorRole.PartySlot1, ChainActorRole.PartySlot2, ChainActorRole.Helper };

    public void Bind(PlayerContext source)
    {
        Player = source;
        Player?.ResolveReferences();
        Refresh();
    }
    void Update()
    {
        if (Player != PlayerContext.Instance) Bind(PlayerContext.Instance);
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + .1f;
        Refresh();
    }
    void ResolveActors()
    {
        actors[0] = Player;
        for (int i = 1; i < 3; i++)
            actors[i] = Player != null && Player.fieldAllyManager != null &&
                Player.fieldAllyManager.TryGetMember(Roles[i], out var member) ? member.ActorContext : null;
        var helper = Player?.allyHelper?.HelperObject;
        actors[3] = helper != null ? helper.GetComponentInChildren<CharacteContext>(true) : null;
    }
    public void Refresh()
    {
        ResolveActors();
        for (int i = 0; i < 3; i++)
        {
            var actor = actors[i + 1];
            SetIcon(portraits[i], actor?.baseStats?.icon);
            SetHealth(healthFills[i], actor);
        }
        SetHealth(playerHealth, Player);
        var hp = Player?.HealthSystem;
        playerHealthLabel.text = hp != null ? $"{hp.currentHealth:0}/{hp.maximumHealth:0}" : "HP / HP";
        var command = Player?.partyCommand;
        float ratio = command != null && command.MaximumCommandPoints > 0f ?
            command.CurrentCommandPoints / command.MaximumCommandPoints : 0f;
        for (int i = 0; i < commandSegments.Length; i++)
            commandSegments[i].fillAmount = Mathf.Clamp01(ratio * commandSegments.Length - i);
        var weapon = Player?.WeaponSystem;
        ammo.text = weapon != null ? $"<size=28>{weapon.CurrentAmmo}/{(weapon.HasInfiniteReserveAmmo ? "∞" : weapon.CurrentReserveAmmo.ToString())}</size>" : "<size=28>— / —</size>";
        for (int kind = 0; kind < 2; kind++) for (int slot = 0; slot < 4; slot++)
        {
            int index = kind * 4 + slot;
            var actor = actors[slot];
            var manager = actor?.SkillManager;
            SkillGemDefinition skill = null;
            bool present = actor != null && actor.baseStats != null && !actor.baseStats.IsHelperRole &&
                manager != null && manager.TryGetSlotSkillDefinition(kind, out skill);
            bool ready = present && actor.HealthSystem != null && actor.HealthSystem.IsAlive &&
                actor.gameObject.activeInHierarchy && manager.CanStartCastSlot(kind);
            skillFrames[index].sprite = ready ? readyFrame : unavailableFrame;
            skillFrames[index].color = ready ? Color.white : new Color(.45f, .45f, .45f, 1f);
            SetIcon(skillIcons[index], present ? skill.icon : null);
            skillIcons[index].color = ready ? Color.white : new Color(.4f, .4f, .4f, .8f);
            string label = present ? (skill.icon != null ? "" : "SKILL") : "—";
            if (present && manager.TryGetSlotChargeStatus(kind, out var charge) && charge.IsRecharging)
                label = $"{charge.NextChargeRemaining:0.0}";
            skillLabels[index].text = label;
        }
        GatherOffers();
        comboRoot.SetActive(offers.Count > 0);
        for (int i = 0; i < comboCards.Length; i++)
        {
            comboCards[i].gameObject.SetActive(i < offers.Count);
            if (i >= offers.Count) continue;
            var offer = offers[i];
            int slot = System.Array.IndexOf(Roles, offer.OwnerRole);
            SetIcon(comboPortraits[i], slot >= 0 ? actors[slot]?.baseStats?.icon : null);
            comboTimes[i].text = Mathf.CeilToInt(Mathf.Max(0, offer.ExpiresAt - Player.partyComboController.ComboClockNow)).ToString();
        }
        if (feedback != null && Time.unscaledTime >= feedbackUntil) feedback.text = "";
    }
    void GatherOffers()
    {
        offers.Clear();
        var controller = Player?.partyComboController;
        if (controller == null || !controller.RuntimeFeatureEnabled) return;
        foreach (var role in Roles)
            if (controller.TryGetOffer(role, out var offer) && offer.State == PartyComboOpportunityState.Offered &&
                offer.ExpiresAt > controller.ComboClockNow &&
                controller.TryGetComboChargeStatus(role, out _, out var charge) && charge.Available > 0)
                offers.Add(offer);
        offers.Sort((a, b) => { int time = a.ExpiresAt.CompareTo(b.ExpiresAt); return time != 0 ? time : a.OfferId.CompareTo(b.OfferId); });
    }
    public bool TryUseFirstCombo()
    {
        GatherOffers();
        if (offers.Count == 0) return false;
        var offer = offers[0];
        if (!Player.partyComboController.TryConsumeOffer(offer.OwnerRole, offer.OfferId, out _)) ShowBlocked();
        return true;
    }
    public void Cast(int slot, int kind)
    {
        ResolveActors();
        var actor = slot >= 0 && slot < actors.Length ? actors[slot] : null;
        if (actor == null || actor.baseStats == null || actor.baseStats.IsHelperRole || !actor.gameObject.activeInHierarchy ||
            actor.HealthSystem == null || !actor.HealthSystem.IsAlive || actor.SkillManager == null ||
            !actor.SkillManager.CanStartCastSlot(kind)) { ShowBlocked(); return; }
        if (!actor.SkillManager.TryStartCastSlot(kind).Started) ShowBlocked();
    }
    void ShowBlocked() { if (feedback != null) feedback.text = "Skill unavailable"; feedbackUntil = Time.unscaledTime + 1.2f; }
    static void SetIcon(Image image, Sprite sprite) { image.sprite = sprite; image.enabled = sprite != null; }
    static void SetHealth(Image image, CharacteContext actor)
    {
        var hp = actor?.HealthSystem;
        image.fillAmount = hp != null && hp.maximumHealth > 0 ? Mathf.Clamp01(hp.currentHealth / hp.maximumHealth) : 0f;
    }
}
