using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Combat HUD readout for one command slot's charges: "available/max" plus a fill showing the
/// recharge in flight. A skill with a single charge shows a plain cooldown, which is why this can
/// sit on every slot rather than only on multi-charge skills.
/// </summary>
[DisallowMultipleComponent]
public sealed class ActiveSkillChargePresenter : MonoBehaviour
{
    [Header("Slot")]
    [SerializeField, Min(0)]
    [Tooltip("Index into CharacterSkillManager.CommandSlots.")]
    private int commandSlotIndex;

    [Header("View")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text chargeLabel;
    [SerializeField] private Image cooldownFill;
    [SerializeField] private CanvasGroup availabilityGroup;

    [Header("Display")]
    [SerializeField, Tooltip("Hide the label entirely while the skill only has one charge.")]
    private bool hideLabelForSingleCharge = true;

    [SerializeField, Range(0f, 1f)]
    private float unavailableAlpha = 0.4f;

    [SerializeField, Min(0f)]
    [Tooltip("Seconds between charge re-reads. Resolving charge status rebuilds the skill's final " +
             "stats, which is far too expensive to do every frame on every slot.")]
    private float refreshInterval = 0.1f;

    CharacterSkillManager skillManager;

    float nextRefreshTime;
    int lastAvailable = -1;
    int lastMax = -1;
    float lastFillAmount = -1f;
    float lastAlpha = -1f;
    bool lastLabelVisible;
    bool lastFillVisible;
    bool hasState;

    public void Bind(CharacteContext context)
    {
        skillManager = null;

        if (context != null)
        {
            context.ResolveReferences();
            skillManager = context.SkillManager;
        }

        ResetCachedState();
        ApplyVisibility(skillManager != null);
        Refresh();
    }

    void OnEnable()
    {
        ResetCachedState();
    }

    void LateUpdate()
    {
        // Throttled: TryGetChargeStatus rebuilds FinalSkillStats, so running it per slot per
        // frame is pure waste. A charge readout does not need frame-rate resolution.
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + Mathf.Max(0f, refreshInterval);
        Refresh();
    }

    void ResetCachedState()
    {
        nextRefreshTime = 0f;
        lastAvailable = -1;
        lastMax = -1;
        lastFillAmount = -1f;
        lastAlpha = -1f;
        hasState = false;
    }

    void Refresh()
    {
        // Reads the manager's shared pool — the same one the cast path spends from — so two slots
        // holding the same skill always agree.
        if (skillManager == null ||
            !skillManager.TryGetSlotChargeStatus(commandSlotIndex, out SkillChargeStatus status))
        {
            ApplyVisibility(false);
            return;
        }

        ApplyVisibility(true);

        if (chargeLabel != null)
        {
            bool showLabel = !hideLabelForSingleCharge || status.Max > 1;
            if (!hasState || showLabel != lastLabelVisible)
            {
                chargeLabel.gameObject.SetActive(showLabel);
                lastLabelVisible = showLabel;
            }

            if (showLabel && (status.Available != lastAvailable || status.Max != lastMax))
                chargeLabel.text = $"{status.Available}/{status.Max}";
        }

        lastAvailable = status.Available;
        lastMax = status.Max;

        if (cooldownFill != null)
        {
            if (!hasState || status.IsRecharging != lastFillVisible)
            {
                cooldownFill.gameObject.SetActive(status.IsRecharging);
                lastFillVisible = status.IsRecharging;
            }

            // The fill tracks only the charge currently recharging, so a partially spent pool
            // still shows meaningful progress rather than sitting empty.
            // Writing fillAmount dirties the Graphic and forces a canvas rebuild, so only write
            // it when the value actually moved.
            float fill = status.IsRecharging ? 1f - status.NextChargeProgress01 : 0f;
            if (!Mathf.Approximately(fill, lastFillAmount))
            {
                cooldownFill.fillAmount = fill;
                lastFillAmount = fill;
            }
        }

        if (availabilityGroup != null)
        {
            float alpha = status.HasCharge ? 1f : unavailableAlpha;
            if (!Mathf.Approximately(alpha, lastAlpha))
            {
                availabilityGroup.alpha = alpha;
                lastAlpha = alpha;
            }
        }

        hasState = true;
    }

    void ApplyVisibility(bool visible)
    {
        GameObject target = root != null ? root : gameObject;

        // The activeSelf guard is the one that matters: a redundant SetActive on a UI object
        // dirties the canvas and forces a rebuild.
        if (target != gameObject && target.activeSelf != visible)
            target.SetActive(visible);
    }
}
