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
        float radiusSqr = searchRadius * searchRadius;
        LayerMask targetMask = payload.TargetLayers;

        // Character actor discovery ทำผ่าน active CharacteContext + ระยะจาก context ตาม project rule —
        // physics เหลือไว้เฉพาะ line-of-sight ที่ต้องอาศัย collider geometry จริงๆ
        CharacteContext[] targetContexts = UnityEngine.Object.FindObjectsByType<CharacteContext>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < targetContexts.Length; i++)
        {
            CharacteContext targetContext = targetContexts[i];
            if (targetContext == null)
                continue;

            Transform targetRoot = targetContext.transform;
            if (BelongsToCaster(targetRoot))
                continue;

            if (!IsInTargetLayers(targetContext, targetMask))
                continue;

            if ((targetRoot.position - origin).sqrMagnitude > radiusSqr)
                continue;

            if (payload.RequireLineOfSight && !HasLineOfSight(origin, targetContext))
                continue;

            targetContext.ResolveReferences();

            // AITargetSensor commonly lives under a child branch (e.g. "AI_System") of the character
            // root rather than on the root itself, so search the whole CharacteContext subtree.
            AITargetSensor sensor = targetContext.GetComponentInChildren<AITargetSensor>(true);
            if (sensor == null)
                continue;

            StatusEffectController controller = targetContext.StatusEffects != null
                ? targetContext.StatusEffects
                : targetContext.GetComponentInChildren<StatusEffectController>(true);
            if (controller == null)
                continue;

            // สกิลเป็นคนลง Taunt status เอง แล้วค่อยบอก sensor ให้ re-resolve — sensor ไม่ถือ Taunt Def เอง
            StatusApplicationSpec tauntSpec = payload.TauntStatus;
            if (tauntSpec?.effect == null)
                continue;

            controller.ApplyEffect(tauntSpec, context.CasterObject, tauntDuration);
            sensor.OnTauntApplied(casterRoot);
            ApplyConditionalStatuses(controller, tauntDuration);
        }
    }

    bool IsInTargetLayers(CharacteContext targetContext, LayerMask targetMask)
    {
        if (targetMask.value == ~0)
            return true;

        // เช็คทั้ง root และ collider ลูก — หลาย prefab วาง collider ไว้บน child ที่ layer ต่างจาก root
        if ((targetMask.value & (1 << targetContext.gameObject.layer)) != 0)
            return true;

        Collider[] colliders = targetContext.GetComponentsInChildren<Collider>(false);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && (targetMask.value & (1 << colliders[i].gameObject.layer)) != 0)
                return true;
        }

        return false;
    }

    bool HasLineOfSight(Vector3 origin, CharacteContext targetContext)
    {
        Collider targetCollider = targetContext.GetComponentInChildren<Collider>(false);
        Vector3 targetPoint = targetCollider != null
            ? targetCollider.bounds.center
            : targetContext.transform.position;

        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;
        if (distance <= Mathf.Epsilon)
            return true;

        // Stop short of the target's own bounds so the raycast doesn't clip the target itself and
        // report a false "blocked" result.
        float boundsPadding = targetCollider != null ? targetCollider.bounds.extents.magnitude : 0f;
        float checkDistance = Mathf.Max(0f, distance - boundsPadding);

        return !Physics.Raycast(
            origin,
            direction / distance,
            checkDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
    }

    void ApplyConditionalStatuses(StatusEffectController controller, float tauntDuration)
    {
        IReadOnlyList<TauntSkillPayloadDef.ConditionalStatus> conditionals = payload.ConditionalApplications;
        if (conditionals == null || conditionals.Count == 0 || context == null || controller == null)
            return;

        GameObject source = context.CasterObject;
        for (int i = 0; i < conditionals.Count; i++)
        {
            TauntSkillPayloadDef.ConditionalStatus conditional = conditionals[i];
            if (conditional == null || !context.HasUpgrade(conditional.requiredUpgradeId))
                continue;

            StatusApplicationSpec resolvedSpec = conditional.spec;
            if (resolvedSpec?.effect == null)
                continue;

            controller.ApplyEffect(resolvedSpec, source, tauntDuration);
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
