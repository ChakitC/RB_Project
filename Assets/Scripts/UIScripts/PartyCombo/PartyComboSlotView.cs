using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PartyComboSlotView : MonoBehaviour
{
    [SerializeField] ChainActorRole role = ChainActorRole.PartySlot1;
    [Header("Opportunity Popup")]
    [SerializeField] GameObject root;
    [SerializeField] RectTransform animatedRoot;
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Image icon;
    [SerializeField] Image expiryFill;
    [SerializeField] TMP_Text fallbackLabel;
    [SerializeField] TMP_Text skillNameLabel;
    [SerializeField] TMP_Text disabledReasonLabel;

    [Header("Party HUD Cooldown")]
    [SerializeField] GameObject cooldownRoot;
    [SerializeField] Image cooldownIcon;
    [SerializeField] Image cooldownFill;
    [SerializeField] Image cooldownReadyGlow;
    [SerializeField] TMP_Text cooldownLabel;

    [Header("Motion")]
    [SerializeField, Min(0.01f)] float entranceDuration = 0.18f;
    [SerializeField, Range(0.5f, 1f)] float entranceScale = 0.82f;
    [SerializeField, Min(0f)] float readyPulseAmount = 0.025f;
    [SerializeField, Min(0f)] float readyPulseSpeed = 5f;

    PartyComboOpportunityController controller;
    PartyComboOfferViewData offer;
    bool hasOffer;
    bool entering;
    float entranceElapsed;
    int presentedOfferId;

    public ChainActorRole Role => role;

    public void Bind(PartyComboOpportunityController source)
    {
        controller = source;
        Clear();
        UpdateCooldown();
    }

    public void Apply(in PartyComboOfferViewData view)
    {
        if (view.OwnerRole != role)
            return;

        offer = view;
        hasOffer = view.State == PartyComboOpportunityState.Offered ||
                   view.State == PartyComboOpportunityState.Starting ||
                   view.State == PartyComboOpportunityState.Executing;
        SetVisible(hasOffer);
        if (!hasOffer)
        {
            UpdateCooldown();
            return;
        }

        if (view.OfferId != presentedOfferId)
        {
            presentedOfferId = view.OfferId;
            BeginEntrance();
        }

        Sprite sprite = view.Definition != null ? view.Definition.ResolvedIcon : null;
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.gameObject.SetActive(sprite != null);
        }
        if (fallbackLabel != null)
        {
            fallbackLabel.gameObject.SetActive(sprite == null);
            fallbackLabel.text = view.Definition != null ? view.Definition.displayName : role.ToString();
        }
        if (skillNameLabel != null)
            skillNameLabel.text = view.Definition != null ? view.Definition.displayName : role.ToString();
        if (disabledReasonLabel != null)
        {
            bool disabled = view.State != PartyComboOpportunityState.Offered;
            disabledReasonLabel.gameObject.SetActive(disabled);
            disabledReasonLabel.text = disabled ? FormatState(view.State) : string.Empty;
        }
        UpdateCountdown();
        UpdateCooldown();
    }

    public void Clear()
    {
        hasOffer = false;
        offer = default;
        entering = false;
        entranceElapsed = 0f;
        presentedOfferId = 0;
        ResetMotion();
        SetVisible(false);
        SetCooldownVisible(false);
    }

    void LateUpdate()
    {
        if (hasOffer)
        {
            UpdateCountdown();
            UpdateMotion();
        }

        UpdateCooldown();
    }

    void UpdateCountdown()
    {
        if (expiryFill == null || controller == null || offer.Definition == null)
            return;

        float duration = Mathf.Max(0.1f, offer.Definition.offerDurationSeconds);
        float remaining = Mathf.Max(0f, offer.ExpiresAt - controller.ComboClockNow);
        expiryFill.fillAmount = Mathf.Clamp01(remaining / duration);
    }

    void UpdateCooldown()
    {
        if (controller == null || !controller.RuntimeFeatureEnabled ||
            !controller.TryGetComboChargeStatus(
                role,
                out PartyComboSkillDef definition,
                out SkillChargeStatus status))
        {
            SetCooldownVisible(false);
            return;
        }

        SetCooldownVisible(true);
        bool recharging = status.IsRecharging && status.Available < status.Max;

        if (cooldownFill != null)
            cooldownFill.fillAmount = recharging ? status.NextChargeProgress01 : 1f;
        if (cooldownLabel != null)
            cooldownLabel.text = recharging
                ? $"{status.NextChargeRemaining:0.0}s"
                : "READY";
        if (cooldownReadyGlow != null)
        {
            Color color = cooldownReadyGlow.color;
            color.a = recharging
                ? 0f
                : 0.16f + Mathf.Sin(Time.unscaledTime * 4f) * 0.06f;
            cooldownReadyGlow.color = color;
        }

        Sprite sprite = definition != null ? definition.ResolvedIcon : null;
        if (cooldownIcon != null)
        {
            cooldownIcon.sprite = sprite;
            cooldownIcon.gameObject.SetActive(sprite != null);
        }
    }

    void BeginEntrance()
    {
        entering = true;
        entranceElapsed = 0f;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        if (animatedRoot != null)
            animatedRoot.localScale = new Vector3(entranceScale, entranceScale, 1f);
    }

    void UpdateMotion()
    {
        if (animatedRoot == null)
            return;

        if (entering)
        {
            entranceElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(entranceElapsed / entranceDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float scale = Mathf.Lerp(entranceScale, 1f, eased);
            animatedRoot.localScale = new Vector3(scale, scale, 1f);
            if (canvasGroup != null)
                canvasGroup.alpha = eased;
            entering = t < 1f;
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * readyPulseSpeed) * readyPulseAmount;
        animatedRoot.localScale = new Vector3(pulse, pulse, 1f);
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    void ResetMotion()
    {
        if (animatedRoot != null)
            animatedRoot.localScale = Vector3.one;
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    void SetCooldownVisible(bool visible)
    {
        if (cooldownRoot != null && cooldownRoot.activeSelf != visible)
            cooldownRoot.SetActive(visible);
    }

    static string FormatState(PartyComboOpportunityState state)
    {
        return state switch
        {
            PartyComboOpportunityState.Starting => "STARTING",
            PartyComboOpportunityState.Executing => "EXECUTING",
            _ => state.ToString().ToUpperInvariant(),
        };
    }

    void SetVisible(bool visible)
    {
        GameObject target = root != null ? root : gameObject;
        if (target != gameObject && target.activeSelf != visible)
            target.SetActive(visible);
    }
}
