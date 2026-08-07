using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[HideMonoScript]
public sealed class TauntSkillPayloadDef : SkillPayloadDef
{
    [Serializable]
    public sealed class ConditionalStatus
    {
        public string requiredUpgradeId;
        public StatusEffectDef effect;
        [Min(1)] public int stacks = 1;
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

    [SerializeField, BoxGroup("Upgrades")]
    [LabelText("Conditional Status Effects (on taunted enemies)")]
    [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
    private List<ConditionalStatus> conditionalApplications = new();

    public float Radius => radius;
    public float Duration => duration;
    public bool UseSkillStats => useSkillStats;
    public bool RequireLineOfSight => requireLineOfSight;
    public LayerMask TargetLayers => targetLayers;
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
