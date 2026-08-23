using UnityEngine;

/// <summary>
/// Owns one carried object from the moment it appears in the caster's hand until it reaches the
/// character the cast locked on.
///
/// Lives on its own root GameObject rather than on the caster. The caster here is often a summoned
/// helper that gets deactivated the instant its animation finishes, and a delivery parented to it
/// would freeze in mid-air or disappear along with it.
/// </summary>
public sealed class TargetedDeliveryRuntime : MonoBehaviour
{
    enum DeliveryState
    {
        Attached,
        InFlight,
        Finished,
    }

    /// <summary>Grace period on top of the animation before an unreleased delivery gives up.</summary>
    const float AttachedTimeoutSlack = 5f;

    /// <summary>Grace period on top of the planned flight before an in-flight delivery forces itself down.</summary>
    const float InFlightTimeoutSlack = 2f;

    SkillCastContext context;
    TargetedDeliverySkillPayloadDef payload;
    CharacterAnimBrain animBrain;
    SkillTargetHandle target;
    int requestId;

    GameObject delivery;
    Transform deliveryTransform;

    DeliveryState state = DeliveryState.Attached;
    bool initialized;
    bool subscribed;
    bool finishing;
    bool warnedMissingRelease;

    float attachedExpireAt;
    float flightStartTime;
    float flightDuration;
    float inFlightExpireAt;
    Vector3 flightStartPoint;
    Vector3 flightEndPoint;

    /// <summary>
    /// Spawns the carried object and arms the release listener. Returns false when the delivery
    /// could not be set up, so the payload can fail the cast instead of committing a cooldown for
    /// something that will never arrive.
    /// </summary>
    public bool Initialize(SkillCastContext castContext, TargetedDeliverySkillPayloadDef payloadDef)
    {
        context = castContext;
        payload = payloadDef;

        if (castContext == null || payloadDef == null || payloadDef.DeliveryPrefab == null)
        {
            Shutdown();
            return false;
        }

        animBrain = castContext.AnimBrain;
        requestId = castContext.RequestId;
        target = castContext.PrimaryTarget;

        if (animBrain == null || !SkillTargetHandle.IsAssigned(target))
        {
            Shutdown();
            return false;
        }

        Transform anchor = ResolveLaunchAnchor();
        if (anchor == null)
        {
            Shutdown();
            return false;
        }

        delivery = Instantiate(payloadDef.DeliveryPrefab, anchor.position, anchor.rotation, anchor);
        if (delivery == null)
        {
            Shutdown();
            return false;
        }

        deliveryTransform = delivery.transform;
        attachedExpireAt = Time.time + AttachedTimeoutSlack;

        animBrain.SkillTimelineEventRaised += OnSkillTimelineEventRaised;
        animBrain.SkillCastInterrupted += OnSkillCastInterrupted;
        subscribed = true;
        initialized = true;
        return true;
    }

    void Update()
    {
        if (!initialized || finishing)
            return;

        switch (state)
        {
            case DeliveryState.Attached:
                TickAttached();
                break;

            case DeliveryState.InFlight:
                TickInFlight();
                break;
        }
    }

    void TickAttached()
    {
        // Two independent backstops. The animation check catches the normal authoring mistake -
        // a clip with no DeliveryRelease marker - and the wall-clock timeout catches the case
        // where the brain never reports the request as finished either.
        bool animationOver = requestId > 0 &&
                             animBrain != null &&
                             !animBrain.TryGetActiveSkillNormalizedTime(requestId, out _);

        if (animationOver || Time.time >= attachedExpireAt)
        {
            WarnMissingRelease();
            Cancel();
        }
    }

