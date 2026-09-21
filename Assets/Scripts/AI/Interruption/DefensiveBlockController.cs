using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class DefensiveBlockController : MonoBehaviour
{
    const float PlacementContactTolerance = 0.005f;
    // Legacy serialized fields remain for prefab/API compatibility and runtime test overrides.
    // Author settings on CharacterStats.defensiveBlock (DefensiveBlockActorProfile).
    [HideInInspector] public BlockAnimationProfile animationProfile;
    public FieldAllyMember member;
    [Tooltip("Used when the current character has no guard profile. Assigned on the Player prefab for self guard.")]
    public DefensiveBlockActorProfile defaultSettings;
    [HideInInspector] public LayerMask worldLayers = 1;
    [HideInInspector] public float standAhead = 2.5f;
    [HideInInspector] public float minimumStandAhead = 0.8f;
    [HideInInspector] public float approachClearance = 4.2f;
    [HideInInspector] public float guardHalfWidth = 0.65f;
    [HideInInspector] public float guardHalfHeight = 1.2f;
    [HideInInspector] public float guardHalfDepth = 0.05f;
    [HideInInspector] public float guardForwardOffset = 1.4f;
    [HideInInspector] public float timeoutSeconds = 2f;
    [HideInInspector] public float slideDistance = 1.5f;
    [HideInInspector] public float slideSeconds = 0.35f;
    [HideInInspector] public GameObject impactVfx;
    [HideInInspector] public float warpFadeOutSeconds = 0.04f;
    [HideInInspector] public float warpFadeInSeconds = 0.08f;
    float guardCenterHeight = 1.2f;
    float activeGuardForwardOffset;
    float impactVfxLifetime = 2f;
    float hitLagDuration = 0.06f, hitLagTimeScale = 0.1f;
    AnimationCurve hitLagShape;
    CharacteContext ctx;
    DefensiveBlockAttack attack;
    PlayerContext player;
    int request;
    int life;
    bool active, impact, restoring;
    float elapsed, slideElapsed, slideApplied;
    Vector3 slideDirection;
    CharacterVisibilityController warpVisibility;
    CharacterPlacementRequest pendingPlacement;
    CharacterPlacementFootprint slideFootprint;
    readonly RaycastHit[] landingGroundHits = new RaycastHit[16];
    bool arrived;
    DefensiveBlockActorProfile loadedProfile;
    CharacterStats sessionDefinition;
    int playerLife;
    GameplayCameraController shotCamera;
    int selfControlToken;
    PlayerMovementCC selfMovement;
    bool selfMovementWasEnabled;
    public CharacteContext ActorContext => ctx;
    public bool IsSelfGuard => active && ctx == player;
    public int RequestId => request;
    public PlayerContext ProtectedPlayer => player;
    public event System.Action<DefensiveBlockController> Accepted;
    public event System.Action<DefensiveBlockController> Arrived;
    public event System.Action<DefensiveBlockController> Impacted;
    public event System.Action<DefensiveBlockController, bool> Finished;
    public DefensiveBlockActorProfile Settings => ctx != null && ctx.baseStats != null && ctx.baseStats.defensiveBlock != null
        ? ctx.baseStats.defensiveBlock : defaultSettings;
    public Vector3 GuardCenter => transform.position + Vector3.up * guardCenterHeight +
        transform.forward * (active && arrived ? activeGuardForwardOffset : guardForwardOffset);
    public bool IsExecuting => active;
    public bool HasArrived => active && arrived;
    float ActorDeltaTime => ctx == null || ctx.UsesWorldSlow
        ? TimeSlowManager.Instance.WorldDeltaTime
        : Time.deltaTime;
    public void CancelFor(DefensiveBlockAttack source, int id)
    {
        if (active && attack == source && request == id) Cancel();
    }

    void Awake()
    {
        ctx = GetComponentInParent<CharacteContext>();
        ctx?.ResolveReferences();
        if (member == null && ctx != null) member = ctx.FieldAllyMember;
        ResolveProfile();
    }
    public bool IsReadyFor(DefensiveBlockAttack source, int id) => active && arrived && !impact && attack == source &&
        request == id && ctx != null && ctx.LifeGeneration == life && ctx.HealthSystem != null && ctx.HealthSystem.IsAlive &&
        ctx.baseStats == sessionDefinition && player != null && player.LifeGeneration == playerLife &&
        player.isActiveAndEnabled && player.HealthSystem != null && player.HealthSystem.IsAlive &&
        ctx.AnimBrain != null && (ctx.AnimBrain.BlockPhase == BlockAnimationPhase.Begin ||
            ctx.AnimBrain.BlockPhase == BlockAnimationPhase.Loop);

    public bool CanBegin(PlayerContext protectedPlayer, DefensiveBlockAttack source) =>
        TryResolveBeginPose(protectedPlayer, source, out _, out _);

    public string DescribeBlockReadiness(PlayerContext issuer, DefensiveBlockAttack source)
    {
        if (!ResolveProfile()) return "Missing/invalid actor profile or Block animation";
        if (!CanStartGuard(issuer, source))
            return $"Actor not ready: enabled={isActiveAndEnabled} activeGuard={active} " +
                $"alive={ctx?.HealthSystem?.IsAlive} life={ctx?.stateHub?.LifeSM.CurrentId} " +
                $"animation={ctx?.AnimBrain?.CurrentAnimationMode} downed={ctx?.AnimBrain?.IsDowned} " +
                $"driver={ctx?.AnimDriver != null} member={member != null}";
        if (!source.CanAcceptCommand(issuer)) return "Attack currently rejects the command (see enemy reason above)";
        var placementDetails = new System.Text.StringBuilder();
        if (ctx == issuer)
        {
            if (!CanBeginSelf(issuer, source))
                return "Self guard blocked by movement/skill state or missing body footprint";
        }
        else if (!TryResolveBeginPose(issuer, source, out _, out _, placementDetails))
            return "No safe guard placement:\n" + placementDetails;
        return (source.CanApproachGuard(issuer, this) ? "Ready" :
            "Timed approach rejected: window nearly closed, facing/passed guard, unsafe path/ground or missing footprint") +
            (placementDetails.Length > 0 ? "\n" + placementDetails : "");
    }

    void TraceGuard(string message)
    {
        if (player != null && player.interruptionCommand != null && player.interruptionCommand.logDefensiveBlock)
            player.interruptionCommand.LogDefensiveBlock($"guard='{ctx?.name}' attack={attack?.GetInstanceID()} " +
                $"request={request} phase={ctx?.AnimBrain?.BlockPhase} position={ctx?.transform.position} {message}");
    }

    internal bool TryPreviewGuardPose(PlayerContext issuer, DefensiveBlockAttack source, out Vector3 position, out Quaternion rotation)
    {
        position = default; rotation = Quaternion.identity;
        if (ctx == issuer)
        {
            if (!CanBeginSelf(issuer, source)) return false;
            Vector3 direction = source.transform.position - ctx.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < .01f) return false;
            position = ctx.transform.position; rotation = Quaternion.LookRotation(direction);
            return true;
        }
        if (!TryResolveBeginPose(issuer, source, out _, out var pose)) return false;
        position = pose.StartPosition; rotation = pose.StartRotation;
        return true;
    }

    // Player-only entry point. Shared presentation, contact and reactions still belong here.
    public bool CanBeginSelf(PlayerContext issuer, DefensiveBlockAttack source)
    {
        if (ctx != issuer || !CanStartGuard(issuer, source) ||
            (ctx.stateHub != null && (!ctx.stateHub.CanUseSkill() || !ctx.stateHub.CanMove()))) return false;
        return TryGetFootprint(out _);
    }

    bool CanStartGuard(PlayerContext issuer, DefensiveBlockAttack source)
    {
        if (!ResolveProfile()) return false;
        if (member == null && ctx != null) member = ctx.FieldAllyMember;
        return isActiveAndEnabled && !active && issuer != null && source != null && ctx != null &&
            ctx.isActiveAndEnabled && ctx.HealthSystem != null && ctx.HealthSystem.IsAlive &&
            (ctx.stateHub == null || ctx.stateHub.LifeSM.CurrentId == LifeStateId.Alive) &&
            member != null && !member.IsBusy && !member.IsReserved && !member.IsInKnockback &&
            ctx.AnimDriver != null && ctx.AnimBrain != null && animationProfile != null &&
            animationProfile.beginClip != null && animationProfile.impactClip != null &&
            CharacterAnimationTransitionPolicy.CanStart(ctx.AnimBrain.CurrentAnimationMode, CharacterAnimationMode.Block,
                CharacterAnimationTransitionReason.NormalCommand, ctx.AnimBrain.IsDowned);
    }

    Collider ResolvePlacementBody()
    {
        // Guard placement and recoil use the locomotion body, including companions.
        // The model's Position collider can move below the floor with root.x animation.
        return ctx != null && ctx.cc != null && ctx.cc.enabled ? ctx.cc :
            ctx != null && ctx.ColliderRefs != null ? ctx.ColliderRefs.CharacterPositionCollider : null;
    }

    bool TryGetFootprint(out CharacterPlacementFootprint footprint)
    {
        return CharacterPlacementFootprintUtility.TryGetColliderFootprint(ResolvePlacementBody(),
            ctx != null ? ctx.transform : transform, out footprint, out _);
    }

    public bool TryBeginSelf(PlayerContext issuer, DefensiveBlockAttack source, int id)
    {
        ctx?.ResolveReferences();
        if (!CanBeginSelf(issuer, source) || !source.CanAcceptCommand(issuer)) return false;
        Vector3 direction = source.transform.position - ctx.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < .01f || !TryGetFootprint(out var footprint)) return false;
        bool movementWasEnabled = issuer.movement != null && issuer.movement.enabled;
        if (!member.TryBeginTransientExecution(this, protectActor: false, allowCollisionOverride: false)) return false;
        attack = source; player = issuer; request = id; life = ctx.LifeGeneration;
        playerLife = player.LifeGeneration; sessionDefinition = ctx.baseStats;
        active = true; impact = false; elapsed = slideElapsed = slideApplied = 0f;
        arrived = true; pendingPlacement = null; slideFootprint = footprint;
        selfControlToken = ctx.stateHub != null ? ctx.stateHub.AcquireExternalControlBlockToken(
            ControlBlockFlags.Move | ControlBlockFlags.Rotate | ControlBlockFlags.Shoot | ControlBlockFlags.Skill) : 0;
        // The Player movement module also performs overlap separation, outside CanMove().
        selfMovement = issuer.movement;
        selfMovementWasEnabled = movementWasEnabled;
        if (selfMovement != null) selfMovement.enabled = false;
        ctx.transform.rotation = Quaternion.LookRotation(direction);
        ResolveActiveGuardOffset();
        ctx.AnimBrain.PlaybackEvent += OnPlayback;
        if (!ctx.AnimDriver.TryBeginBlock(id, animationProfile)) { Cancel(); return false; }
        Accepted?.Invoke(this);
        shotCamera = GameplayCameraController.Instance;
        TraceGuard("Accepted self guard");
        shotCamera?.BeginDefensiveBlockShot(this, player, ctx.transform, loadedProfile);
        Arrived?.Invoke(this);
        return active;
    }

    bool ResolveProfile()
    {
        var next = Settings;
        if (next == null || !next.IsConfigured) return false;
        if (next == loadedProfile) return true;
        loadedProfile = next;
        animationProfile = next.animation; worldLayers = next.worldLayers;
        standAhead = next.standAhead; minimumStandAhead = next.minimumStandAhead;
        approachClearance = next.approachClearance;
        guardHalfWidth = next.guardHalfWidth; guardHalfHeight = next.guardHalfHeight;
        guardHalfDepth = next.guardHalfDepth;
        guardCenterHeight = next.guardCenterHeight;
        guardForwardOffset = next.guardForwardOffset; timeoutSeconds = next.timeoutSeconds;
        activeGuardForwardOffset = guardForwardOffset;
        slideDistance = next.slideDistance; slideSeconds = next.slideSeconds;
        warpFadeOutSeconds = next.warpFadeOutSeconds; warpFadeInSeconds = next.warpFadeInSeconds;
        impactVfx = next.impactVfx;
        impactVfxLifetime = next.impactVfxLifetime;
        hitLagDuration = next.hitLagDuration; hitLagTimeScale = next.hitLagTimeScale;
        hitLagShape = next.hitLagShape != null && next.hitLagShape.length > 0 ? next.hitLagShape : null;
        return true;
    }

    bool TryResolveBeginPose(PlayerContext protectedPlayer, DefensiveBlockAttack source,
        out CharacterPlacementRequest placement, out CharacterPlacementResult pose,
        System.Text.StringBuilder diagnostics = null)
    {
        placement = null;
        pose = default;
        if (ctx == protectedPlayer || !CanStartGuard(protectedPlayer, source))
        { diagnostics?.AppendLine("  Rejected: companion guard state is not ready."); return false; }
        Vector3 direction = source.transform.position - protectedPlayer.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f)
        { diagnostics?.AppendLine("  Rejected: Player/caster planar separation is below 0.1 m."); return false; }
        float sourceDistance = direction.magnitude;
        direction.Normalize();
        Collider body = ResolvePlacementBody();
        if (!CharacterPlacementFootprintUtility.TryGetColliderFootprint(body, ctx.transform, out var footprint, out var footprintError))
        { diagnostics?.AppendLine("  Rejected: " + footprintError); return false; }
        float ahead = Mathf.Clamp(sourceDistance - approachClearance, minimumStandAhead, standAhead);
        Vector3 desired = protectedPlayer.transform.position + direction * ahead;
        diagnostics?.AppendLine($"  Guard profile='{Settings?.name}' actor={ctx.transform.position:F3} player={protectedPlayer.transform.position:F3} caster={source.transform.position:F3} distance={sourceDistance:F3} ahead={ahead:F3} min={minimumStandAhead:F3} max={standAhead:F3} clearance={approachClearance:F3} worldMask={worldLayers.value}");
        diagnostics?.AppendLine($"  Body='{body.name}' id={body.GetInstanceID()} enabled={body.enabled} shape={footprint.Shape} center={footprint.CenterOffset:F3} radius={footprint.Radius:F3} height={footprint.Height:F3} extents={footprint.HalfExtents:F3} scale={body.transform.lossyScale:F3}");
        placement = new CharacterPlacementRequest(ctx.transform, body, footprint, ctx.TargetIdentity,
            source.transform, CharacterPlacementRequest.AnchorSnapshot.Capture(source.transform),
            new[] { new CharacterPlacementRequest.Candidate(desired, Quaternion.LookRotation(direction), 0, 0) },
            null, 0f, null, worldLayers, ~0, null, this, false, false, true,
            // A static warp uses the exact body shape; inflated padding can touch the floor
            // and score a false world obstruction while the guard pose is being raised.
            runtimePolicy: CharacterPlacementRuntimePolicy.CreateDefault(true, 0.75f,
                QueryTriggerInteraction.Ignore, collisionPadding: 0f),
            poseValidator: diagnostics == null ? HasLandingGround :
                (position, rotation) => HasLandingGround(position, rotation, diagnostics));
        return TryResolveGuardPlacement(placement, out pose, diagnostics);
    }

    bool TryResolveGuardPlacement(CharacterPlacementRequest placement, out CharacterPlacementResult pose,
        System.Text.StringBuilder diagnostics = null)
    {
        if (!CharacterPlacementResolver.TryResolve(placement, CharacterPlacementReservationRegistry.Shared, out pose, diagnostics))
        { diagnostics?.AppendLine("  Resolver failed: " + pose.FailureReason); return false; }
        bool accepted = pose.Score.MaxWorldPenetration <= PlacementContactTolerance;
        diagnostics?.AppendLine($"  {(accepted ? "Accepted" : "Rejected: world penetration exceeds tolerance")}: resolved={pose.StartPosition:F3} worldPenetration={pose.Score.MaxWorldPenetration:F5} tolerance={PlacementContactTolerance:F5} actorPenetration={pose.Score.MaxActorPenetration:F5} (actor/reservation overlap is scored, not a Block rejection gate)");
        return accepted;
    }

    bool HasLandingGround(Vector3 position, Quaternion rotation) => HasLandingGround(position, rotation, null);

    bool HasLandingGround(Vector3 position, Quaternion rotation, System.Text.StringBuilder diagnostics)
    {
        // A baked NavMesh can remain above a removed/moved floor. Require nearby physical
        // support as well, both when accepting the command and when fade-out completes.
        const float tolerance = 0.2f;
        int count = Physics.RaycastNonAlloc(position + Vector3.up * tolerance, Vector3.down,
            landingGroundHits, tolerance * 2f, worldLayers, QueryTriggerInteraction.Ignore);
        diagnostics?.AppendLine($"  Landing ground: position={position:F3} tolerance={tolerance:F3} mask={worldLayers.value} hits={count}/{landingGroundHits.Length}");
        if (count == landingGroundHits.Length)
        { diagnostics?.AppendLine("  Rejected: landing ground query buffer full."); return false; }
        for (int i = 0; i < count; i++)
        {
            var hit = landingGroundHits[i];
            diagnostics?.AppendLine($"   Ground collider='{hit.collider.name}' id={hit.collider.GetInstanceID()} layer={hit.collider.gameObject.layer} point={hit.point:F3} normalY={hit.normal.y:F3} character={hit.collider.GetComponentInParent<CharacteContext>() != null}");
            if (hit.normal.y < 0.5f || hit.collider.GetComponentInParent<CharacteContext>() != null) continue;
            return true;
        }
        diagnostics?.AppendLine("  Rejected: no nearby physical floor with normalY >= 0.5 (character colliders excluded).");
        return false;
    }

    public bool TryBegin(PlayerContext protectedPlayer, DefensiveBlockAttack source, int id)
    {
        ctx?.ResolveReferences();
        if (!TryResolveBeginPose(protectedPlayer, source, out var placement, out var pose)) return false;
        if (!member.TryBeginTransientExecution(this, protectActor: false, allowCollisionOverride: false)) return false;
        if (!CharacterPlacementReservationRegistry.Shared.TryReserve(placement, pose, out _))
        {
            member.EndTransientExecution(this);
            return false;
        }
        attack = source; player = protectedPlayer; request = id; life = ctx.LifeGeneration;
        playerLife = player.LifeGeneration; sessionDefinition = ctx.baseStats;
        active = true; impact = false; elapsed = slideElapsed = slideApplied = 0f;
        arrived = false;
        pendingPlacement = placement;
        slideFootprint = placement.Footprint;
        ctx.AnimBrain.PlaybackEvent += OnPlayback;
        if (!ctx.AnimDriver.TryBeginBlock(id, animationProfile)) { Cancel(); return false; }
        Accepted?.Invoke(this);
        TraceGuard("Accepted companion; waiting for warp arrival");
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
        { TraceGuard("Warp arrival rejected: actor/request changed or reserved landing no longer safe"); Cancel(); return; }
        // Recheck the originally reserved point; never chase a moving player during fade-out.
        ctx.transform.SetPositionAndRotation(pose.StartPosition, pose.StartRotation);
        if (ctx is AllyContext companion && companion.agent != null && companion.agent.enabled && companion.agent.isOnNavMesh)
            companion.agent.nextPosition = pose.StartPosition;
        Physics.SyncTransforms();
        arrived = true;
        ResolveActiveGuardOffset();
        pendingPlacement = null;
        if (warpVisibility != null) warpVisibility.Appear(warpFadeInSeconds);
        shotCamera = GameplayCameraController.Instance;
        shotCamera?.BeginDefensiveBlockShot(this, player, transform, loadedProfile);
        Arrived?.Invoke(this);
        TraceGuard("Arrived at reserved guard position");
    }

    void ResolveActiveGuardOffset()
    {
        activeGuardForwardOffset = attack != null && attack.skill != null &&
            attack.skill.defensiveBlock?.mode == DefensiveBlockMode.Contact
            ? DefensiveBlockGeometry.ContactGuardOffset(transform.position, transform.forward,
                attack.CasterContext.transform.position, guardForwardOffset, guardHalfDepth)
            : guardForwardOffset;
    }

    public void ConfirmImpact(DefensiveBlockAttack source, int id)
    {
        if (!IsReadyFor(source, id) || !ctx.AnimDriver.TryBlockImpact(id, slideSeconds)) return;
        impact = true;
        if (loadedProfile != null && loadedProfile.impactCue != null)
            AudioService.Instance.PlayAtPosition(loadedProfile.impactCue, GuardCenter);
        Impacted?.Invoke(this);
        slideDirection = -ctx.transform.forward;
        if (hitLagDuration > 0f)
            GlobalTimeScaleManager.Instance.RequestHitLag(hitLagDuration, hitLagTimeScale, hitLagShape);
        if (impactVfx != null)
        {
            GameObject effect = Instantiate(impactVfx, GuardCenter, ctx.transform.rotation);
            Destroy(effect, impactVfxLifetime);
        }
    }
    void Update()
    {
        if (!active) return;
        if (ctx == null || ctx.LifeGeneration != life || ctx.HealthSystem == null || !ctx.HealthSystem.IsAlive ||
            ctx.baseStats != sessionDefinition || player == null || !player.isActiveAndEnabled ||
            player.LifeGeneration != playerLife || player.HealthSystem == null || !player.HealthSystem.IsAlive ||
            player.interruptionCommand == null || !player.interruptionCommand.defensiveBlockEnabled ||
            CutsceneDirector.IsCinematicPlaying || NpcPresentationController.IsActive ||
            (!impact && (attack == null || !attack.Matches(request))))
        { TraceGuard("Cancelled: actor/player/request invalid, profile changed or cinematic active"); Cancel(); return; }
        elapsed += ActorDeltaTime;
        if (!impact && !attack.IsTimedApproach && elapsed >= timeoutSeconds && ctx.AnimBrain.BlockPhase != BlockAnimationPhase.Exit)
        {
            TraceGuard("Timed out without hitbox interception");
            ctx.AnimDriver.EndBlock(request);
        }
    }
    void LateUpdate()
    {
        if (!active || !impact || slideElapsed >= slideSeconds) return;
        float dt = ActorDeltaTime;
        if (dt <= 0f) return;
        slideElapsed += dt;
        float next = slideDistance * Mathf.Clamp01(slideElapsed / slideSeconds);
        float distance = next - slideApplied;
        slideApplied = next;
        // Production companion position colliders are animated triggers. Sweep their accepted
        // placement footprint, rather than requiring a solid collider or following recoil bones.
        var shape = DefensiveBlockSlideMotor.ResolveShape(slideFootprint, ctx.transform);
        distance = CharacterBodySweepUtility.ResolveAllowedDistance(shape, slideDirection, distance,
            0.03f, ~0, QueryTriggerInteraction.Ignore, ctx.transform);
        Vector3 desired = ctx.transform.position + slideDirection * distance;
        if (NavMesh.Raycast(ctx.transform.position, desired, out var edge, NavMesh.AllAreas)) desired = edge.position;
        if (NavMesh.SamplePosition(desired, out var sample, 0.2f, NavMesh.AllAreas) && Mathf.Abs(sample.position.y - desired.y) < 0.2f)
        {
            // Self guard shares the Player vertical motor's controller. Companions are
            // positioned by the reserved footprint sweep while autonomy is suspended;
            // a second CC.Move can depenetrate an adjacent actor upward after the warp.
            if (ctx == player && ctx.cc != null && ctx.cc.enabled) ctx.cc.Move(sample.position - ctx.transform.position);
            else ctx.transform.position = sample.position;
        }
        if (ctx is AllyContext companion && companion.agent != null && companion.agent.enabled && companion.agent.isOnNavMesh)
            companion.agent.nextPosition = ctx.transform.position;
    }
    void OnPlayback(CharacterAnimBrain.PlaybackSignal signal)
    {
        if (signal.Kind == CharacterAnimBrain.PlaybackKind.Block && signal.RequestId == request &&
            (signal.Phase == CharacterAnimBrain.PlaybackPhase.Completed || signal.Phase == CharacterAnimBrain.PlaybackPhase.Interrupted))
        {
            TraceGuard("Block animation " + signal.Phase);
            Cancel();
        }
    }
    public void Cancel()
    {
        if (!active || restoring) return;
        TraceGuard($"Finished impact={impact} arrived={arrived} attackResult='{attack?.LastResult}' probe='{attack?.LastProbe}'");
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
        }
        member?.EndTransientExecution(this);
        if (selfControlToken != 0) ctx?.stateHub?.ReleaseExternalControlBlockToken(selfControlToken);
        selfControlToken = 0;
        if (selfMovement != null) selfMovement.enabled = selfMovementWasEnabled;
        selfMovement = null;
        CharacterPlacementReservationRegistry.Shared.ReleaseOwner(this);
        shotCamera?.EndDefensiveBlockShot(this, impact);
        shotCamera = null;
        Finished?.Invoke(this, impact);
        attack?.ReleaseDefender(this, request);
        attack = null; player = null; request = 0;
        restoring = false;
    }
    void OnDisable() { Cancel(); }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.matrix = Matrix4x4.TRS(GuardCenter, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(guardHalfWidth * 2f, guardHalfHeight * 2f, guardHalfDepth * 2f));
    }
}
