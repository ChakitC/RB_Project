using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Combat HUD readout for one command slot: the assigned skill's icon, the charges still in the
/// pool, and a radial overlay for the recharge in flight. A skill with a single charge shows a
/// plain cooldown, which is why this can sit on every slot rather than only on multi-charge ones.
///
/// Everything shown here is a read of the shared charge pool the cast path spends from — the HUD
/// never runs a timer of its own. It reports cooldown and charges only: energy, animation locks,
/// and cutscene locks deliberately do not darken the slot, because those clear on their own and
/// would make the overlay mean two different things.
///
/// A passive slot has no charge pool to read, so it takes a separate path: icon only, no charge
/// count and no cooldown overlay, with the ready flash reused as the cue for a proc landing.
/// </summary>
[DisallowMultipleComponent]
public sealed class ActiveSkillChargePresenter : MonoBehaviour
{
    /// <summary>Charge counts are single digits in practice; avoid an int.ToString() per update.</summary>
    static readonly string[] ChargeTexts = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

    [Header("Slot")]
    [SerializeField, Min(0)]
    [Tooltip("Index into CharacterSkillManager.CommandSlots.")]
    private int commandSlotIndex;

    [Header("View")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image skillIcon;

    [SerializeField, Tooltip("Stand-in shown when the slot's skill has no icon authored.")]
    private TMP_Text fallbackLabel;

    [SerializeField] private TMP_Text chargeLabel;
    [SerializeField] private Image cooldownFill;

    [SerializeField, Tooltip("White overlay pulsed when the slot becomes usable again.")]
    private Graphic readyFlash;

    [Header("Display")]
    [SerializeField, Range(0f, 1f)]
    [Tooltip("Overlay alpha while a charge is recharging but the slot is still usable.")]
    private float rechargingOverlayAlpha = 0.35f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Overlay alpha once the pool is empty and the slot cannot be used.")]
    private float emptyOverlayAlpha = 0.65f;

    [SerializeField, Min(0f)]
    [Tooltip("Length of the white flash played when the pool goes from empty to usable.")]
    private float readyFlashSeconds = 0.18f;

    [SerializeField, Min(0f)]
    [Tooltip("Seconds between charge re-reads. Resolving charge status rebuilds the skill's final " +
             "stats, which is far too expensive to do every frame on every slot. The overlay is " +
             "extrapolated between reads so the sweep still runs at frame rate.")]
    private float refreshInterval = 0.1f;

    CharacterSkillManager skillManager;
    PassiveController passiveController;

    float nextRefreshTime;
    bool hasStatus;

    // Passive slots keep their own icon cache: the active path caches a SkillGemDefinition, and
    // the two can never be assigned to the same slot at once.
    bool isPassiveSlot;
    PassiveDefinition passiveDef;
    PassiveDefinition lastPassiveDef;
    bool hasPassiveDef;

    // Recharge sampled at sampleTime and extrapolated per frame. Time.time is the pool's own clock,
    // so the sweep stops exactly when the cooldown does — including while the game is paused.
    float sampleTime;
    float sampledRemaining;
    float sampledDuration;

    int lastAvailable = -1;
    SkillGemDefinition lastSkillDef;
    bool hasSkillDef;
    float lastFillAmount = -1f;
    float lastOverlayAlpha = -1f;
    bool lastFillVisible;
    float flashEndTime = -1f;
    float lastFlashAlpha = -1f;

    public void Bind(CharacteContext context)
    {
        skillManager = null;
        SubscribeToPassiveController(null);

        if (context != null)
        {
            context.ResolveReferences();
            skillManager = context.SkillManager;
            SubscribeToPassiveController(context.PassiveController);
        }

        ResetCachedState();
        ApplyVisibility(skillManager != null);
        Refresh();
    }

    void OnEnable()
    {
        ResetCachedState();
    }

    void OnDestroy()
    {
        SubscribeToPassiveController(null);
    }

    void SubscribeToPassiveController(PassiveController controller)
    {
        if (ReferenceEquals(passiveController, controller))
            return;

        if (passiveController != null)
            passiveController.PassiveTriggered -= HandlePassiveTriggered;

        passiveController = controller;

        if (passiveController != null)
            passiveController.PassiveTriggered += HandlePassiveTriggered;
    }

    /// <summary>The proc cue. Every passive on the character reports here, so the slot filters to
    /// the one definition it is showing.</summary>
    void HandlePassiveTriggered(PassiveDefinition definition)
    {
        if (!isPassiveSlot || definition == null || definition != passiveDef)
            return;

        StartReadyFlash();
    }

    void LateUpdate()
    {
        // Throttled: TryGetSlotChargeStatus rebuilds FinalSkillStats, so running it per slot per
        // frame is pure waste. A read is pulled forward the moment the sampled recharge is due, so
        // the charge count and the ready flash still land on time rather than up to a tick late.
        if (Time.unscaledTime >= nextRefreshTime || IsSampledRechargeDue())
        {
            nextRefreshTime = Time.unscaledTime + Mathf.Max(0f, refreshInterval);
            Refresh();
        }

        // A passive slot never fills a cooldown, but it still animates the flash it was just told
        // to start, so it runs its own short tail of the update.
        if (isPassiveSlot)
        {
            UpdateReadyFlash();
            return;
        }

        if (!hasStatus)
            return;

        UpdateCooldownFill();
        UpdateReadyFlash();
    }

    void ResetCachedState()
    {
        nextRefreshTime = 0f;
        hasStatus = false;

        sampleTime = 0f;
        sampledRemaining = 0f;
        sampledDuration = 0f;

        isPassiveSlot = false;
        passiveDef = null;
        lastPassiveDef = null;
        hasPassiveDef = false;

        lastAvailable = -1;
        lastSkillDef = null;
        hasSkillDef = false;
        lastFillAmount = -1f;
        lastOverlayAlpha = -1f;
        lastFillVisible = false;

        flashEndTime = -1f;
        lastFlashAlpha = -1f;
        if (readyFlash != null)
            SetActiveIfNeeded(readyFlash.gameObject, false);
    }

    void Refresh()
    {
        // Reads the manager's shared pool — the same one the cast path spends from — so two slots
        // holding the same skill always agree.
        // Asked first because a passive slot can never answer the charge read below: it has no
        // runtime skill and no pool, so falling through would hide the slot outright.
        if (skillManager != null &&
            skillManager.TryGetSlotPassiveDefinition(commandSlotIndex, out PassiveDefinition passive))
        {
            isPassiveSlot = true;
            passiveDef = passive;
            hasStatus = false;

            ApplyVisibility(true);
            ApplyPassiveIcon(passive);
            return;
        }

        isPassiveSlot = false;
        passiveDef = null;

        if (skillManager == null ||
            !skillManager.TryGetSlotChargeStatus(commandSlotIndex, out SkillChargeStatus status))
        {
            ApplyVisibility(false);
            hasStatus = false;
            return;
        }

        ApplyVisibility(true);

        ApplyIcon();
        ApplyCharges(status);

        sampleTime = Time.time;
        sampledRemaining = status.NextChargeRemaining;
        sampledDuration = status.NextChargeDuration;

        hasStatus = true;
    }

    /// <summary>
    /// The icon only changes when the loadout does, so this compares the resolved definition and
    /// leaves the Image alone otherwise.
    /// </summary>
    void ApplyIcon()
    {
        if (skillIcon == null && fallbackLabel == null)
            return;

        skillManager.TryGetSlotSkillDefinition(commandSlotIndex, out SkillGemDefinition skillDef);

        if (hasSkillDef && skillDef == lastSkillDef)
            return;

        lastSkillDef = skillDef;
        hasSkillDef = true;

        Sprite sprite = skillDef != null ? skillDef.SkillDefinitionIcon : null;

        if (skillIcon != null)
        {
            skillIcon.sprite = sprite;
            SetActiveIfNeeded(skillIcon.gameObject, sprite != null);
        }

        if (fallbackLabel != null)
            SetActiveIfNeeded(fallbackLabel.gameObject, sprite == null);
    }

    /// <summary>
    /// Icon-only readout for a passive: there is nothing to count down and nothing to spend, so
    /// the charge label and the cooldown overlay are switched off rather than left showing a
    /// stale value from a previous loadout.
    /// </summary>
    void ApplyPassiveIcon(PassiveDefinition definition)
    {
        if (hasPassiveDef && definition == lastPassiveDef)
            return;

        lastPassiveDef = definition;
        hasPassiveDef = true;

        Sprite sprite = definition != null ? definition.SkillDefinitionIcon : null;

        if (skillIcon != null)
        {
            skillIcon.sprite = sprite;
            SetActiveIfNeeded(skillIcon.gameObject, sprite != null);
        }

        if (fallbackLabel != null)
            SetActiveIfNeeded(fallbackLabel.gameObject, sprite == null);

        if (chargeLabel != null)
            SetActiveIfNeeded(chargeLabel.gameObject, false);

        if (cooldownFill != null)
        {
            SetActiveIfNeeded(cooldownFill.gameObject, false);
            lastFillVisible = false;
        }
    }

    void ApplyCharges(SkillChargeStatus status)
    {
        if (chargeLabel != null)
        {
            // A usable slot always shows its count, "1" included: the number is what tells the
            // player a second press is available, so hiding it at 1 would hide the useful case.
            bool showLabel = status.Available > 0;
            SetActiveIfNeeded(chargeLabel.gameObject, showLabel);

            if (showLabel && status.Available != lastAvailable)
                chargeLabel.text = GetChargeText(status.Available);
        }

        // The flash marks the slot turning usable again, so it fires only on the empty -> usable
        // edge. Banking a spare charge on top of a usable one is not a state change worth a cue.
        if (hasStatus && lastAvailable == 0 && status.Available > 0)
            StartReadyFlash();

        lastAvailable = status.Available;
    }

    bool IsSampledRechargeDue()
    {
        return hasStatus && sampledDuration > 0f && Time.time - sampleTime >= sampledRemaining;
    }

    void UpdateCooldownFill()
    {
        if (cooldownFill == null)
            return;

        float remaining = sampledDuration > 0f
            ? Mathf.Clamp(sampledRemaining - (Time.time - sampleTime), 0f, sampledDuration)
            : 0f;

        // A full pool has nothing owed, so the overlay disappears entirely rather than sitting at
        // zero fill and dimming the icon.
        bool visible = remaining > 0f;
        if (visible != lastFillVisible)
        {
            cooldownFill.gameObject.SetActive(visible);
            lastFillVisible = visible;
        }

        if (!visible)
            return;

        // Radial 360 from 12 o'clock, filled counter-clockwise: the overlay holds the time still
        // owed, so the cleared wedge is the one that sweeps clockwise as the recharge runs.
        // Writing fillAmount dirties the Graphic and forces a canvas rebuild, so only write it
        // when the value actually moved.
        float fill = remaining / sampledDuration;
        if (!Mathf.Approximately(fill, lastFillAmount))
        {
            cooldownFill.fillAmount = fill;
            lastFillAmount = fill;
        }

        float alpha = lastAvailable > 0 ? rechargingOverlayAlpha : emptyOverlayAlpha;
        if (!Mathf.Approximately(alpha, lastOverlayAlpha))
        {
            Color color = cooldownFill.color;
            color.a = alpha;
            cooldownFill.color = color;
            lastOverlayAlpha = alpha;
        }
    }

    void StartReadyFlash()
    {
        if (readyFlash == null || readyFlashSeconds <= 0f)
            return;

        // Unscaled: the flash is a one-off cue, not part of the cooldown it announces, so it should
        // not stretch under hitlag or a skill cutscene's world slow.
        flashEndTime = Time.unscaledTime + readyFlashSeconds;
        lastFlashAlpha = -1f;
        SetActiveIfNeeded(readyFlash.gameObject, true);
    }

    void UpdateReadyFlash()
    {
        if (readyFlash == null || flashEndTime < 0f)
            return;

        float remaining = flashEndTime - Time.unscaledTime;
        if (remaining <= 0f)
        {
            flashEndTime = -1f;
            lastFlashAlpha = -1f;
            SetActiveIfNeeded(readyFlash.gameObject, false);
            return;
        }

        float alpha = Mathf.Clamp01(remaining / readyFlashSeconds);
        if (!Mathf.Approximately(alpha, lastFlashAlpha))
        {
            Color color = readyFlash.color;
            color.a = alpha;
            readyFlash.color = color;
            lastFlashAlpha = alpha;
        }
    }

    void ApplyVisibility(bool visible)
    {
        GameObject target = root != null ? root : gameObject;

        // The activeSelf guard is the one that matters: a redundant SetActive on a UI object
        // dirties the canvas and forces a rebuild.
        if (target != gameObject && target.activeSelf != visible)
            target.SetActive(visible);
    }

    static void SetActiveIfNeeded(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    static string GetChargeText(int available)
    {
        return (uint)available < (uint)ChargeTexts.Length
            ? ChargeTexts[available]
            : available.ToString();
    }
}
