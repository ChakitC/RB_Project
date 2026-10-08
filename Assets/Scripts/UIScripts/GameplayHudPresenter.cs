using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameplayHudPresenter : MonoBehaviour
{
    sealed class ActorView
    {
        public GameObject root;
        public UnityEngine.UI.Image portrait;
        public UnityEngine.UI.Image healthFill;
        public UnityEngine.UI.Image staminaFill;
        public SlantedBarFillEffect slantedHealthFill;
        public SlantedBarFillEffect slantedStaminaFill;
        public TMP_Text currentHealth;
        public TMP_Text maximumHealth;
        public CharacteContext actor;
    }

    sealed class SkillView
    {
        public GameObject ready;
        public GameObject unavailable;
        public UnityEngine.UI.Image illustration;
        public Sprite defaultIllustration;
    }

    readonly ActorView[] actorViews = new ActorView[4];
    readonly SkillView[] skillViews = new SkillView[4];
    readonly CharacteContext[] actors = new CharacteContext[4];

    TMP_Text currentAmmo;
    TMP_Text reserveAmmo;
    UnityEngine.UI.Image ammoFill;
    GameObject followUpDisplay;
    PlayerContext player;
    float nextRefresh;

    public void Bind(PlayerContext context)
    {
        player = context;
        if (player != null)
            player.ResolveReferences();

        GameplayHudAimLayout aimLayout = GetComponent<GameplayHudAimLayout>();
        if (aimLayout == null)
            aimLayout = gameObject.AddComponent<GameplayHudAimLayout>();
        aimLayout.Bind(player);

        GameplayHudBlockVisibility blockVisibility = GetComponent<GameplayHudBlockVisibility>();
        if (blockVisibility == null)
            blockVisibility = gameObject.AddComponent<GameplayHudBlockVisibility>();
        blockVisibility.Bind(player);

        PassiveHudHost passiveHud = GetComponentInChildren<PassiveHudHost>(true);
        if (passiveHud != null) passiveHud.Bind(player);

        CooperativeBlockHud blockHud = GetComponentInChildren<CooperativeBlockHud>(true);
        if (blockHud != null)
            blockHud.Bind(player != null ? player.interruptionCommand : null);

        actorViews[0] = CreateActorView("PlayerHpStamina", false);
        actorViews[1] = CreateActorView("AllyTop", true);
        actorViews[2] = CreateActorView("AllyMiddle", true);
        actorViews[3] = CreateActorView("AllyBottom", true);

        for (int i = 0; i < skillViews.Length; i++)
        {
            Transform card = transform.Find($"SkillReady Ally {i + 1}");
            skillViews[i] = new SkillView
            {
                ready = FindChild(card, "ReadyState")?.gameObject,
                unavailable = FindChild(card, "UnavailableState")?.gameObject,
                illustration = FindChild(card, "ReadySkillIllustration")?.GetComponent<UnityEngine.UI.Image>(),
            };
            if (skillViews[i].illustration != null)
                skillViews[i].defaultIllustration = skillViews[i].illustration.sprite;
        }

        Transform ammo = transform.Find("AmmoDisplay");
        currentAmmo = FindChild(ammo, "CurrentAmmoText")?.GetComponent<TMP_Text>();
        reserveAmmo = FindChild(ammo, "ReserveAmmoText")?.GetComponent<TMP_Text>();
        ammoFill = FindChild(ammo, "Ammo_Progress Bar")?.GetComponent<UnityEngine.UI.Image>();
        followUpDisplay = transform.Find("FollowUpDisplay")?.gameObject;
        if (followUpDisplay != null)
            followUpDisplay.SetActive(false);

        nextRefresh = 0f;
        Refresh();
    }

    void Update()
    {
        if (player == null || Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + 0.1f;
        Refresh();
    }

    void Refresh()
    {
        if (player == null)
            return;

        actors[0] = player;
        actors[1] = GetPartyActor(ChainActorRole.PartySlot1);
        actors[2] = GetPartyActor(ChainActorRole.PartySlot2);
        actors[3] = GetHelperActor();

        for (int i = 0; i < actorViews.Length; i++)
        {
            RefreshActor(actorViews[i], actors[i]);
            RefreshSkill(skillViews[i], actors[i]);
        }

        WeaponSystem weapon = player.WeaponSystem;
        if (currentAmmo != null)
            currentAmmo.text = weapon != null ? weapon.CurrentAmmo.ToString() : "0";
        if (reserveAmmo != null)
            reserveAmmo.text = weapon == null ? "0" :
                weapon.HasInfiniteReserveAmmo ? "INF" : weapon.CurrentReserveAmmo.ToString();
        SetFill(ammoFill, weapon != null ? weapon.CurrentAmmo : 0,
            weapon != null ? weapon.MagazineSize : 0);
    }

    CharacteContext GetPartyActor(ChainActorRole role)
    {
        FieldAllyManager manager = player.fieldAllyManager;
        return manager != null && manager.TryGetMember(role, out FieldAllyMember member) && member != null
            ? member.ActorContext
            : null;
    }

    CharacteContext GetHelperActor()
    {
        AllyHelperManager manager = player.allyHelper;
        if (manager == null || !manager.IsHelperActive || manager.HelperObject == null)
            return null;

        return manager.HelperObject.GetComponentInChildren<AllyContext>(true);
    }

    public bool TryBindFollowUpDisplay(PartyHudPresenter presenter)
    {
        if (presenter == null || followUpDisplay == null)
            return false;

        // The first card, beside E, must use the same offer ordering as input.
        string[] names = { "AllyIconSmall", "AllyIconMedium", "AllyIconLarge" };
        var cards = new RectTransform[names.Length];
        var portraits = new UnityEngine.UI.Image[names.Length];
        var times = new TMP_Text[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            cards[i] = followUpDisplay.transform.Find(names[i]) as RectTransform;
            portraits[i] = FindChild(cards[i], "PortraitIcon")?.GetComponent<UnityEngine.UI.Image>();
            times[i] = FindChild(cards[i], "Remaining")?.GetComponent<TMP_Text>();
        }

        return presenter.BindFollowUpView(followUpDisplay, cards, portraits, times);
    }

    static ActorView CreateActorView(Transform root, bool ally)
    {
        if (root == null)
            return new ActorView();

        ActorView view = new ActorView
        {
            root = root.gameObject,
            portrait = FindChild(root, "PortraitIcon")?.GetComponent<UnityEngine.UI.Image>(),
            healthFill = FindChild(root, ally ? "AllyStatus_HP_Progress" : "HP_Bar_Progress Bar Fill")?.GetComponent<UnityEngine.UI.Image>(),
            staminaFill = FindChild(root, ally ? "AllyStatus_Stamina_Progress" : "Stamina_ProgressBar")?.GetComponent<UnityEngine.UI.Image>(),
            currentHealth = FindChild(root, "CurrentHPText")?.GetComponent<TMP_Text>(),
            maximumHealth = FindChild(root, "MaxHPText")?.GetComponent<TMP_Text>(),
        };

        if (!ally)
        {
            // These sprites draw diagonally inside a rectangular texture. Clip along
            // the painted bar instead of using Image's horizontal fill direction.
            view.slantedHealthFill = ConfigureSlantedFill(view.healthFill,
                new Vector2(-0.47416f, 0.88044f), -0.22423f, 0.47894f);
            view.slantedStaminaFill = ConfigureSlantedFill(view.staminaFill,
                new Vector2(-0.45983f, 0.88801f), -0.20906f, 0.72190f);
        }

        return view;
    }

    static SlantedBarFillEffect ConfigureSlantedFill(UnityEngine.UI.Image image,
        Vector2 axis, float start, float end)
    {
        if (image == null)
            return null;

        image.type = UnityEngine.UI.Image.Type.Simple;
        SlantedBarFillEffect effect = image.GetComponent<SlantedBarFillEffect>();
        if (effect == null)
            effect = image.gameObject.AddComponent<SlantedBarFillEffect>();
        effect.Configure(axis, start, end);
        return effect;
    }

    ActorView CreateActorView(string name, bool ally) => CreateActorView(transform.Find(name), ally);

    static void RefreshActor(ActorView view, CharacteContext actor)
    {
        if (view == null || view.root == null)
            return;

        view.root.SetActive(actor != null);
        if (actor == null)
        {
            view.actor = null;
            return;
        }

        if (view.actor != actor)
        {
            view.actor = actor;
            actor.ResolveReferences();
        }

        // Character data can load or change after the actor was first bound.
        if (view.portrait != null)
        {
            Sprite portrait = actor.baseStats != null ? actor.baseStats.icon : null;
            if (view.portrait.sprite != portrait)
                view.portrait.sprite = portrait;
            view.portrait.enabled = portrait != null;
        }

        HealthSystem health = actor.HealthSystem;
        StaminaSystem stamina = actor.StaminaSystem;
        SetActorFill(view.healthFill, view.slantedHealthFill,
            health != null ? health.currentHealth : 0f,
            health != null ? health.maximumHealth : 0f);
        SetActorFill(view.staminaFill, view.slantedStaminaFill,
            stamina != null ? stamina.Current : 0f,
            stamina != null ? stamina.Max : 0f);
        if (view.currentHealth != null)
            view.currentHealth.text = health != null ? Mathf.CeilToInt(health.currentHealth).ToString() : "0";
        if (view.maximumHealth != null)
            view.maximumHealth.text = health != null ? Mathf.CeilToInt(health.maximumHealth).ToString() : "0";
    }

    static void RefreshSkill(SkillView view, CharacteContext actor)
    {
        if (view == null)
            return;

        CharacterSkillManager manager = actor != null ? actor.SkillManager : null;
        bool ready = manager != null && manager.TryGetSlotChargeStatus(0, out SkillChargeStatus status) &&
                     status.Available > 0 && manager.CanStartCastSlot(0);
        if (view.illustration != null)
        {
            view.illustration.sprite = manager != null &&
                manager.TryGetSlotSkillDefinition(0, out SkillGemDefinition skill) && skill.icon != null
                    ? skill.icon
                    : view.defaultIllustration;
        }
        if (view.ready != null) view.ready.SetActive(ready);
        if (view.unavailable != null) view.unavailable.SetActive(!ready);
    }

    static void SetFill(UnityEngine.UI.Image image, float current, float maximum)
    {
        if (image == null)
            return;

        image.type = UnityEngine.UI.Image.Type.Filled;
        image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        image.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
        image.fillAmount = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
    }

    static void SetActorFill(UnityEngine.UI.Image image, SlantedBarFillEffect slantedFill,
        float current, float maximum)
    {
        if (slantedFill != null)
            slantedFill.SetFillAmount(maximum > 0f ? current / maximum : 0f);
        else
            SetFill(image, current, maximum);
    }

    static Transform FindChild(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }
}
