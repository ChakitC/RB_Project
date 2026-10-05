using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Roma's visual treatment. All gameplay quantities are read from its grant.</summary>
public sealed class RomaPassiveHudView : PassiveHudView
{
    [SerializeField] string momentumVariable = "momentum";
    [SerializeField] PassiveGaugeArc staminaRing;
    [SerializeField] PassiveGaugeArc momentumRing;
    [SerializeField] TMP_Text momentumText;
    [SerializeField] TMP_Text staminaText;
    [SerializeField] TMP_Text nextAttackText;
    [SerializeField] TMP_Text damageText;
    [SerializeField] TMP_Text feedbackText;
    [SerializeField] Color normalColor = new(0.77f, 0.89f, 0.94f);
    [SerializeField] Color lowColor = new(1f, 0.69f, 0.28f);
    readonly List<RuntimeStatModifier> modifiers = new();
    PassiveRuntime subscribed;
    StaminaSystem stamina;
    Coroutine feedback;
    public float DisplayedMomentum { get; private set; }
    public float DisplayedNextAttackPercent { get; private set; }
    public float DisplayedDamagePercent { get; private set; }
    public float DisplayedStaminaRatio { get; private set; }
    public string Feedback => feedbackText != null ? feedbackText.text : string.Empty;

    protected override void Subscribe()
    {
        Unsubscribe();
        subscribed = Runtime;
        subscribed.StateChanged += RefreshView;
        subscribed.EffectResolved += OnEffect;
        subscribed.AttackChanged += OnAttack;
        subscribed.Controller.StatModifiersChanged += RefreshView;
        stamina = subscribed.Controller.Owner.StaminaSystem;
        if (stamina != null) stamina.OnStaminaChanged += OnStamina;
    }
    protected override void Unsubscribe()
    {
        if (subscribed != null)
        {
            subscribed.StateChanged -= RefreshView;
            subscribed.EffectResolved -= OnEffect;
            subscribed.AttackChanged -= OnAttack;
            if (subscribed.Controller != null) subscribed.Controller.StatModifiersChanged -= RefreshView;
        }
        if (stamina != null) stamina.OnStaminaChanged -= OnStamina;
        subscribed = null; stamina = null;
        if (feedback != null) StopCoroutine(feedback);
        feedback = null;
        if (feedbackText != null) feedbackText.text = string.Empty;
    }
    void OnStamina(float current, float maximum) => RefreshView();
    public override void RefreshView()
    {
        if (!HasLiveRuntime) return;
        var resource = Runtime.Controller.Owner.StaminaSystem;
        Runtime.TryGetVariable(momentumVariable, out float value, out float cap);
        DisplayedMomentum = value;
        DisplayedNextAttackPercent = Runtime.GetNextAttackBonusPercent(momentumVariable);
        DisplayedStaminaRatio = resource != null && resource.Max > 0 ? Mathf.Clamp01(resource.Current / resource.Max) : 0;
        modifiers.Clear(); Runtime.AppendModifiers(modifiers);
        DisplayedDamagePercent = 0;
        foreach (var modifier in modifiers)
            if (modifier.StatType == StatType.Damage && modifier.Operation == ModifierOp.AddPercent)
                DisplayedDamagePercent += modifier.Value;
        if (momentumRing != null) momentumRing.SetFill(cap > 0 ? value / cap : 0);
        if (staminaRing != null)
        { staminaRing.SetFill(DisplayedStaminaRatio); staminaRing.color = DisplayedDamagePercent > 0 ? lowColor : normalColor; }
        if (momentumText != null) momentumText.text = value.ToString("0.#");
        if (staminaText != null) staminaText.text = resource != null ? $"STA  {resource.Current:0.#} / {resource.Max:0.#}" : "STA  --";
        if (nextAttackText != null) nextAttackText.text = $"NEXT ATTACK  +{DisplayedNextAttackPercent:0.#}%";
        if (damageText != null)
        {
            damageText.text = DisplayedDamagePercent > 0 ? $"LOW STAMINA  +{DisplayedDamagePercent:0.#}% DMG" : "LOW STAMINA BONUS  --";
            damageText.color = DisplayedDamagePercent > 0 ? lowColor : normalColor;
        }
    }
    void OnEffect(PassiveStep step, PassiveResult result, PassiveEventContext evt)
    {
        if (!HasLiveRuntime || evt.Type != PassiveEventType.PerfectDodge ||
            step is not PassiveResourceStep resource || resource.operation != PassiveResourceOperation.RestoreStamina ||
            result.Status != PassiveOutcome.Succeeded || result.Amount <= 0 || result.Target != Runtime.Controller.Owner) return;
        ShowFeedback($"PERFECT DASH  +{result.Amount:0.#} STA");
    }
    void OnAttack(string attackId, PassiveAttackPhase phase, float percent)
    {
        if (!HasLiveRuntime) return;
        ShowFeedback(phase == PassiveAttackPhase.Reserved ? $"RESERVED  +{percent:0.#}%" :
            phase == PassiveAttackPhase.Committed ? $"RELEASED  +{percent:0.#}%" : "MOMENTUM RETURNED");
    }
    void ShowFeedback(string message)
    {
        if (feedbackText == null) return;
        if (feedback != null) StopCoroutine(feedback);
        feedbackText.text = message;
        if (Application.isPlaying && isActiveAndEnabled) feedback = StartCoroutine(ClearFeedback());
    }
    IEnumerator ClearFeedback()
    { yield return new WaitForSecondsRealtime(1.4f); feedbackText.text = string.Empty; feedback = null; }
}