    void TickInFlight()
    {
        if (deliveryTransform == null)
        {
            Finish(playImpact: false);
            return;
        }

        // Re-resolve every frame so the delivery tracks a target that is still moving, and keeps
        // aiming at the last place it saw one that is not.
        bool targetStillExists = target.TryResolveLiveContext(out _);
        if (targetStillExists || payload.TargetInvalidPolicy == DeliveryTargetInvalidPolicy.PresentAtLastKnownPoint)
        {
            flightEndPoint = target.ResolveDeliveryPoint(payload.ArrivalClearance);
        }
        else
        {
            Finish(playImpact: false);
            return;
        }

        float elapsed = Time.time - flightStartTime;
        float t = flightDuration > 0f ? Mathf.Clamp01(elapsed / flightDuration) : 1f;

        Vector3 position = Vector3.Lerp(flightStartPoint, flightEndPoint, t);

        // A plain sine hump: zero lift at both ends, full arc height at the midpoint.
        if (payload.ArcHeight > 0f)
            position.y += Mathf.Sin(t * Mathf.PI) * payload.ArcHeight;

        deliveryTransform.position = position;

        Vector3 spin = payload.SpinDegreesPerSecond;
        if (spin.sqrMagnitude > 0f)
            deliveryTransform.Rotate(spin * Time.deltaTime, Space.Self);

        if (t >= 1f || Time.time >= inFlightExpireAt)
            Arrive();
    }

    void OnSkillTimelineEventRaised(int raisedRequestId, CombatTimelineEventName eventName)
    {
        if (!initialized || finishing)
            return;

        if (raisedRequestId != requestId)
            return;

        if (eventName != CombatTimelineEventName.DeliveryRelease)
            return;

        // Accepted once only. A clip authored with a duplicate marker, or a re-entered request id,
        // must not relaunch a delivery that is already on its way.
        if (state != DeliveryState.Attached)
            return;

        Release();
    }

    void OnSkillCastInterrupted(int interruptedRequestId)
    {
        if (interruptedRequestId != requestId)
            return;

        // Only meaningful while the object is still in hand. Once it has been thrown it belongs to
        // the world, not to the animation, and it finishes its flight whatever happens to the caster.
        if (state == DeliveryState.Attached)
            Cancel();
    }

    void Release()
    {
        if (deliveryTransform == null)
        {
            Cancel();
            return;
        }

        // The delivery stops taking orders from the animation here, so it stops listening to it too.
        Unsubscribe();

        deliveryTransform.SetParent(null, worldPositionStays: true);

        flightStartPoint = deliveryTransform.position;
        flightEndPoint = target.ResolveDeliveryPoint(payload.ArrivalClearance);

        float distance = Vector3.Distance(flightStartPoint, flightEndPoint);
        float speed = Mathf.Max(0.01f, payload.Speed);
        float maxDuration = Mathf.Max(0.01f, payload.MaxFlightDuration);
        float minDuration = Mathf.Clamp(payload.MinFlightDuration, 0f, maxDuration);

        flightDuration = Mathf.Clamp(distance / speed, minDuration, maxDuration);
        flightStartTime = Time.time;
        inFlightExpireAt = flightStartTime + flightDuration + InFlightTimeoutSlack;

        state = DeliveryState.InFlight;
    }

    void Arrive()
    {
        // Existence and eligibility are separate questions. The delivery always finishes its
        // presentation, but a target that is down, dead, or gone receives nothing.
        bool applyEffect = target.TryResolveEffectTarget(out CharacteContext effectTarget);

        if (applyEffect)
            ApplyArrivalEffects(effectTarget);

        Finish(playImpact: true);
    }

    void ApplyArrivalEffects(CharacteContext effectTarget)
    {
        FinalSkillStats stats = context != null ? context.SkillStats : null;
        float fallbackDuration = stats != null && stats.effectDuration > 0f ? stats.effectDuration : 0f;

        HealthSystem health = effectTarget.HealthSystem;
        float healAmount = payload.ResolveHealAmount(health, stats);
        if (health != null && healAmount > 0f)
            health.Heal(healAmount);

        StatusEffectController controller = CharacterContextModuleLookup.ResolveStatusEffects(
            effectTarget.gameObject, effectTarget);
        if (controller == null)
            return;

        GameObject source = context != null ? context.CasterObject : null;

        var applications = payload.StatusSpecApplications;
        if (applications != null)
        {
            for (int i = 0; i < applications.Count; i++)
            {
                StatusApplicationSpec spec = applications[i]?.spec;
                if (spec?.effect == null)
                    continue;

                controller.ApplyEffect(spec, source, fallbackDuration);
            }
        }

        payload.ConditionalStatuses?.ApplyUnlocked(context, controller, source, fallbackDuration);
    }

