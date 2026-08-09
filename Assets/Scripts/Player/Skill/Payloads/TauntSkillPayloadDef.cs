using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[HideMonoScript]
public sealed class TauntSkillPayloadDef : SkillPayloadDef
{
    // tauntDuration (the taunt's own duration) is passed as the `fallbackDuration` argument of
    // StatusEffectController.ApplyEffect, so a per-application duration override still wins over it.
    [Serializable]
    public sealed class ConditionalStatus
    {
        public string requiredUpgradeId;

        public StatusApplicationSpec spec = new();
    }

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
    [LabelText("Conditional Status Effects (on taunted enemies)")]
    [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
    private List<ConditionalStatus> conditionalApplications = new();

    public float Radius => radius;
    public float Duration => duration;
    public bool UseSkillStats => useSkillStats;
    public bool RequireLineOfSight => requireLineOfSight;
    public LayerMask TargetLayers => targetLayers;
    public StatusApplicationSpec TauntStatus => tauntStatus;
    public IReadOnlyList<ConditionalStatus> ConditionalApplications => conditionalApplications;

    public override bool RequiresSkillTimelineEvents => true;

    public override void CollectTimelineEventNames(List<CombatTimelineEventName> eventNames)
    {
        CombatTimelineEventNames.AddUnique(eventNames, CombatTimelineEventName.TauntApply);
    }

    public override void CollectUpgradeIds(List<string> ids)
    {
        if (conditionalApplications == null)
            return;

        for (int i = 0; i < conditionalApplications.Count; i++)
        {
            ConditionalStatus conditional = conditionalApplications[i];
            if (conditional != null)
                SkillUpgradeIdCollection.AddUnique(ids, conditional.requiredUpgradeId);
        }
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

    public override void Execute(SkillCastContext context)
    {
        if (context == null || context.CasterObject == null)
            return;

        GameObject host = new GameObject("TauntSkillRuntime");
        host.transform.SetParent(null);

        TauntSkillRuntime runtime = host.AddComponent<TauntSkillRuntime>();
        runtime.Initialize(context, this);
    }
}
