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

    HealthSystem _ally1Health;
    SkillUserSystem _ally1Energy;
    HealthSystem _ally2Health;
    SkillUserSystem _ally2Energy;
    FieldAllyManager _fieldAllyManager;
    PartyCommandLabelData _partyCommandLabelData;
    bool _partyCommandCooldownActive;
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
        BindRuntimeSources();
    }

    void Update()
    {
        if (!_partyCommandCooldownActive)
            return;

        if (_partyCommandLabelData.CooldownReadyTime <= Time.time)
            _partyCommandCooldownActive = false;

        FormatPartyCommandLabel();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        UnbindRuntimeSources();

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

    static bool IsHelperAlly(AllyContext ally)
    {
        FieldAllyMember member = ally.GetComponentInChildren<FieldAllyMember>(true);
        if (member != null && member.ActorRole == ChainActorRole.Helper)
            return true;

        return ally.name.IndexOf("Helper", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void BindRuntimeSources()
    {
        if (!ctx)
            return;

        ctx.ResolveReferences();

        if (ctx.HealthSystem != null)
        {
            ctx.HealthSystem.HealthChanged -= OnHealthChanged;
            ctx.HealthSystem.HealthChanged += OnHealthChanged;
            ctx.HealthSystem.Healed -= OnHealed;
            ctx.HealthSystem.Healed += OnHealed;
            UpdateHPText(ctx.HealthSystem.currentHealth, ctx.HealthSystem.maximumHealth);
        }

        if (ctx.EnegySystem != null)
        {
            ctx.EnegySystem.EnergyChanged -= OnEnergyChanged;
            ctx.EnegySystem.EnergyChanged += OnEnergyChanged;
            UpdateEnegyText(ctx.EnegySystem.CurrentEnergy, ctx.EnegySystem.MaximumEnergy);
        }

        if (ctx.StaminaSystem != null)
        {
            ctx.StaminaSystem.OnStaminaChanged -= UpdateStamina;
            ctx.StaminaSystem.OnStaminaChanged += UpdateStamina;
            UpdateStamina(ctx.StaminaSystem.Current, ctx.StaminaSystem.Max);
        }

        if (ctx.WeaponSystem != null)
        {
            ctx.WeaponSystem.AmmoChanged -= OnAmmoChanged;
            ctx.WeaponSystem.AmmoChanged += OnAmmoChanged;
            var ws = ctx.WeaponSystem;
            UpdateAmmoText(ws.CurrentAmmo, ws.MagazineSize, ws.CurrentReserveAmmo, ws.HasInfiniteReserveAmmo);
        }

        if (ctx is PlayerContext playerCtx)
        {
            if (playerCtx.partyCommand != null)
            {
                playerCtx.partyCommand.CommandPointsChanged -= OnCommandPointsChanged;
                playerCtx.partyCommand.CommandPointsChanged += OnCommandPointsChanged;
                playerCtx.partyCommand.PartyCommandLabelChanged -= OnPartyCommandLabelChanged;
                playerCtx.partyCommand.PartyCommandLabelChanged += OnPartyCommandLabelChanged;
                UpdateCommandPointText(playerCtx.partyCommand.CurrentCommandPoints, playerCtx.partyCommand.MaximumCommandPoints);
            }

            if (playerCtx.fieldAllyManager != null)
            {
                _fieldAllyManager = playerCtx.fieldAllyManager;
                _fieldAllyManager.MemberRegistered -= OnAllyRegistered;
                _fieldAllyManager.MemberRegistered += OnAllyRegistered;
                _fieldAllyManager.MemberUnregistered -= OnAllyUnregistered;
                _fieldAllyManager.MemberUnregistered += OnAllyUnregistered;
                BindAllySlotFromManager(ChainActorRole.PartySlot1);
                BindAllySlotFromManager(ChainActorRole.PartySlot2);
            }
            else
            {
                UnbindAllySlot(ChainActorRole.PartySlot1);
                UnbindAllySlot(ChainActorRole.PartySlot2);
            }

            if (playerCtx.DashSystem != null)
                playerCtx.DashSystem.PerfectDodgeHandler = OnPerfectDodge;
        }

        RefreshCharacterIcon();
        BindStatusEffectUI();
    }

    void UnbindRuntimeSources()
    {
        UnbindAllySlot(ChainActorRole.PartySlot1);
        UnbindAllySlot(ChainActorRole.PartySlot2);

        if (_fieldAllyManager != null)
        {
            _fieldAllyManager.MemberRegistered -= OnAllyRegistered;
            _fieldAllyManager.MemberUnregistered -= OnAllyUnregistered;
            _fieldAllyManager = null;
        }

        if (ctx)
        {
            if (ctx.HealthSystem != null)
            {
                ctx.HealthSystem.HealthChanged -= OnHealthChanged;
                ctx.HealthSystem.Healed -= OnHealed;
            }

            if (ctx.EnegySystem != null)
                ctx.EnegySystem.EnergyChanged -= OnEnergyChanged;

            if (ctx.StaminaSystem != null)
                ctx.StaminaSystem.OnStaminaChanged -= UpdateStamina;

            if (ctx.WeaponSystem != null)
                ctx.WeaponSystem.AmmoChanged -= OnAmmoChanged;

            if (ctx is PlayerContext playerCtx)
            {
                if (playerCtx.partyCommand != null)
                {
                    playerCtx.partyCommand.CommandPointsChanged -= OnCommandPointsChanged;
                    playerCtx.partyCommand.PartyCommandLabelChanged -= OnPartyCommandLabelChanged;
                }

                if (playerCtx.DashSystem != null)
                    playerCtx.DashSystem.PerfectDodgeHandler = null;
            }
        }
    }

    void OnHealthChanged(float current, float max) => UpdateHPText(current, max);

    void OnHealed(float amount, float current, float max) => PlayHealFullscreenEffect(amount, current, max);

    void OnEnergyChanged(float current, float max) => UpdateEnegyText(current, max);

    void OnAmmoChanged(int magazine, int maxMagazine, int reserveAmmo, bool infiniteReserve)
    {
        UpdateAmmoText(magazine, maxMagazine, infiniteReserve ? -1 : reserveAmmo, infiniteReserve);
    }

    void OnCommandPointsChanged(float current, float max) => UpdateCommandPointText(current, max);

    void OnPartyCommandLabelChanged(PartyCommandLabelData data)
    {
        _partyCommandLabelData = data;
        _partyCommandCooldownActive = data.BlockReason == PartyCommandBlockReason.Cooldown && data.CooldownReadyTime > Time.time;
        FormatPartyCommandLabel();
    }

    void FormatPartyCommandLabel()
    {
        if (_partyCommandLabelData.CommandName == null)
            return;

        string label;
        if (_partyCommandLabelData.BlockReason == PartyCommandBlockReason.MissingConfig)
        {
            label = "Party Command: none";
        }
        else
        {
            label = $"{_partyCommandLabelData.CommandName} | {_partyCommandLabelData.CommandPointCost:0.##} CP";
            string blockLabel = FormatBlockReason(_partyCommandLabelData.BlockReason, _partyCommandLabelData.CooldownReadyTime);
            if (blockLabel != null)
                label = $"{label} [{blockLabel}]";
        }

        UpdatePartyCommandText(label);
    }

    static string FormatBlockReason(PartyCommandBlockReason reason, float cooldownReadyTime)
    {
        return reason switch
        {
            PartyCommandBlockReason.None => null,
            PartyCommandBlockReason.NotEnoughCommandPoints => "No CP",
            PartyCommandBlockReason.Cooldown => $"Cooldown {Mathf.Max(0f, cooldownReadyTime - Time.time):0.0}s",
            PartyCommandBlockReason.OwnerUnavailable => "Owner Down",
            PartyCommandBlockReason.HelperUnavailable => "Helper Offline",
            PartyCommandBlockReason.HelperBusy => "Helper Busy",
            PartyCommandBlockReason.AllyUnavailable => "Ally Offline",
            PartyCommandBlockReason.AllyBusy => "Ally Busy",
            PartyCommandBlockReason.SkillUnavailable => "No Skill",
            PartyCommandBlockReason.SkillBlocked => "Skill Blocked",
            PartyCommandBlockReason.SequenceUnavailable => "Ally Offline",
            PartyCommandBlockReason.SequenceBusy => "Chain Busy",
            PartyCommandBlockReason.MissingTarget => "No Target",
            _ => "Invalid",
        };
    }

    bool OnPerfectDodge(Vector3 direction, float duration, float scale)
    {
        return PlayPerfectDodgeFullscreenEffect(direction, duration, scale);
    }

    void OnAllyRegistered(ChainActorRole role, FieldAllyMember member) => BindAllySlot(role, member);

    void OnAllyUnregistered(ChainActorRole role) => UnbindAllySlot(role);

    void BindAllySlotFromManager(ChainActorRole role)
    {
        if (_fieldAllyManager != null && _fieldAllyManager.TryGetMember(role, out FieldAllyMember member))
            BindAllySlot(role, member);
        else
            UnbindAllySlot(role);
    }

    void BindAllySlot(ChainActorRole role, FieldAllyMember member)
    {
        AllyContext allyCtx = member != null ? member.ActorContext as AllyContext : null;
        if (allyCtx != null && IsHelperAlly(allyCtx))
            allyCtx = null;

        UnbindAllySlot(role);

        if (allyCtx == null)
            return;

        allyCtx.ResolveReferences();
        HealthSystem health = allyCtx.HealthSystem;
        SkillUserSystem energy = allyCtx.EnegySystem;
        GameObject root;
        Image iconImage, hpFill, energyFill;

        if (role == ChainActorRole.PartySlot1)
        {
            _ally1Health = health;
            _ally1Energy = energy;
            if (health != null) health.HealthChanged += OnAlly1HealthChanged;
            if (energy != null) energy.EnergyChanged += OnAlly1EnergyChanged;
            root = ally1VitalRoot; iconImage = ally1IconImage;
            hpFill = ally1HpFillImage; energyFill = ally1EnergyFillImage;
        }
        else if (role == ChainActorRole.PartySlot2)
        {
            _ally2Health = health;
            _ally2Energy = energy;
            if (health != null) health.HealthChanged += OnAlly2HealthChanged;
            if (energy != null) energy.EnergyChanged += OnAlly2EnergyChanged;
            root = ally2VitalRoot; iconImage = ally2IconImage;
            hpFill = ally2HpFillImage; energyFill = ally2EnergyFillImage;
        }
        else
        {
            return;
        }

        if (root) root.SetActive(true);
        if (iconImage)
        {
            Sprite icon = allyCtx.baseStats != null ? allyCtx.baseStats.icon : null;
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }
        SetFillAmount(hpFill, health != null ? health.currentHealth : 0f,
            health != null ? health.maximumHealth : 1f);
        SetFillAmount(energyFill, energy != null ? energy.CurrentEnergy : 0f,
            energy != null ? energy.MaximumEnergy : 1f);
    }

    void UnbindAllySlot(ChainActorRole role)
    {
        if (role == ChainActorRole.PartySlot1)
        {
            if (_ally1Health != null) _ally1Health.HealthChanged -= OnAlly1HealthChanged;
            if (_ally1Energy != null) _ally1Energy.EnergyChanged -= OnAlly1EnergyChanged;
            _ally1Health = null;
            _ally1Energy = null;
            if (ally1VitalRoot) ally1VitalRoot.SetActive(false);
        }
        else if (role == ChainActorRole.PartySlot2)
        {
            if (_ally2Health != null) _ally2Health.HealthChanged -= OnAlly2HealthChanged;
            if (_ally2Energy != null) _ally2Energy.EnergyChanged -= OnAlly2EnergyChanged;
            _ally2Health = null;
            _ally2Energy = null;
            if (ally2VitalRoot) ally2VitalRoot.SetActive(false);
        }
    }

    void OnAlly1HealthChanged(float current, float max) => SetFillAmount(ally1HpFillImage, current, max);
    void OnAlly1EnergyChanged(float current, float max) => SetFillAmount(ally1EnergyFillImage, current, max);
    void OnAlly2HealthChanged(float current, float max) => SetFillAmount(ally2HpFillImage, current, max);
    void OnAlly2EnergyChanged(float current, float max) => SetFillAmount(ally2EnergyFillImage, current, max);

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