    void PlayImpactFeedback()
    {
        Vector3 impactPoint = deliveryTransform != null ? deliveryTransform.position : flightEndPoint;

        if (payload.ImpactVfxPrefab != null)
        {
            GameObject vfx = Instantiate(payload.ImpactVfxPrefab, impactPoint, Quaternion.identity);
            if (vfx != null && payload.ImpactVfxLifetime > 0f)
                Destroy(vfx, payload.ImpactVfxLifetime);
        }

        if (payload.ImpactAudio != null)
            AudioSource.PlayClipAtPoint(payload.ImpactAudio, impactPoint, payload.ImpactAudioVolume);
    }

    /// <summary>Ends the delivery before it was ever thrown. Nothing lands and nothing is shown.</summary>
    void Cancel()
    {
        Finish(playImpact: false);
    }

    void Finish(bool playImpact)
    {
        if (finishing)
            return;

        if (playImpact && payload != null)
            PlayImpactFeedback();

        state = DeliveryState.Finished;
        Shutdown();
    }

    void WarnMissingRelease()
    {
        if (warnedMissingRelease)
            return;

        warnedMissingRelease = true;

        // Authoring problem, not a runtime one: the cast already committed at its cast point, so
        // this costs the caster a cooldown every time it happens. Say so once, loudly.
        string skillName = context != null && context.SkillDef != null ? context.SkillDef.name : "<unknown skill>";
        Debug.LogWarning(
            $"[TargetedDelivery] '{skillName}' finished without raising DeliveryRelease. " +
            "Add the marker to the clip after its cast point, or the delivery will never be thrown.");
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();

        // Scene teardown lands here. Destroying the delivery is all that is left to do - applying
        // an effect on the way out would double-buff a target that already received one.
        if (delivery != null)
        {
            Destroy(delivery);
            delivery = null;
            deliveryTransform = null;
        }
    }

    void Unsubscribe()
    {
        if (!subscribed || animBrain == null)
            return;

        animBrain.SkillTimelineEventRaised -= OnSkillTimelineEventRaised;
        animBrain.SkillCastInterrupted -= OnSkillCastInterrupted;
        subscribed = false;
    }

    void Shutdown()
    {
        if (finishing)
            return;

        finishing = true;
        Unsubscribe();

        if (delivery != null)
        {
            Destroy(delivery);
            delivery = null;
            deliveryTransform = null;
        }

        Destroy(gameObject);
    }

    Transform ResolveLaunchAnchor()
    {
        switch (payload.LaunchAnchorMode)
        {
            case DeliveryLaunchAnchorMode.ChildPath:
                return ResolveChildPathAnchor();

            case DeliveryLaunchAnchorMode.HumanoidBone:
                return ResolveBoneAnchor();

            default:
                return context.CastOrigin != null ? context.CastOrigin : context.CasterRoot;
        }
    }

    Transform ResolveChildPathAnchor()
    {
        Transform root = context.CasterRoot;
        if (root == null || string.IsNullOrWhiteSpace(payload.LaunchAnchorChildPath))
            return context.CastOrigin != null ? context.CastOrigin : root;

        Transform found = root.Find(payload.LaunchAnchorChildPath);
        if (found != null)
            return found;

        // Character prefabs in this project are not uniformly shaped, so a missing path is a
        // setup warning rather than a reason to drop the cast.
        Debug.LogWarning(
            $"[TargetedDelivery] Anchor path '{payload.LaunchAnchorChildPath}' not found under '{root.name}'. " +
            "Falling back to the cast origin.",
            root);

        return context.CastOrigin != null ? context.CastOrigin : root;
    }

    Transform ResolveBoneAnchor()
    {
        GameObject casterObject = context.CasterObject;
        Animator animator = casterObject != null ? casterObject.GetComponentInChildren<Animator>(true) : null;

        if (animator != null && animator.isHuman)
        {
            Transform bone = animator.GetBoneTransform(payload.LaunchAnchorBone);
            if (bone != null)
                return bone;
        }

        Debug.LogWarning(
            $"[TargetedDelivery] Bone '{payload.LaunchAnchorBone}' is unavailable on the caster. " +
            "Falling back to the cast origin.",
            casterObject);

        return context.CastOrigin != null ? context.CastOrigin : context.CasterRoot;
    }
}
