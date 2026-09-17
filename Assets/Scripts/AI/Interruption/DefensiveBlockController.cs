using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class DefensiveBlockController : MonoBehaviour
{
    const float PlacementContactTolerance = 0.005f;
    public BlockAnimationProfile animationProfile;
    public FieldAllyMember member;
    public LayerMask worldLayers = 1;
    [Min(0.1f)] public float standAhead = 2.5f;
    [Min(0.1f)] public float minimumStandAhead = 0.8f;
    [Min(0.1f)] public float approachClearance = 4.2f;
    [Min(0.1f)] public float guardHalfWidth = 0.65f;
    [Min(0.1f)] public float guardHalfHeight = 1.2f;
    [Min(0f)] public float guardForwardOffset = 1.4f;
    [Min(0.1f)] public float timeoutSeconds = 2f;
    [Min(0f)] public float slideDistance = 1.5f;
    [Min(0.01f)] public float slideSeconds = 0.35f;
    public GameObject impactVfx;
    [Header("Warp presentation")]
    [Min(0f)] public float warpFadeOutSeconds = 0.04f;
    [Min(0f)] public float warpFadeInSeconds = 0.08f;
    AllyContext ctx;
    DefensiveBlockAttack attack;
    PlayerContext player;
    int request;
    int life;
    bool active, impact, restoring;
    bool btEnabled, moveEnabled, agentStopped, agentPosition, agentRotation;
    bool agentOwned, rbKinematic, rbGravity;
    float elapsed, slideElapsed, slideApplied;
    Vector3 slideDirection;
    CharacterVisibilityController warpVisibility;
    CharacterPlacementRequest pendingPlacement;
    bool arrived;
    public Vector3 GuardCenter => transform.position + Vector3.up * 1.2f + transform.forward * guardForwardOffset;
    public bool IsExecuting => active;
    public bool HasArrived => active && arrived;
    public void CancelFor(DefensiveBlockAttack source, int id)
    {
        if (active && attack == source && request == id) Cancel();
    }

    void Awake() { ctx = GetComponentInParent<AllyContext>(); ctx?.ResolveReferences(); }
    public bool IsReadyFor(DefensiveBlockAttack source, int id) => active && arrived && !impact && attack == source &&
        request == id && ctx != null && ctx.LifeGeneration == life && ctx.HealthSystem != null && ctx.HealthSystem.IsAlive &&
        ctx.AnimBrain != null && (ctx.AnimBrain.BlockPhase == BlockAnimationPhase.Begin ||
            ctx.AnimBrain.BlockPhase == BlockAnimationPhase.Loop);

    public bool CanBegin(PlayerContext protectedPlayer, DefensiveBlockAttack source) =>
        TryResolveBeginPose(protectedPlayer, source, out _, out _);

    bool TryResolveBeginPose(PlayerContext protectedPlayer, DefensiveBlockAttack source,
        out CharacterPlacementRequest placement, out CharacterPlacementResult pose)
    {
        placement = null;
        pose = default;
        if (!isActiveAndEnabled || active || protectedPlayer == null || source == null || ctx == null || ctx.HealthSystem == null || !ctx.HealthSystem.IsAlive ||
            member == null || member.IsBusy || member.IsReserved || ctx.AnimDriver == null || ctx.AnimBrain == null ||
            animationProfile == null || animationProfile.beginClip == null || animationProfile.impactClip == null ||
            !CharacterAnimationTransitionPolicy.CanStart(ctx.AnimBrain.CurrentAnimationMode, CharacterAnimationMode.Block,
                CharacterAnimationTransitionReason.NormalCommand, ctx.AnimBrain.IsDowned))
            return false;
        Vector3 direction = source.transform.position - protectedPlayer.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return false;
        float sourceDistance = direction.magnitude;
        direction.Normalize();
        Collider body = ctx.ColliderRefs != null ? ctx.ColliderRefs.CharacterPositionCollider : null;
        if (!CharacterPlacementFootprintUtility.TryGetColliderFootprint(body, ctx.transform, out var footprint, out _)) return false;
        float ahead = Mathf.Clamp(sourceDistance - approachClearance, minimumStandAhead, standAhead);
        Vector3 desired = protectedPlayer.transform.position + direction * ahead;
        placement = new CharacterPlacementRequest(ctx.transform, body, footprint, ctx.TargetIdentity,
            source.transform, CharacterPlacementRequest.AnchorSnapshot.Capture(source.transform),
            new[] { new CharacterPlacementRequest.Candidate(desired, Quaternion.LookRotation(direction), 0, 0) },
            null, 0f, null, worldLayers, ~0, null, this, false, false, true,
            // A static warp uses the exact body shape; inflated padding can touch the floor
            // and score a false world obstruction while the guard pose is being raised.
            runtimePolicy: CharacterPlacementRuntimePolicy.CreateDefault(true, 0.75f,
                QueryTriggerInteraction.Ignore, collisionPadding: 0f));
        return CharacterPlacementResolver.TryResolve(placement, CharacterPlacementReservationRegistry.Shared, out pose) &&
            pose.Score.MaxWorldPenetration <= PlacementContactTolerance;
    }

    public bool TryBegin(PlayerContext protectedPlayer, DefensiveBlockAttack source, int id)
    {
        ctx?.ResolveReferences();
        if (!TryResolveBeginPose(protectedPlayer, source, out var placement, out var pose)) return false;
        if (!member.TryReserve(this)) return false;
        if (!CharacterPlacementReservationRegistry.Shared.TryReserve(placement, pose, out _))
        {
            member.ReleaseReservation(this);
            return false;
        }
        attack = source; player = protectedPlayer; request = id; life = ctx.LifeGeneration;
        active = true; impact = false; elapsed = slideElapsed = slideApplied = 0f;
        arrived = false;
        pendingPlacement = placement;
        btEnabled = ctx.BehaviorTree != null && ctx.BehaviorTree.enabled;
        moveEnabled = ctx.AgentMoveDriver != null && ctx.AgentMoveDriver.enabled;
        if (ctx.BehaviorTree != null) ctx.BehaviorTree.enabled = false;
        if (ctx.AgentMoveDriver != null) ctx.AgentMoveDriver.enabled = false;
        agentOwned = ctx.agent != null && ctx.agent.enabled && ctx.agent.isOnNavMesh;
        if (agentOwned)
        {
            agentStopped = ctx.agent.isStopped; agentPosition = ctx.agent.updatePosition; agentRotation = ctx.agent.updateRotation;
            ctx.agent.isStopped = true; ctx.agent.updatePosition = false; ctx.agent.updateRotation = false;
        }
        if (ctx.rb != null)
        {
            rbKinematic = ctx.rb.isKinematic; rbGravity = ctx.rb.useGravity;
            ctx.rb.isKinematic = true; ctx.rb.useGravity = false;
        }
        ctx.AnimBrain.PlaybackEvent += OnPlayback;
        if (!ctx.AnimDriver.TryBeginBlock(id, animationProfile)) { Cancel(); return false; }
        // Raise the guard while fading out, so the visual transition does not add another
        // startup delay. No interception is allowed until the actor has actually arrived.
        warpVisibility = ctx.Visibility;
        if (warpVisibility != null && warpVisibility.isActiveAndEnabled)
        {
            warpVisibility.Disappeared += OnWarpHidden;
            warpVisibility.Disappear(warpFadeOutSeconds);
        }
        else OnWarpHidden();
        return active;
    }

    void OnWarpHidden()
    {
        if (warpVisibility != null) warpVisibility.Disappeared -= OnWarpHidden;
        if (!active || arrived) return;
        if (ctx == null || !ctx.isActiveAndEnabled || ctx.LifeGeneration != life ||
            ctx.HealthSystem == null || !ctx.HealthSystem.IsAlive || attack == null || !attack.Matches(request) ||
            pendingPlacement == null ||
            !CharacterPlacementResolver.TryResolve(pendingPlacement, CharacterPlacementReservationRegistry.Shared, out var pose) ||
            pose.Score.MaxWorldPenetration > PlacementContactTolerance)
        { Cancel(); return; }
        // Recheck the originally reserved point; never chase a moving player during fade-out.
        ctx.transform.SetPositionAndRotation(pose.StartPosition, pose.StartRotation);
        if (agentOwned && ctx.agent != null && ctx.agent.isOnNavMesh) ctx.agent.nextPosition = pose.StartPosition;
        Physics.SyncTransforms();
        arrived = true;
        pendingPlacement = null;
        if (warpVisibility != null) warpVisibility.Appear(warpFadeInSeconds);
    }

    public void ConfirmImpact(DefensiveBlockAttack source, int id)
    {
        if (!IsReadyFor(source, id) || !ctx.AnimDriver.TryBlockImpact(id, slideSeconds)) return;
        impact = true;
        slideDirection = -ctx.transform.forward;
        GlobalTimeScaleManager.Instance.RequestHitLag(0.06f, 0.1f, null);
        if (impactVfx != null)
        {
            GameObject effect = Instantiate(impactVfx, GuardCenter, ctx.transform.rotation);
            Destroy(effect, 2f);
        }
    }
    void Update()
    {
        if (!active) return;
        if (ctx == null || ctx.LifeGeneration != life || ctx.HealthSystem == null || !ctx.HealthSystem.IsAlive ||
            (!impact && (attack == null || !attack.Matches(request)))) { Cancel(); return; }
        elapsed += Time.deltaTime;
        if (!impact && elapsed >= timeoutSeconds && ctx.AnimBrain.BlockPhase != BlockAnimationPhase.Exit)
            ctx.AnimDriver.EndBlock(request);
    }
    void LateUpdate()
    {
        if (!active || !impact || slideElapsed >= slideSeconds) return;
        slideElapsed += Time.deltaTime;
        float next = slideDistance * Mathf.Clamp01(slideElapsed / slideSeconds);
        float distance = next - slideApplied;
        slideApplied = next;
        Collider body = ctx.ColliderRefs != null ? ctx.ColliderRefs.CharacterPositionCollider : null;
        if (CharacterBodySweepUtility.TryResolveShape(body, out var shape))
            distance = CharacterBodySweepUtility.ResolveAllowedDistance(shape, slideDirection, distance,
                0.03f, ~0, QueryTriggerInteraction.Ignore, ctx.transform);
        else distance = 0f;
        Vector3 desired = ctx.transform.position + slideDirection * distance;
        if (NavMesh.Raycast(ctx.transform.position, desired, out var edge, NavMesh.AllAreas)) desired = edge.position;
        if (NavMesh.SamplePosition(desired, out var sample, 0.2f, NavMesh.AllAreas) && Mathf.Abs(sample.position.y - desired.y) < 0.2f)
            ctx.transform.position = sample.position;
        if (agentOwned && ctx.agent.isOnNavMesh) ctx.agent.nextPosition = ctx.transform.position;
    }
    void OnPlayback(CharacterAnimBrain.PlaybackSignal signal)
    {
        if (signal.Kind == CharacterAnimBrain.PlaybackKind.Block && signal.RequestId == request &&
            (signal.Phase == CharacterAnimBrain.PlaybackPhase.Completed || signal.Phase == CharacterAnimBrain.PlaybackPhase.Interrupted))
            Cancel();
    }
    public void Cancel()
    {
        if (!active || restoring) return;
        restoring = true;
        active = false;
        if (warpVisibility != null)
        {
            warpVisibility.Disappeared -= OnWarpHidden;
            warpVisibility.SetVisibleImmediate();
            warpVisibility = null;
        }
        pendingPlacement = null;
        arrived = false;
        if (ctx != null)
        {
            if (ctx.AnimBrain != null) ctx.AnimBrain.PlaybackEvent -= OnPlayback;
            ctx.AnimDriver?.EndBlock(request, true);
            if (agentOwned && ctx.agent != null && ctx.agent.enabled && ctx.agent.isOnNavMesh)
            {
                ctx.agent.nextPosition = ctx.transform.position;
                ctx.agent.isStopped = agentStopped; ctx.agent.updatePosition = agentPosition; ctx.agent.updateRotation = agentRotation;
            }
            if (ctx.BehaviorTree != null) ctx.BehaviorTree.enabled = btEnabled;
            if (ctx.AgentMoveDriver != null) ctx.AgentMoveDriver.enabled = moveEnabled;
            if (ctx.rb != null) { ctx.rb.isKinematic = rbKinematic; ctx.rb.useGravity = rbGravity; }
        }
        member?.ReleaseReservation(this);
        CharacterPlacementReservationRegistry.Shared.ReleaseOwner(this);
        attack = null; player = null; request = 0;
        restoring = false;
    }
    void OnDisable() { Cancel(); }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.matrix = Matrix4x4.TRS(GuardCenter, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(guardHalfWidth * 2f, guardHalfHeight * 2f, 0.1f));
    }
}
