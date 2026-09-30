using UnityEngine;

/// <summary>Placement and aim lifetime for a skill explicitly ordered by the player.</summary>
[DisallowMultipleComponent]
public sealed class PartyCommandSkillExecution : MonoBehaviour
{
    const float NavMeshSampleDistance = 1f;
    public sealed class Plan
    {
        public AllyContext Actor;
        public SkillTargetHandle Target;
        public Vector3 AimPoint;
        public CharacterPlacementRequest Request;
        public CharacterPlacementResult Placement;
        public int ActorLifeGeneration;
        public bool KeepCurrentPosition;

        public bool TargetIsValid => !SkillTargetHandle.IsAssigned(Target) ||
            Target.TryResolveAliveTarget(out _, out _);
    }

    Plan plan;
    CharacterSkillManager manager;
    FieldAllyMember member;
    AIAimTargetDriver aimDriver;
    bool aimDriverWasEnabled;
    bool running;
    bool starting;
    int requestId;
    Vector3 origin;
    Quaternion originRotation;
    System.Action onInterrupted;
    CharacterVisibilityController warpVisibility;
    bool waitingForWarp;
    bool waitingForAppearance;
    int pendingSlotIndex;
    SkillGemDefinition pendingExplicitSkill;
    bool pendingUseCommandSkill;

