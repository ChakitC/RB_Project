using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private StatusEffectController statusEffectController;

    public GameObject Inventory;

    [Header("Audio")]
    [SerializeField] private AudioCue inventoryToggleCue;

    [Header("Texts")]
    public TextMeshProUGUI ammoText;
    public TextMeshProUGUI staminaText;
    public TextMeshProUGUI EnegyText;
    public TextMeshProUGUI HPText;

    [Header("Status Effects")]
    [SerializeField] private StatusEffectGridUI buffGridUI;
    [SerializeField] private StatusEffectGridUI debuffGridUI;

    void Awake()
    {
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

        BindStatusEffectUI();
    }

    void OnDestroy()
    {
        if (ctx && ctx.StaminaSystem != null)
            ctx.StaminaSystem.OnStaminaChanged -= UpdateStamina;
    }

    void ResolveReferences()
    {
        if (!ctx) ctx = FindAnyObjectByType<CharacteContext>();

        if (!statusEffectController && ctx)
            statusEffectController = ctx.GetComponent<StatusEffectController>();
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
        if (!ammoText) return;
        ammoText.text = $"{currentAmmo}/{maxAmmo}";
        ammoText.color = currentAmmo == 0 ? Color.red : Color.white;
    }

    public void UpdateStamina(float currentStamina, float maxStamina)
    {
        if (!staminaText) return;
        staminaText.text = $"{currentStamina:0}/{maxStamina:0}";
    }

    public void UpdateEnegyText(float currentEnegyText, float maxEnegyText)
    {
        if (!EnegyText) return;
        EnegyText.text = $"{currentEnegyText:0}/{maxEnegyText:0}";
    }

    public void UpdateHPText(float currentHPText, float maxHPText)
    {
        if (!HPText) return;
        HPText.text = $"{currentHPText:0}/{maxHPText:0}";
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
