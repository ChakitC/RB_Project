using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private StatusEffectController statusEffectController;
    [SerializeField] private GameObject playerHudRoot;
    [SerializeField] private MonoBehaviour fullscreenEffects;

    public GameObject Inventory;
    public IPlayerFullscreenEffectController FullscreenEffects =>
        fullscreenEffects as IPlayerFullscreenEffectController;

    [Header("Audio")]
    [SerializeField] private AudioCue inventoryToggleCue;

    [Header("Texts")]
    public TextMeshProUGUI ammoText;
    public TextMeshProUGUI staminaText;
    public TextMeshProUGUI EnegyText;
    public TextMeshProUGUI CommandPointText;
    public TextMeshProUGUI PartyCommandText;
    public TextMeshProUGUI HPText;

    [Header("Vital HUD")]
    [SerializeField] private Image characterIconImage;
    [SerializeField] private Image hpFillImage;
    [SerializeField] private Image energyFillImage;
    [SerializeField] private Image staminaFillImage;
    [SerializeField] private float vitalHudRefreshInterval = 0.1f;

    [Header("Ally Vital HUD")]
    [SerializeField] private GameObject ally1VitalRoot;
    [SerializeField] private Image ally1IconImage;
    [SerializeField] private Image ally1HpFillImage;
    [SerializeField] private Image ally1EnergyFillImage;
    [SerializeField] private GameObject ally2VitalRoot;
    [SerializeField] private Image ally2IconImage;
    [SerializeField] private Image ally2HpFillImage;
    [SerializeField] private Image ally2EnergyFillImage;

    AllyContext ally1Context;
    AllyContext ally2Context;
    float vitalHudRefreshTimer;
    Canvas hudCanvas;
    bool hudVisibleBeforeCutscene = true;
    bool hudHiddenByCutscene;

    [Header("Status Effects")]
    [SerializeField] private StatusEffectGridUI buffGridUI;
    [SerializeField] private StatusEffectGridUI debuffGridUI;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[UI] Multiple UIManager instances; ignoring extra.", this);
        }
        else
        {
            Instance = this;
        }

        ResolveReferences();
        BindStatusEffectUI();

        if (!ammoText) Debug.LogWarning("[UI] ammoText not assigned");
        if (!staminaText) Debug.LogWarning("[UI] staminaText not assigned");
    }

    void Start()
    {
        ResolveReferences();

        if (ctx && ctx.StaminaSystem != null)
        {
            ctx.StaminaSystem.OnStaminaChanged -= UpdateStamina;
            ctx.StaminaSystem.OnStaminaChanged += UpdateStamina;
            UpdateStamina(ctx.StaminaSystem.Current, ctx.StaminaSystem.Max);
        }

        if (ctx is PlayerContext playerContext && playerContext.partyCommand != null)
        {
            UpdateCommandPointText(
                playerContext.partyCommand.CurrentCommandPoints,
                playerContext.partyCommand.MaximumCommandPoints);
            UpdatePartyCommandText(playerContext.partyCommand.GetSelectedPartyCommandUiLabel());
        }

        if (ctx && ctx.HealthSystem != null)
            UpdateHPText(ctx.HealthSystem.currentHealth, ctx.HealthSystem.maximumHealth);

        if (ctx && ctx.EnegySystem != null)
            UpdateEnegyText(ctx.EnegySystem.CurrentEnergy, ctx.EnegySystem.MaximumEnergy);

        RefreshCharacterIcon();
        ResolveAllyHudContexts();
        RefreshAllyVitalHuds();

        BindStatusEffectUI();
    }

    void Update()
    {
        vitalHudRefreshTimer += Time.unscaledDeltaTime;
        if (vitalHudRefreshTimer < vitalHudRefreshInterval)
            return;

        vitalHudRefreshTimer = 0f;
        RefreshPlayerVitalHud();
        ResolveAllyHudContexts();
        RefreshAllyVitalHuds();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (ctx && ctx.StaminaSystem != null)
            ctx.StaminaSystem.OnStaminaChanged -= UpdateStamina;

        if (ctx && ctx.UIManager == this)
            ctx.UIManager = null;
    }

    void ResolveReferences()
    {
        if (!ctx) ctx = FindAnyObjectByType<PlayerContext>();
        if (!ctx) ctx = FindAnyObjectByType<CharacteContext>();

        ctx?.ResolveReferences();

        if (ctx && ctx.UIManager != this)
            ctx.UIManager = this;

        if (!statusEffectController && ctx)
            statusEffectController = ctx.GetComponentInChildren<StatusEffectController>(true);

        if (!playerHudRoot)
        {
            Transform playerHud = transform.Find("PlayerHUD");
            if (playerHud)
                playerHudRoot = playerHud.gameObject;
        }

        if (!fullscreenEffects)
            fullscreenEffects = ResolveFullscreenEffectBehaviour(playerHudRoot);

        RefreshCharacterIcon();
    }

    static MonoBehaviour ResolveFullscreenEffectBehaviour(GameObject root)
    {
        if (!root)
            return null;

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour is IPlayerFullscreenEffectController)
                return behaviour;
        }

        return null;
    }

    void BindStatusEffectUI()
    {
        if (buffGridUI)
        {
            buffGridUI.SetCategory(StatusEffectCategory.Buff);
            buffGridUI.Bind(statusEffectController);
        }

        if (debuffGridUI)
        {
            debuffGridUI.SetCategory(StatusEffectCategory.Debuff);
            debuffGridUI.Bind(statusEffectController);
        }
    }

    public void UpdateAmmoText(int currentAmmo, int maxAmmo)
    {
        UpdateAmmoText(currentAmmo, maxAmmo, -1, false);
    }

    public void UpdateAmmoText(int currentAmmo, int maxAmmo, int reserveAmmo, bool infiniteReserveAmmo)
    {
        if (!ammoText) return;

        bool showReserve = infiniteReserveAmmo || reserveAmmo >= 0;
        string reserveText = infiniteReserveAmmo ? "INF" : reserveAmmo.ToString();
        ammoText.text = showReserve
            ? $"{currentAmmo}/{maxAmmo} | {reserveText}"
            : $"{currentAmmo}/{maxAmmo}";

        bool totalEmpty = currentAmmo <= 0 && !infiniteReserveAmmo && reserveAmmo <= 0;
        ammoText.color = totalEmpty ? Color.red : currentAmmo <= 0 ? Color.yellow : Color.white;
    }

    public void UpdateStamina(float currentStamina, float maxStamina)
    {
        SetFillAmount(staminaFillImage, currentStamina, maxStamina);

        if (staminaText)
            staminaText.text = $"{currentStamina:0}/{maxStamina:0}";
    }

    public void UpdateEnegyText(float currentEnegyText, float maxEnegyText)
    {
        SetFillAmount(energyFillImage, currentEnegyText, maxEnegyText);

        if (EnegyText)
            EnegyText.text = $"{currentEnegyText:0}/{maxEnegyText:0}";
    }

    public void UpdateCommandPointText(float currentCommandPointText, float maxCommandPointText)
    {
        if (!CommandPointText) return;
        CommandPointText.text = $"{currentCommandPointText:0.##}/{maxCommandPointText:0.##}";
    }

    public void UpdatePartyCommandText(string partyCommandLabel)
    {
        if (!PartyCommandText) return;
        PartyCommandText.text = string.IsNullOrWhiteSpace(partyCommandLabel)
            ? "Party Command: none"
            : partyCommandLabel;
    }

    public void UpdateHPText(float currentHPText, float maxHPText)
    {
        SetFillAmount(hpFillImage, currentHPText, maxHPText);

        if (HPText)
            HPText.text = $"{currentHPText:0}/{maxHPText:0}";
    }

    public bool PlayHealFullscreenEffect(float amount, float currentHealth, float maximumHealth)
    {
        IPlayerFullscreenEffectController effectController = FullscreenEffects;
        return effectController != null && effectController.PlayHeal(amount, currentHealth, maximumHealth);
    }

    public bool PlayPerfectDodgeFullscreenEffect(Vector3 worldDashDirection, float slowDuration, float slowScale)
    {
        IPlayerFullscreenEffectController effectController = FullscreenEffects;
        return effectController != null && effectController.PlayPerfectDodge(worldDashDirection, slowDuration, slowScale);
    }

    public void SetHudVisible(bool visible)
    {
        if (!ResolveHudCanvas())
            return;

        if (!visible)
        {
            if (hudHiddenByCutscene)
                return;

            hudVisibleBeforeCutscene = hudCanvas.enabled;
            hudHiddenByCutscene = true;
            hudCanvas.enabled = false;
            return;
        }

        if (!hudHiddenByCutscene)
            return;

        hudHiddenByCutscene = false;
        hudCanvas.enabled = hudVisibleBeforeCutscene;
    }

    bool ResolveHudCanvas()
    {
        if (hudCanvas != null)
            return true;

        if (!playerHudRoot)
            return false;

        if (!playerHudRoot.TryGetComponent(out hudCanvas))
            hudCanvas = playerHudRoot.GetComponentInParent<Canvas>();

        return hudCanvas != null;
    }

    void RefreshCharacterIcon()
    {
        if (!characterIconImage)
            return;

        Sprite icon = ctx != null && ctx.baseStats != null ? ctx.baseStats.icon : null;
        characterIconImage.sprite = icon;
        characterIconImage.enabled = icon != null;
    }

    void RefreshPlayerVitalHud()
    {
        if (ctx == null)
            return;

        ctx.ResolveReferences();

        if (ctx.HealthSystem != null)
            SetFillAmount(hpFillImage, ctx.HealthSystem.currentHealth, ctx.HealthSystem.maximumHealth);

        if (ctx.EnegySystem != null)
            SetFillAmount(energyFillImage, ctx.EnegySystem.CurrentEnergy, ctx.EnegySystem.MaximumEnergy);

        if (ctx.StaminaSystem != null)
            SetFillAmount(staminaFillImage, ctx.StaminaSystem.Current, ctx.StaminaSystem.Max);

        RefreshCharacterIcon();
    }

    void ResolveAllyHudContexts()
    {
        AllyContext resolvedAlly1 = null;
        AllyContext resolvedAlly2 = null;

        if (ctx is PlayerContext playerContext)
        {
            playerContext.ResolveReferences();
            FieldAllyManager fieldAllyManager = playerContext.fieldAllyManager;
            if (fieldAllyManager != null)
            {
                if (fieldAllyManager.TryGetMember(ChainActorRole.PartySlot1, out FieldAllyMember member1))
                    resolvedAlly1 = member1.ActorContext as AllyContext;

                if (fieldAllyManager.TryGetMember(ChainActorRole.PartySlot2, out FieldAllyMember member2))
                    resolvedAlly2 = member2.ActorContext as AllyContext;
            }
        }

        ResolveAllyContextsFromHierarchy(ref resolvedAlly1, ref resolvedAlly2);

        ally1Context = resolvedAlly1;
        ally2Context = resolvedAlly2;
    }

    void ResolveAllyContextsFromHierarchy(ref AllyContext resolvedAlly1, ref AllyContext resolvedAlly2)
    {
        Transform searchRoot = ctx != null ? ctx.transform.root : transform.root;
        if (searchRoot == null)
            return;

        AllyContext[] allies = searchRoot.GetComponentsInChildren<AllyContext>(true);
        for (int i = 0; i < allies.Length; i++)
        {
            AllyContext ally = allies[i];
            if (ally == null || IsHelperAlly(ally))
                continue;

            FieldAllyMember member = ally.GetComponentInChildren<FieldAllyMember>(true);
            if (member != null)
            {
                if (member.ActorRole == ChainActorRole.PartySlot1 && resolvedAlly1 == null)
                {
                    resolvedAlly1 = ally;
                    continue;
                }

                if (member.ActorRole == ChainActorRole.PartySlot2 && resolvedAlly2 == null)
                {
                    resolvedAlly2 = ally;
                    continue;
                }
            }

            if (resolvedAlly1 == null)
                resolvedAlly1 = ally;
            else if (resolvedAlly2 == null && ally != resolvedAlly1)
                resolvedAlly2 = ally;
        }
    }

    static bool IsHelperAlly(AllyContext ally)
    {
        FieldAllyMember member = ally.GetComponentInChildren<FieldAllyMember>(true);
        if (member != null && member.ActorRole == ChainActorRole.Helper)
            return true;

        return ally.name.IndexOf("Helper", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void RefreshAllyVitalHuds()
    {
        RefreshVitalHud(ally1Context, ally1VitalRoot, ally1IconImage, ally1HpFillImage, ally1EnergyFillImage);
        RefreshVitalHud(ally2Context, ally2VitalRoot, ally2IconImage, ally2HpFillImage, ally2EnergyFillImage);
    }

    static void RefreshVitalHud(
        CharacteContext targetContext,
        GameObject root,
        Image iconImage,
        Image hpFill,
        Image energyFill)
    {
        bool hasContext = targetContext != null;
        if (root != null && root.activeSelf != hasContext)
            root.SetActive(hasContext);

        if (!hasContext)
        {
            if (iconImage)
                iconImage.enabled = false;

            SetFillAmount(hpFill, 0f, 1f);
            SetFillAmount(energyFill, 0f, 1f);
            return;
        }

        targetContext.ResolveReferences();

        if (iconImage)
        {
            Sprite icon = targetContext.baseStats != null ? targetContext.baseStats.icon : null;
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        HealthSystem health = targetContext.HealthSystem;
        if (health == null)
            health = targetContext.GetComponentInChildren<HealthSystem>(true);

        if (health != null)
            SetFillAmount(hpFill, health.currentHealth, health.maximumHealth);
        else
            SetFillAmount(hpFill, 0f, 1f);

        SkillUserSystem energy = targetContext.EnegySystem;
        if (energy == null)
            energy = targetContext.GetComponentInChildren<SkillUserSystem>(true);

        if (energy != null)
            SetFillAmount(energyFill, energy.CurrentEnergy, energy.MaximumEnergy);
        else
            SetFillAmount(energyFill, 0f, 1f);
    }

    static void SetFillAmount(Image fillImage, float current, float maximum)
    {
        if (!fillImage)
            return;

        float ratio = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;

        fillImage.type = Image.Type.Simple;
        fillImage.fillAmount = 1f;
        fillImage.enabled = ratio > 0f;

        RectTransform fillRect = fillImage.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(ratio, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
    }

    public void OnInventoryPress(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed)
            return;

        if (Inventory == null)
        {
            Debug.LogWarning("[UI] Inventory not assigned");
            return;
        }

        Inventory.SetActive(!Inventory.activeSelf);
        if (inventoryToggleCue != null)
            AudioService.Instance.Play(inventoryToggleCue);
    }
}
