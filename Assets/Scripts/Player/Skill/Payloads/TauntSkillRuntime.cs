using System;
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
        expireAt = Time.time + (payloadDef != null ? payloadDef.Duration + 5f : 10f);

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

    void PerformTaunt()
    {
        if (payload == null || context == null)
            return;

        Transform casterRoot = context.CasterRoot;
        Vector3 origin = casterRoot != null ? casterRoot.position : transform.position;
        float searchRadius = payload.Radius;
        LayerMask targetMask = payload.TargetLayers;
        float tauntDuration = payload.Duration;

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
                if (Physics.Raycast(origin, direction.normalized, direction.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    continue;
            }

            AITargetSensor sensor = hit.GetComponentInParent<AITargetSensor>();
            if (sensor == null)
                continue;

            sensor.ApplyTaunt(sensor.transform, tauntDuration);
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