    public static bool TryCreatePlan(PlayerContext player, AllyContext actor,
        SkillGemDefinition skill, out Plan plan)
    {
        plan = null;
        if (player == null || actor == null || skill == null)
            return false;

        player.ResolveReferences();
        actor.ResolveReferences();
        Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        CharacteContext target = null;
        PartyCommandPlacementMode mode = skill.partyCommandPlacementMode;
        if (mode == PartyCommandPlacementMode.PlayerTarget)
        {
            player.Targeting?.TryGetTarget(out target);
            if (target == null)
                mode = skill.partyCommandNoTargetPlacement == PartyCommandNoTargetPlacement.AroundPlayer
                    ? PartyCommandPlacementMode.AroundPlayer : PartyCommandPlacementMode.InFrontOfPlayer;
        }
        bool aroundPlayer = mode == PartyCommandPlacementMode.AroundPlayer;
        float playerDistance = Mathf.Max(0.1f, skill.partyCommandPlayerDistance);
        SkillTargetHandle handle = SkillTargetHandle.For(target);
        if (target != null && !handle.TryResolveAliveTarget(out _, out _))
            return false;

        Vector3 center = target != null ? target.transform.position
            : aroundPlayer ? player.transform.position : player.transform.position + forward * playerDistance;
        Vector3 aim = target != null && target.TargetInfo != null && target.TargetInfo.AimPoint != null
            ? target.TargetInfo.AimPoint.position : target != null ? center : center + forward * 6f;
        float distance = Mathf.Max(0.1f, skill.partyCommandTargetDistance);
        if (target != null && skill.TryGetTargetPlacement(out SkillTargetPlacementSpec spec))
            distance = spec.TargetStandOffDistanceAtCastPoint;

        // Use the locomotion capsule when available. The animated position collider
        // can dip below the floor during an idle pose and falsely block every warp.
        Collider body = actor.cc != null ? actor.cc
            : actor.ColliderRefs != null ? actor.ColliderRefs.CharacterPositionCollider : null;
        if (!CharacterPlacementFootprintUtility.TryGetColliderFootprint(body, actor.transform,
                out CharacterPlacementFootprint footprint, out _))
            footprint = CharacterPlacementFootprintUtility.CreateFallbackBox(
                Vector3.up * 0.9f, new Vector3(0.35f, 0.9f, 0.35f));

        Collider targetBody = target != null ? target.ColliderRefs?.CharacterPositionCollider : null;
        if (targetBody != null && targetBody.enabled && targetBody.gameObject.activeInHierarchy)
        {
            // A stationary skill must not place its caster inside a large target.
            // Keep the authored stand-off as a minimum, with room for both bodies.
            Bounds bounds = targetBody.bounds;
            float targetRadius = new Vector2(bounds.extents.x, bounds.extents.z).magnitude +
                Vector3.ProjectOnPlane(bounds.center - center, Vector3.up).magnitude;
            float actorRadius = footprint.Shape == CharacterPlacementShape.Box
                ? new Vector2(footprint.HalfExtents.x, footprint.HalfExtents.z).magnitude
                : footprint.Radius;
            distance = Mathf.Max(distance, targetRadius + actorRadius +
                Vector3.ProjectOnPlane(footprint.CenterOffset, Vector3.up).magnitude + 0.1f);
        }

        CharacterPlacementAnimationInput animation = null;
        CharacterPlacementAnimationInput.Sample impact = default;
        float placementTime = skill.GetPlacementPointNormalized();
        if (actor.AnimBrain != null &&
            actor.AnimBrain.TryResolveSkillAnimationClip(skill, out AnimationClip clip) && clip != null)
        {
            if (!actor.AnimBrain.TryGetRootMotionSamplingAnimator(out Animator animator))
                return false;
            TargetedSkillRootMotionTrajectory trajectory;
            bool sampled = actor.RootMotionDriver != null && actor.RootMotionDriver.ZeroY
                ? TargetedSkillRootMotionTrajectoryCache.TryGet(clip, animator, out trajectory, out _)
                : TargetedSkillRootMotionTrajectoryCache.TryGetWithVerticalMotion(clip, animator, out trajectory, out _);
            if (!sampled)
                return false;
            animation = trajectory.PlacementInput;
            if (!animation.TrySample(placementTime, out impact))
                return false;
        }

        LayerMask ignoredMotionLayers = animation != null && skill.IgnoreCharacterCollisionDuringRootMotion
            ? actor.RootMotionDriver != null ? actor.RootMotionDriver.CharacterCollisionLayers
                : LayerMask.GetMask("Player", "Enemy", "Ally") : 0;

        // Prefer an already reachable target from the actor's exact current pose.
        // Check the placement-point position, so a lunge that overshoots still repositions.
        if (actor.isActiveAndEnabled)
        {
            Vector3 facing = target != null
                ? Vector3.ProjectOnPlane(center - actor.transform.position, Vector3.up) : forward;
            Quaternion rotation = Quaternion.LookRotation(facing.sqrMagnitude > 0.0001f ? facing : forward);
            if (animation != null && target != null)
                rotation *= Quaternion.Inverse(Quaternion.Euler(0f, impact.LocalYaw, 0f));
            Vector3 predictedImpact = actor.transform.position + rotation * impact.LocalPosition;
            Vector3 remaining = Vector3.ProjectOnPlane(center - predictedImpact, Vector3.up);
            bool inRange = target != null
                ? remaining.magnitude <= distance + 0.05f &&
                    Vector3.Dot(remaining, facing.normalized) >= -0.05f &&
                    HasClearTargetLine(actor, target, predictedImpact, footprint)
                : Vector3.ProjectOnPlane(actor.transform.position - center, Vector3.up).magnitude <=
                    (aroundPlayer ? playerDistance : 0.35f) &&
                    Mathf.Abs(actor.transform.position.y - center.y) <= NavMeshSampleDistance;
            if (inRange &&
                TryResolveStart(actor, body, footprint, actor.transform.position, rotation, out _, true))
            {
                var currentRequest = CreatePlacementRequest(actor, body, footprint, target, animation,
                    placementTime, ignoredMotionLayers,
                    new[] { new CharacterPlacementRequest.Candidate(actor.transform.position, rotation,
                        0f, 0, preservePosition: true) });
                if (TryResolveClear(currentRequest, out CharacterPlacementResult currentPlacement))
                {
                    plan = new Plan { Actor = actor, Target = handle,
                        AimPoint = target != null ? aim : actor.transform.position + forward * 6f,
                        Request = currentRequest, Placement = currentPlacement, KeepCurrentPosition = true };
                    return true;
                }
            }
        }

        // Player-relative modes capture the player's pose and face their forward.
        // AroundPlayer searches several rings, preferring the nearest clear position.
        Vector3 away = target != null
            ? Vector3.ProjectOnPlane(player.transform.position - center, Vector3.up).normalized
            : aroundPlayer ? Vector3.ProjectOnPlane(actor.transform.position - center, Vector3.up).normalized : -forward;
        if (away.sqrMagnitude < 0.0001f)
            away = -forward;
        int candidateCount = target != null ? 8 : aroundPlayer ? 24 : 5;
        var candidates = new System.Collections.Generic.List<CharacterPlacementRequest.Candidate>(candidateCount);
        for (int i = 0; i < candidateCount; i++)
        {
            int directionIndex = aroundPlayer ? i % 8 : i;
            float angle = directionIndex == 0 ? 0f
                : ((directionIndex + 1) / 2) * 45f * (directionIndex % 2 == 1 ? 1f : -1f);
            Vector3 position = target != null
                ? center + Quaternion.AngleAxis(angle, Vector3.up) * away * distance
                : aroundPlayer ? center + Quaternion.AngleAxis(angle, Vector3.up) * away *
                    (playerDistance * (1f - (i / 8) / 3f))
                : center + Vector3.Cross(Vector3.up, forward) *
                    (i == 0 ? 0f : ((i + 1) / 2) * 0.75f * (i % 2 == 1 ? 1f : -1f));
            Vector3 facing = target != null ? Vector3.ProjectOnPlane(aim - position, Vector3.up) : forward;
            Quaternion rotation = Quaternion.LookRotation(facing.sqrMagnitude > 0.0001f ? facing : forward);
            Vector3 desiredImpact = position;
            if (animation != null && target != null)
            {
                rotation *= Quaternion.Inverse(Quaternion.Euler(0f, impact.LocalYaw, 0f));
                Vector3 displacement = impact.LocalPosition;
                if (animation.PreserveVerticalMotion)
                {
                    // Align contact horizontally; the clip owns jump height above
                    // the grounded start, rather than lowering the start underground.
                    desiredImpact.y += displacement.y;
                    displacement.y = 0f;
                }
                position -= rotation * displacement;
            }
            else if (animation != null)
            {
                // Untargeted commands begin in front of the player. Rewinding a
                // lunging clip from that point could otherwise warp behind them.
                desiredImpact = position + rotation * impact.LocalPosition;
            }
            if (TryResolveStart(actor, body, footprint, position, rotation, out Vector3 groundedStart))
            {
                // Actor roots can sit below the baked surface. Preserve the horizontal
                // contact point while grounding the trajectory's starting plane.
                desiredImpact.y += groundedStart.y - position.y;
                // NavMesh sampling must not move a player-relative candidate outside its region.
                Vector3 planarOffset = Vector3.ProjectOnPlane(groundedStart - center, Vector3.up);
                if (target == null && (aroundPlayer ? planarOffset.magnitude > playerDistance + 0.05f
                    : Mathf.Abs(Vector3.Dot(planarOffset, forward)) > 0.35f))
                    continue;
                float preference = aroundPlayer ? (groundedStart - actor.transform.position).sqrMagnitude : Mathf.Abs(angle);
                candidates.Add(new CharacterPlacementRequest.Candidate(groundedStart, rotation,
                    preference, i, animation != null ? desiredImpact : null));
            }
        }
        if (candidates.Count == 0)
            return false;

        var request = CreatePlacementRequest(actor, body, footprint, target, animation,
            placementTime, ignoredMotionLayers, candidates.ToArray());
        if (!TryResolveClear(request, out CharacterPlacementResult placement))
            return false;

        plan = new Plan { Actor = actor, Target = handle,
            AimPoint = target != null ? aim : placement.StartPosition + forward * 6f,
            Request = request, Placement = placement };
        return true;
    }

