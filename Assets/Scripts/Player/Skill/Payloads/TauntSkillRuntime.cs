using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TauntSkillRuntime : MonoBehaviour
{
    private SkillCastContext context;
    private TauntSkillPayloadDef payload;
    private CharacterAnimBrain animBrain;
    private int requestId;
    private float expireAt;
    private bool initialized;
    private bool shuttingDown;

    public void Initialize(SkillCastContext castContext, TauntSkillPayloadDef payloadDef)
    {
        context = castContext;
        payload = payloadDef;
        animBrain = castContext != null ? castContext.AnimBrain : null;
        requestId = castContext != null ? castContext.RequestId : 0;

        if (payloadDef != null)
        {
            ResolveTauntParams(out _, out float duration);
            expireAt = Time.time + duration + 5f;
        }
        else
        {
            expireAt = Time.time + 10f;
        }

        if (animBrain == null || payloadDef == null)
        {
            Shutdown();
            return;
        }

        animBrain.SkillTimelineEventRaised += OnSkillTimelineEventRaised;
        animBrain.SkillCastInterrupted += OnSkillCastInterrupted;
        initialized = true;
    }

    void Update()
    {
        if (!initialized || shuttingDown)
            return;

        if (Time.time >= expireAt)
        {
            Shutdown();
            return;
        }

        if (animBrain != null && requestId > 0)
        {
            if (!animBrain.TryGetActiveSkillNormalizedTime(requestId, out _))
            {
                Shutdown();
            }
        }
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    void Unsubscribe()
    {
        if (animBrain == null)
            return;

        animBrain.SkillTimelineEventRaised -= OnSkillTimelineEventRaised;
        animBrain.SkillCastInterrupted -= OnSkillCastInterrupted;
    }

    void OnSkillTimelineEventRaised(int raisedRequestId, CombatTimelineEventName eventName)
    {
        if (!initialized || shuttingDown)
            return;

        if (raisedRequestId != requestId)
            return;

        if (eventName == CombatTimelineEventName.TauntApply)
        {
            PerformTaunt();
        }
    }

    void OnSkillCastInterrupted(int interruptedRequestId)
    {
        if (interruptedRequestId != requestId)
            return;

        Shutdown();
    }

    void ResolveTauntParams(out float radius, out float duration)
    {
        radius = payload.Radius;
        duration = payload.Duration;
        FinalSkillStats stats = context != null ? context.SkillStats : null;
        if (!payload.UseSkillStats || stats == null)
            return;
        if (stats.areaRadius > 0f)
            radius = stats.areaRadius;
        if (stats.effectDuration > 0f)
            duration = stats.effectDuration;
    }

    void PerformTaunt()
    {
        if (payload == null || context == null)
            return;

        Transform casterRoot = context.CasterRoot;
        Vector3 origin = casterRoot != null ? casterRoot.position : transform.position;
        ResolveTauntParams(out float searchRadius, out float tauntDuration);
        LayerMask targetMask = payload.TargetLayers;

        Collider[] hits = Physics.OverlapSphere(origin, searchRadius, targetMask);
        if (hits == null || hits.Length == 0)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
                continue;

            if (BelongsToCaster(hit.transform))
                continue;

            if (payload.RequireLineOfSight)
            {
                Vector3 hitPoint = hit.bounds.center;
                Vector3 direction = hitPoint - origin;
                float distance = direction.magnitude;
                // Stop short of the target's own bounds so the raycast doesn't clip the target
                // itself and report a false "blocked" result.
                float checkDistance = Mathf.Max(0f, distance - hit.bounds.extents.magnitude);
                if (Physics.Raycast(origin, direction.normalized, checkDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    continue;
            }

            // AITargetSensor commonly lives under a child branch (e.g. "AI_System") that is a
            // descendant of the hit collider's root, not an ancestor, so GetComponentInParent
            // alone would miss it. Fall back to searching within the owning character's
            // CharacteContext specifically (NOT hit.transform.root) — in gameplay scenes,
            // multiple characters are commonly parented under a shared room root, so scoping
            // to scene-root would grab the first sensor found under that root, not the one
            // belonging to the character that was actually hit.
            CharacteContext targetContext = hit.GetComponentInParent<CharacteContext>();

            AITargetSensor sensor = hit.GetComponentInParent<AITargetSensor>();
            if (sensor == null && targetContext != null)
                sensor = targetContext.GetComponentInChildren<AITargetSensor>(true);

            if (sensor == null)
                continue;

            sensor.ApplyTaunt(casterRoot, tauntDuration);
            ApplyConditionalStatuses(hit, targetContext, tauntDuration);
        }
    }

    void ApplyConditionalStatuses(Collider hit, CharacteContext targetContext, float tauntDuration)
    {
        IReadOnlyList<TauntSkillPayloadDef.ConditionalStatus> conditionals = payload.ConditionalApplications;
        if (conditionals == null || conditionals.Count == 0 || context == null)
            return;

        StatusEffectController controller = hit.GetComponentInParent<StatusEffectController>();
        if (controller == null && targetContext != null)
            controller = targetContext.GetComponentInChildren<StatusEffectController>(true);
        if (controller == null)
            return;

        GameObject source = context.CasterObject;
        for (int i = 0; i < conditionals.Count; i++)
        {
            TauntSkillPayloadDef.ConditionalStatus conditional = conditionals[i];
            if (conditional == null || !context.HasUpgrade(conditional.requiredUpgradeId))
                continue;

            StatusApplicationSpec resolvedSpec = conditional.ResolvedSpec();
            if (resolvedSpec?.effect == null)
                continue;

            controller.ApplyEffect(resolvedSpec.ResolveWithDurationFallback(tauntDuration), source);
        }
    }

    bool BelongsToCaster(Transform other)
    {
        Transform casterRoot = context != null ? context.CasterRoot : null;
        if (casterRoot == null || other == null)
            return false;

        return other == casterRoot || other.IsChildOf(casterRoot) || other.root == casterRoot;
    }

    void Shutdown()
    {
        if (shuttingDown)
            return;

        shuttingDown = true;
        Unsubscribe();
        Destroy(gameObject);
    }
}
