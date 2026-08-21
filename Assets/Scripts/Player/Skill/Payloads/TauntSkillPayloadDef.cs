using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[HideMonoScript]
public sealed class TauntSkillPayloadDef : SkillPayloadDef
{
    // tauntDuration (the taunt's own duration) is passed as the `fallbackDuration` argument of
    // StatusEffectController.ApplyEffect, so a per-application duration override still wins over it.
    [PropertyOrder(-20)]
    [InfoBox("Spawns a runtime listener that applies taunt to enemies in range when the TauntApply timeline event fires.")]
    [SerializeField, BoxGroup("Setup"), Min(0f)]
    [LabelText("Radius"), SuffixLabel("m")]
    private float radius = 10f;

    [SerializeField, BoxGroup("Setup"), Min(0f)]
    [LabelText("Duration"), SuffixLabel("s")]
    private float duration = 3f;

    [SerializeField, BoxGroup("Setup"), ToggleLeft]
    [LabelText("Use Skill Stats")]
    [Tooltip("Read radius from FinalSkillStats.areaRadius and duration from FinalSkillStats.effectDuration when they are configured.")]
    private bool useSkillStats = true;

    [SerializeField, BoxGroup("Setup"), ToggleLeft]
    [LabelText("Require Line of Sight")]
    private bool requireLineOfSight = false;

    [SerializeField, BoxGroup("Setup")]
    [LabelText("Target Layers")]
    private LayerMask targetLayers = ~0;

    // Taunt state เป็นของสกิล ไม่ใช่ของ AITargetSensor อีกแล้ว — sensor derive สถานะ taunt จาก
    // StatusEffectInstance ที่ติด tag StatusEffectTags.Taunt แทนการถือ Def เฉพาะตัว
    [SerializeField, BoxGroup("Setup")]
    [LabelText("Taunt Status")]
    [Tooltip("Status applied to every taunted enemy. Its StatusEffectDef must carry the \"Taunt\" tag, " +
        "use separatePerSource = true and StackMode.RefreshDuration.")]
    private StatusApplicationSpec tauntStatus = new();

    [SerializeField, BoxGroup("Upgrades")]
    [LabelText("Conditional Status Effects")]
    [SkillStatusRouteTarget(SkillStatusTarget.TauntedEnemies, "After Taunt")]
    private ConditionalStatusRoute conditionalStatuses = new();

    public float Radius => radius;
    public float Duration => duration;
    public bool UseSkillStats => useSkillStats;
    public bool RequireLineOfSight => requireLineOfSight;
    public LayerMask TargetLayers => targetLayers;
    public StatusApplicationSpec TauntStatus => tauntStatus;
    public ConditionalStatusRoute ConditionalStatuses => conditionalStatuses;

    public override bool RequiresSkillTimelineEvents => true;

    public override void CollectTimelineEventNames(List<CombatTimelineEventName> eventNames)
    {
        CombatTimelineEventNames.AddUnique(eventNames, CombatTimelineEventName.TauntApply);
    }

    public override void CollectUpgradeIds(List<string> ids)
    {
        conditionalStatuses?.CollectUpgradeIds(ids);
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            return;

        if (radius <= 0f)
            issues.Add("Taunt payload has no radius configured.");

        if (duration <= 0f)
            issues.Add("Taunt payload has no duration configured.");

        if (targetLayers.value == 0)
            issues.Add("Taunt payload has no target layers configured.");

        CollectTauntStatusIssues(issues);
        conditionalStatuses?.CollectValidationIssues(issues, "conditionalStatuses");
    }

    /// <summary>
    /// Taunt Def ที่ author ผิดจะทำให้ AITargetSensor มองไม่เห็น taunt (ไม่มี tag) หรือทำให้ผู้ใช้สกิลหลายคน
    /// ทับ instance เดียวกันจน fallback กลับหาคนก่อนหน้าไม่ได้ — จับตั้งแต่ตอน validate ไม่ใช่ตอนเล่น
    /// </summary>
    void CollectTauntStatusIssues(List<string> issues)
    {
        if (tauntStatus == null || tauntStatus.effect == null)
        {
            issues.Add(
                $"Taunt payload has no Taunt Status configured (needs a StatusEffectDef tagged " +
                $"\"{StatusEffectTags.Taunt}\").");
            return;
        }

        tauntStatus.CollectValidationIssues(issues, "tauntStatus");

        StatusEffectDef definition = tauntStatus.effect;

        if (!StatusEffectTags.Has(definition, StatusEffectTags.Taunt))
        {
            issues.Add(
                $"Taunt Status '{definition.name}' is missing the \"{StatusEffectTags.Taunt}\" tag — " +
                "AITargetSensor resolves taunt state by tag and will ignore it.");
        }

        if (!definition.separatePerSource)
        {
            issues.Add(
                $"Taunt Status '{definition.name}' must set separatePerSource = true so each taunter keeps " +
                "its own instance (otherwise expiry cannot fall back to the previous taunter).");
        }

        if (definition.stackMode != StackMode.RefreshDuration)
        {
            issues.Add(
                $"Taunt Status '{definition.name}' must use StackMode.RefreshDuration (found " +
                $"{definition.stackMode}) so re-taunting from the same source refreshes instead of stacking.");
        }
    }

    /// <summary>
    /// Succeeds once the runtime listener exists and is armed. The taunt itself lands on the
    /// TauntApply timeline event, well after the cast point, and a taunt that reaches nobody is
    /// still a cast the player spent - so target count is deliberately not part of this result.
    /// </summary>
    public override SkillExecutionResult ExecuteWithResult(SkillCastContext context)
    {
        if (context == null || context.CasterObject == null)
        {
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingRuntimeContext,
                "Taunt payload executed without a caster object.");
        }

        if (tauntStatus == null || tauntStatus.effect == null)
        {
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingAuthoringData,
                "Taunt payload has no Taunt Status configured.");
        }

        GameObject host = new GameObject("TauntSkillRuntime");
        host.transform.SetParent(null);

        TauntSkillRuntime runtime = host.AddComponent<TauntSkillRuntime>();
        if (runtime == null)
        {
            Destroy(host);
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingRuntimeContext,
                "Taunt payload could not create its runtime host.");
        }

        if (!runtime.Initialize(context, this))
        {
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingRuntimeContext,
                "Taunt runtime could not arm: no animation brain to raise TauntApply.");
        }

        return SkillExecutionResult.Succeeded;
    }
}