    static CharacterPlacementRequest CreatePlacementRequest(AllyContext actor, Collider body,
        CharacterPlacementFootprint footprint, CharacteContext target, CharacterPlacementAnimationInput animation,
        float placementTime, LayerMask ignoredMotionLayers, CharacterPlacementRequest.Candidate[] candidates)
    {
        return new CharacterPlacementRequest(actor.transform, body, footprint,
            target != null ? target.TargetIdentity : AITargetIdentity.Generic,
            target != null ? target.transform : null,
            CharacterPlacementRequest.AnchorSnapshot.Capture(target != null ? target.transform : null),
            candidates, animation, placementTime, null,
            Physics.DefaultRaycastLayers & ~ignoredMotionLayers.value, ~ignoredMotionLayers.value,
            actor.transform, actor.transform, animation != null, animation != null, true,
            CharacterPlacementRuntimePolicy.CreateDefault(true, NavMeshSampleDistance, QueryTriggerInteraction.Ignore,
                collisionPadding: body != null ? 0.05f : 0f));
    }

    static bool TryResolveClear(CharacterPlacementRequest request, out CharacterPlacementResult placement)
        => CharacterPlacementResolver.TryResolve(request, CharacterPlacementReservationRegistry.Shared, out placement) &&
            placement.Score.MaxWorldPenetration <= 0.0001f && placement.Score.MaxActorPenetration <= 0.0001f;

