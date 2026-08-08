using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[HideMonoScript]
public sealed class TauntSkillPayloadDef : SkillPayloadDef
{
    // Legacy-field fallback (lazy resolve, NOT ISerializationCallbackReceiver — see the "Legacy Migration"
    // block at the bottom of StatusEffects/StatusApplicationSpec.cs for why) — Aires_Skill_3.asset /
    // Aires_Active.asset have real `effect`/`stacks` data serialized flat under this class.
    // Duration has no field of its own on the spec here — tauntDuration (the taunt's own duration) is passed
    // as the fallback via ResolveWithDurationFallback at the call site, same as before.
    [Serializable]
    public sealed class ConditionalStatus
    {
        public string requiredUpgradeId;

        [SerializeField, HideInInspector] StatusEffectDef effect;
        [SerializeField, HideInInspector] int stacks;

        public StatusApplicationSpec spec = new();

        /// <summary>Call from TauntSkillRuntime at trigger time only — never during deserialization.</summary>
        public StatusApplicationSpec ResolvedSpec()
        {
            if (spec != null && spec.effect != null)
                return spec;

            if (effect == null)
                return spec;

            return new StatusApplicationSpec
            {
                effect = effect,
                stacks = stacks > 0 ? stacks : 1,
                modifiers = spec?.modifiers,
                durationOverride = spec?.durationOverride ?? 0f,
                tickDamageOverride = spec?.tickDamageOverride ?? 0f,
            };
        }
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
