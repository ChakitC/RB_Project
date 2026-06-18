using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[HideMonoScript]
public sealed class TauntSkillPayloadDef : SkillPayloadDef
{
    [PropertyOrder(-20)]
    [InfoBox("Spawns a runtime listener that applies taunt to enemies in range when the TauntApply timeline event fires.")]
    [SerializeField, BoxGroup("Setup"), Min(0f)]
    [LabelText("Radius"), SuffixLabel("m")]
    private float radius = 10f;

    [SerializeField, BoxGroup("Setup"), Min(0f)]
    [LabelText("Duration"), SuffixLabel("s")]
    private float duration = 3f;

    [SerializeField, BoxGroup("Setup"), ToggleLeft]
    [LabelText("Require Line of Sight")]
    private bool requireLineOfSight = false;

    [SerializeField, BoxGroup("Setup")]
    [LabelText("Target Layers")]
    private LayerMask targetLayers = ~0;

    public float Radius => radius;
    public float Duration => duration;
    public bool RequireLineOfSight => requireLineOfSight;
    public LayerMask TargetLayers => targetLayers;

    public override bool RequiresSkillTimelineEvents => true;

    public override void CollectTimelineEventNames(List<CombatTimelineEventName> eventNames)
    {
        CombatTimelineEventNames.AddUnique(eventNames, CombatTimelineEventName.TauntApply);
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