    static bool HasClearTargetLine(AllyContext actor, CharacteContext target, Vector3 impactPosition,
        CharacterPlacementFootprint footprint)
    {
        Vector3 origin = impactPosition + Vector3.up * Mathf.Max(0.5f, footprint.CenterOffset.y);
        Vector3 end = target.TargetInfo != null && target.TargetInfo.AimPoint != null
            ? target.TargetInfo.AimPoint.position
            : target.transform.position + Vector3.up * Mathf.Max(0.5f, footprint.CenterOffset.y);
        Vector3 delta = end - origin;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(actor.transform) || hit.transform.IsChildOf(target.transform))
                continue;
            return false;
        }
        return true;
    }

    static bool TryResolveStart(AllyContext actor, Collider body, CharacterPlacementFootprint footprint,
        Vector3 position, Quaternion rotation, out Vector3 groundedStart, bool preservePosition = false)
    {
        // A skill may pass through characters during root motion, but its initial warp
        // still needs a free body-sized pose.
        var request = new CharacterPlacementRequest(actor.transform, body, footprint,
            AITargetIdentity.Generic, null, default,
            new[] { new CharacterPlacementRequest.Candidate(position, rotation, 0f, 0,
                preservePosition: preservePosition) },
            null, 0f, null, Physics.DefaultRaycastLayers, ~0, actor.transform, actor.transform,
            false, false, true,
            CharacterPlacementRuntimePolicy.CreateDefault(true, NavMeshSampleDistance, QueryTriggerInteraction.Ignore,
                collisionPadding: body != null ? 0.05f : 0f));
        bool resolved = CharacterPlacementResolver.TryResolve(request, CharacterPlacementReservationRegistry.Shared,
            out CharacterPlacementResult result);
        groundedStart = result.StartPosition;
        return resolved && result.Score.MaxWorldPenetration <= 0.0001f &&
            result.Score.MaxActorPenetration <= 0.0001f;
    }

    public static SkillCastStartResult TryStart(Plan plan, int slotIndex = 0,
        bool controlFieldAlly = true, SkillGemDefinition explicitSkill = null,
        FieldAllyMember fieldMember = null, System.Action onInterrupted = null,
        bool useCommandSkill = true)
    {
        var rejected = new SkillCastStartResult(SkillCastStartKind.Rejected, 0);
        if (plan == null || plan.Actor == null || !plan.Actor.isActiveAndEnabled || !plan.TargetIsValid)
            return rejected;
        AllyContext actor = plan.Actor;
        CharacterSkillManager manager = actor.SkillManager;
        if (manager == null || (explicitSkill != null
                ? !manager.CanStartExternalSkill(explicitSkill, SkillCastCostPolicy.Normal)
                : useCommandSkill && manager.HasConfiguredPlayerCommandSkill ? !manager.CanStartPlayerCommandSkill()
                    : !manager.CanStartCastSlot(slotIndex)))
            return rejected;
        var execution = actor.GetComponent<PartyCommandSkillExecution>();
        if (execution == null)
            execution = actor.gameObject.AddComponent<PartyCommandSkillExecution>();
        if (execution.running || actor.AimTargetDriver == null || actor.AimTargetDriver.HasOverride)
            return rejected;
        return execution.Begin(plan, manager, slotIndex, controlFieldAlly, explicitSkill, fieldMember, onInterrupted, useCommandSkill);
    }

    SkillCastStartResult Begin(Plan nextPlan, CharacterSkillManager skillManager,
        int slotIndex, bool controlFieldAlly, SkillGemDefinition explicitSkill, FieldAllyMember fieldMember,
        System.Action interrupted, bool useCommandSkill)
    {
        var rejected = new SkillCastStartResult(SkillCastStartKind.Rejected, 0);
        plan = nextPlan;
        plan.ActorLifeGeneration = plan.Actor.LifeGeneration;
        manager = skillManager;
        onInterrupted = interrupted;
        origin = transform.position;
        originRotation = transform.rotation;
        member = controlFieldAlly ? fieldMember : null;
        if (controlFieldAlly && (member == null || member.ActorContext != plan.Actor ||
            !member.TryBeginTransientExecution(this, false, false)))
            return rejected;
        if (!CharacterPlacementReservationRegistry.Shared.TryReserve(plan.Request, plan.Placement, out _))
        {
            member?.EndTransientExecution(this);
            return rejected;
        }
        running = true;
        starting = false;
        requestId = 0;
        aimDriver = plan.Actor.AimTargetDriver;
        aimDriverWasEnabled = aimDriver.enabled;
        // The transient scope may disable this driver. Explicit refresh still binds the aim.
        aimDriver.enabled = true;
        pendingSlotIndex = slotIndex;
        pendingExplicitSkill = explicitSkill;
        pendingUseCommandSkill = useCommandSkill;
        // Helpers already own their summon appearance in AllyHelperManager.
        // Only field allies that actually reposition need a departure/arrival fade.
        warpVisibility = controlFieldAlly && !plan.KeepCurrentPosition ? plan.Actor.Visibility : null;
        if (warpVisibility != null)
        {
            waitingForWarp = true;
            RefreshAim();
            warpVisibility.Disappear();
            return new SkillCastStartResult(SkillCastStartKind.WaitingForPlacement, 0);
        }
        ApplyPlacement();
        return StartCast();
    }

    void ApplyPlacement()
    {
        if (plan.KeepCurrentPosition)
            transform.rotation = plan.Placement.StartRotation;
        else
            transform.SetPositionAndRotation(plan.Placement.StartPosition, plan.Placement.StartRotation);
        RefreshAim();
    }

    SkillCastStartResult StartCast()
    {
        starting = true;
        manager.CastCancelled += OnCancelled;
        manager.CastExecutionFailed += OnFailed;
        SkillCastStartResult result = manager.TryStartDirectedPartyCommand(
            pendingSlotIndex, plan.Target, CanContinue, pendingExplicitSkill, pendingUseCommandSkill);
        requestId = result.RequestId;
        starting = false;
        if (!result.Started)
            Cleanup(true, true);
        else if (result.Kind == SkillCastStartKind.ImmediateSuccess)
            Cleanup(false);
        return result;
    }

    bool CanContinue() => running && plan.Actor != null && plan.Actor.isActiveAndEnabled &&
        plan.Actor.LifeGeneration == plan.ActorLifeGeneration &&
        (plan.Actor.HealthSystem == null || plan.Actor.HealthSystem.IsAlive) && plan.TargetIsValid;

    void RefreshAim()
    {
        if (SkillTargetHandle.IsAssigned(plan.Target) &&
            plan.Target.TryResolveLiveContext(out CharacteContext target))
            plan.AimPoint = target.TargetInfo != null && target.TargetInfo.AimPoint != null
                ? target.TargetInfo.AimPoint.position : target.transform.position;
        aimDriver.SetOverridePoint(plan.AimPoint);
    }

    void Update()
    {
        if (!running || starting)
            return;
        if (waitingForWarp || waitingForAppearance)
        {
            if (!CanContinue() || manager == null || !manager.isActiveAndEnabled ||
                (pendingExplicitSkill != null
                    ? !manager.CanStartExternalSkill(pendingExplicitSkill, SkillCastCostPolicy.Normal)
                    : pendingUseCommandSkill && manager.HasConfiguredPlayerCommandSkill
                        ? !manager.CanStartPlayerCommandSkill() : !manager.CanStartCastSlot(pendingSlotIndex)))
            {
                Cleanup(true, true);
                return;
            }
            RefreshAim();
            if (warpVisibility != null && warpVisibility.isActiveAndEnabled && warpVisibility.IsTransitioning)
                return;
            if (waitingForWarp)
            {
                waitingForWarp = false;
                waitingForAppearance = true;
                ApplyPlacement();
                warpVisibility?.Appear();
                return;
            }
            waitingForAppearance = false;
            StartCast();
            return;
        }
        bool casting = manager.TryGetActiveCast(out ActiveSkillCastInfo cast) && cast.RequestId == requestId;
        if (casting && !CanContinue())
        {
            manager.TryCancelActiveCast(SkillCastCancelReason.InvalidState);
            Cleanup(false, true);
            return;
        }
        if (plan.TargetIsValid)
            RefreshAim();
        bool playing = plan.Actor.AnimBrain != null &&
            plan.Actor.AnimBrain.TryGetActiveSkillNormalizedTime(requestId, out _);
        if (!casting && !playing)
            Cleanup(false);
    }

    void OnCancelled(ActiveSkillCastInfo info, SkillCastCancelReason reason)
    {
        if (starting || info.RequestId == requestId)
            Cleanup(starting, true);
    }

    void OnFailed(ActiveSkillCastInfo info, SkillExecutionResult result)
    {
        if (starting || info.RequestId == requestId)
            Cleanup(starting, true);
    }

    void OnDisable()
    {
        if (running && manager != null && manager.TryGetActiveCast(out ActiveSkillCastInfo cast) &&
            (starting || cast.RequestId == requestId))
            manager.TryCancelActiveCast(SkillCastCancelReason.InvalidState);
        Cleanup(false);
    }

    void Cleanup(bool restoreOrigin, bool interrupted = false)
    {
        if (!running)
            return;
        running = false;
        waitingForWarp = false;
        waitingForAppearance = false;
        if (manager != null)
        {
            manager.CastCancelled -= OnCancelled;
            manager.CastExecutionFailed -= OnFailed;
        }
        if (restoreOrigin)
            transform.SetPositionAndRotation(origin, originRotation);
        if (warpVisibility != null)
        {
            warpVisibility.SetVisibleImmediate();
            warpVisibility = null;
        }
        pendingExplicitSkill = null;
        if (aimDriver != null)
        {
            aimDriver.ClearOverride();
            aimDriver.enabled = aimDriverWasEnabled;
        }
        CharacterPlacementReservationRegistry.Shared.ReleaseOwner(transform);
        member?.EndTransientExecution(this);
        member = null;
        System.Action callback = onInterrupted;
        onInterrupted = null;
        if (interrupted)
            callback?.Invoke();
    }
}
