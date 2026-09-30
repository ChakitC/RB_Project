using UnityEngine;
using UnityEngine.AI;

public sealed partial class DefensiveBlockController
{
    CooperativeBlockSession cooperativeSession;
    int cooperativeMovementToken;
    bool cooperativePlayerReservation;

    internal bool CanBeginCooperative(PlayerContext issuer, DefensiveBlockAttack source) =>
        CanStartGuard(issuer, source) && Settings.cooperativeEnabled &&
        (ctx.stateHub == null || (ctx.stateHub.CanMove() && ctx.stateHub.CanUseSkill())) && TryGetFootprint(out _);

    internal float CooperativeRadius => TryGetFootprint(out var shape)
        ? Mathf.Max(shape.Radius, Mathf.Max(shape.HalfExtents.x, shape.HalfExtents.z)) : 0f;

    internal bool TryCooperativePose(PlayerContext issuer, DefensiveBlockAttack source, Vector3 desired,
        Quaternion rotation, out CharacterPlacementRequest placement, out CharacterPlacementResult pose)
    {
        placement = null; pose = default;
        if (!CanBeginCooperative(issuer, source) || !TryGetFootprint(out var shape)) return false;
        placement = new CharacterPlacementRequest(ctx.transform, ResolvePlacementBody(), shape, ctx.TargetIdentity,
            source.transform, CharacterPlacementRequest.AnchorSnapshot.Capture(source.transform),
            new[] { new CharacterPlacementRequest.Candidate(desired, rotation, 0, 0) },
            null, 0f, null, worldLayers, ~0, null, this, false, false, true,
            runtimePolicy: CharacterPlacementRuntimePolicy.CreateDefault(true, .2f,
                QueryTriggerInteraction.Ignore, collisionPadding: 0f), poseValidator: HasLandingGround);
        return ResolveCooperativePose(placement, out pose) && Vector3.Distance(desired, pose.StartPosition) < .1f;
    }

    static bool ResolveCooperativePose(CharacterPlacementRequest placement, out CharacterPlacementResult pose) =>
        CharacterPlacementResolver.TryResolve(placement, CharacterPlacementReservationRegistry.Shared, out pose) &&
        pose.Score.MaxWorldPenetration <= PlacementContactTolerance && pose.Score.MaxActorPenetration <= PlacementContactTolerance;

    // Session owns camera, damage protection and outcome. Each guard owns only its local animation/control scope.
    internal bool TryBeginCooperative(CooperativeBlockSession owner, PlayerContext issuer, DefensiveBlockAttack source,
        CharacterPlacementRequest placement)
    {
        if (!CanBeginCooperative(issuer, source) || !ResolveCooperativePose(placement, out var pose)) return false;
        bool self = ctx == issuer;
        if (!(self ? member.TryReserve(this) : member.TryBeginTransientExecution(this, protectActor: false, allowCollisionOverride: false))) return false;
        if (!CharacterPlacementReservationRegistry.Shared.TryReserve(placement, pose, out _))
        { if (self) member.ReleaseReservation(this); else member.EndTransientExecution(this); return false; }
        cooperativePlayerReservation = self;
        cooperativeSession = owner;
        attack = source; player = issuer; request = source.RequestId; life = ctx.LifeGeneration;
        playerLife = issuer.LifeGeneration; sessionDefinition = ctx.baseStats;
        active = true; arrived = true; impact = false; elapsed = slideElapsed = slideApplied = 0f;
        slideFootprint = placement.Footprint;
        selfControlToken = ctx.stateHub != null ? ctx.stateHub.AcquireExternalControlBlockToken(
            ControlBlockFlags.Move | ControlBlockFlags.Rotate | ControlBlockFlags.Shoot | ControlBlockFlags.Skill, nameof(DefensiveBlockController)) : 0;
        if (ctx == issuer)
        {
            selfMovement = issuer.movement;
            if (selfMovement != null) cooperativeMovementToken = selfMovement.AcquireSuspensionToken();
        }
        // Recheck after reservation, immediately before moving; rollback owns every acquired resource.
        if (!ResolveCooperativePose(placement, out pose)) { Cancel(); return false; }
        ctx.transform.SetPositionAndRotation(pose.StartPosition, pose.StartRotation);
        if (ctx is AllyContext companion && companion.agent != null && companion.agent.enabled && companion.agent.isOnNavMesh)
            companion.agent.nextPosition = pose.StartPosition;
        Physics.SyncTransforms();
        ResolveActiveGuardOffset();
        ctx.AnimBrain.PlaybackEvent += OnPlayback;
        if (!ctx.AnimDriver.TryBeginBlock(request)) { Cancel(); return false; }
        Accepted?.Invoke(this); Arrived?.Invoke(this);
        return active;
    }
}
