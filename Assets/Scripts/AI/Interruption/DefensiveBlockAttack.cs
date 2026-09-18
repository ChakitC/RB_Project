using System.Collections.Generic;
using UnityEngine;

// Skill-authored opt-in. The issuing Player selects the defender from its own party.
[DisallowMultipleComponent]
public sealed class DefensiveBlockAttack : MonoBehaviour
{
    public SkillGemDefinition skill;
    public AllyContext ally;
    [Min(0f)] public float commandRange = 8f;
    [Min(0.01f)] public float knockbackDistance = 2f;
    [Min(0.01f)] public float knockbackSeconds = 0.4f;
    public LayerMask worldLayers = 1;
    [Range(0f, 1f)] public float windowStartNormalized = 0f;
    [Range(0f, 1f)] public float windowEndNormalized = 0.62f;
    CharacteContext ctx;
    CharacterSkillManager subscribedManager;
    DefensiveBlockAttackProfile profile;
    SkillHitboxSequenceRuntime execution;
    int requestId;
    int life;
    bool resolved;
    SkillPreCastHoldHandle windupHold;
    float windupRemaining;
    int windupStartFrame;
    Vector3 previousRoot;
    float observedChargeSpeed;
    readonly Dictionary<CharacteContext, int> passedTargets = new Dictionary<CharacteContext, int>();
    readonly List<Collider> playerColliders = new List<Collider>();
    PlayerContext protectedPlayer;
    DefensiveBlockController defender;
    public DefensiveBlockController Defender => defender;
    Vector3 previousPlayerRoot;
    public CharacteContext CasterContext => ctx;
    public string LastResult { get; private set; } = "Idle";
    public string LastProbe { get; private set; } = "No probe";
    public int SuccessCount { get; private set; }
    public bool IsPreparingCharge => windupHold.IsValid;
    public bool WindowOpen => OwnsCurrentSkill && profile.IsConfigured && ctx.AnimBrain != null &&
        ctx.AnimBrain.TryGetActiveSkillNormalizedTime(requestId, out float time) &&
        time >= windowStartNormalized && time <= windowEndNormalized && !resolved;
    public bool OwnsCurrentSkill => isActiveAndEnabled && profile != null && ctx != null && ctx.LifeGeneration == life &&
        requestId > 0 && ctx.AnimBrain != null && ctx.AnimBrain.TryGetActiveSkillNormalizedTime(requestId, out _);

    void Awake()
    {
        ctx = GetComponentInParent<CharacteContext>(); ctx?.ResolveReferences();
    }
    void OnEnable() { RefreshSubscription(); }
    void RefreshSubscription()
    {
        ctx?.ResolveReferences();
        var next = ctx != null ? ctx.SkillManager : null;
        if (next == subscribedManager) return;
        if (subscribedManager != null) subscribedManager.CastStarted -= OnCastStarted;
        subscribedManager = next;
        if (subscribedManager != null) subscribedManager.CastStarted += OnCastStarted;
    }
    void Update()
    {
        if (ctx != null && ctx.SkillManager != subscribedManager) RefreshSubscription();
        if (!windupHold.IsValid) return;
        if (!OwnsCurrentSkill || ctx.HealthSystem == null || !ctx.HealthSystem.IsAlive)
        { ReleaseWindup(); return; }
        if (Time.frameCount == windupStartFrame) return;
        windupRemaining -= ctx.UsesWorldSlow ? TimeSlowManager.Instance.WorldDeltaTime : Time.deltaTime;
        if (windupRemaining <= 0f) ReleaseWindup();
    }

    void ReleaseWindup()
    {
        if (windupHold.IsValid) ctx?.AnimDriver?.ReleasePreCastHold(windupHold);
        windupHold = default;
        windupRemaining = 0f;
    }
    void OnCastStarted(ActiveSkillCastInfo cast)
    {
        ResetExecution();
        profile = cast.SkillDef != null ? cast.SkillDef.defensiveBlock : null;
        if (profile == null) return;
        skill = cast.SkillDef;
        commandRange = profile.commandRange; worldLayers = profile.worldLayers;
        windowStartNormalized = profile.windowStartNormalized; windowEndNormalized = profile.windowEndNormalized;
        knockbackDistance = profile.knockbackDistance; knockbackSeconds = profile.knockbackSeconds;
        requestId = cast.RequestId; life = ctx.LifeGeneration; resolved = false;
        previousRoot = ctx.transform.position;
        observedChargeSpeed = 0f;
        // CastStarted is raised after playback accepts the request, before its first advance.
        // Holding at the initial pose delays the actual attack, including payload and hitboxes,
        // while its command/telegraph window remains open. This also applies without a defender.
        if (profile.windupSeconds > 0f && ctx.AnimDriver != null &&
            ctx.AnimDriver.TryAcquirePreCastHold(requestId, 0f, 0.005f, out windupHold))
        {
            windupRemaining = profile.windupSeconds;
            windupStartFrame = Time.frameCount;
        }
    }
    public void Bind(SkillHitboxSequenceRuntime runtime, SkillCastContext cast)
    {
        if (!isActiveAndEnabled || profile == null || cast.SkillDef != skill || cast.CasterContext != ctx ||
            cast.RequestId != requestId || ctx.LifeGeneration != life) return;
        execution = runtime;
        requestId = cast.RequestId;
        life = ctx.LifeGeneration;
        previousRoot = ctx.transform.position;
    }
    public InterruptionCommandResult RequestBlock(PlayerContext player)
    {
        var eligibility = EvaluateBlockCommand(player, out string reason);
        if (eligibility != InterruptionCommandResult.Success) return Result(eligibility, reason);
        if (player.interruptionCommand == null ||
            !player.interruptionCommand.TrySelectDefensiveBlockDefender(this, out var selected))
            return Result(InterruptionCommandResult.NoAvailableAlly, "No available party defender");
        protectedPlayer = player;
        previousPlayerRoot = player.transform.position;
        player.GetComponentsInChildren(true, playerColliders);
        defender = selected;
        ally = selected.ActorContext as AllyContext; // Legacy/debug binding; selection is controller-owned.
        bool began = selected.ActorContext == player
            ? selected.TryBeginSelf(player, this, requestId) : selected.TryBegin(player, this, requestId);
        if (!began)
        {
            ally = null; defender = null;
            return Result(InterruptionCommandResult.NoAvailableAlly, "Ally unavailable or unsafe placement");
        }
        previousRoot = ctx.transform.position;
        return Result(InterruptionCommandResult.Success, selected.ActorContext == player ? "Player guard requested" : "Guard requested");
    }

    // Presentation uses the same dry-run rules as input, without reserving or moving Aires.
    public bool CanRequestBlock(PlayerContext player) =>
        player != null && player.interruptionCommand != null &&
        player.interruptionCommand.TrySelectDefensiveBlockDefender(this, out _);

    public bool CanAcceptCommand(PlayerContext player) =>
        EvaluateBlockCommand(player, out _) == InterruptionCommandResult.Success;

    public bool TryGetIncomingThreat(PlayerContext player, out float contactSeconds, bool requireReady = true)
    {
        contactSeconds = float.PositiveInfinity;
        if (player == null || !WindowOpen || ctx.HealthSystem == null || !ctx.HealthSystem.IsAlive ||
            (requireReady && !CanAcceptCommand(player))) return false;
        Vector3 forward = ctx.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return false;
        forward.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        float halfWidth = profile.threatHalfWidth;
        float reach = profile.threatForwardReach;
        if (execution != null && profile.AllowsStep(execution.ActiveStepIndex) &&
            execution.TryGetActiveBounds(out Bounds bounds))
        {
            halfWidth = Mathf.Abs(side.x) * bounds.extents.x + Mathf.Abs(side.z) * bounds.extents.z;
            reach = Vector3.Dot(bounds.center - ctx.transform.position, forward) +
                Mathf.Abs(forward.x) * bounds.extents.x + Mathf.Abs(forward.z) * bounds.extents.z;
        }
        var body = player.ColliderRefs != null ? player.ColliderRefs.CharacterPositionCollider : null;
        if (body != null)
        {
            Vector3 extents = body.bounds.extents;
            halfWidth += Mathf.Abs(side.x) * extents.x + Mathf.Abs(side.z) * extents.z;
            reach += Mathf.Abs(forward.x) * extents.x + Mathf.Abs(forward.z) * extents.z;
        }
        float speed = observedChargeSpeed > 0.1f ? observedChargeSpeed : profile.estimatedChargeSpeed;
        return DefensiveBlockGeometry.TryPredictThreat(ctx.transform.position, forward, player.transform.position,
            halfWidth, reach, speed, out contactSeconds);
    }

    InterruptionCommandResult EvaluateBlockCommand(PlayerContext player, out string reason)
    {
        reason = "Charge window closed";
        if (!WindowOpen || ctx.LifeGeneration != life)
            return InterruptionCommandResult.TargetWindowClosed;
        if (defender != null && defender.IsExecuting)
            return InterruptionCommandResult.SkillRejected;
        reason = "Actor unavailable";
        if (player == null || !player.isActiveAndEnabled || player.HealthSystem == null || !player.HealthSystem.IsAlive ||
            (player.stateHub != null && player.stateHub.LifeSM.CurrentId != LifeStateId.Alive) ||
            ctx.HealthSystem == null || !ctx.HealthSystem.IsAlive)
            return InterruptionCommandResult.NoValidTarget;
        reason = "Player contacted before guard";
        if (HasPassedTarget(player)) return InterruptionCommandResult.TargetWindowClosed;
        if (player.DefensiveBlock != null && player.DefensiveBlock.IsExecuting)
            return InterruptionCommandResult.SkillRejected;
        Vector3 delta = ctx.transform.position - player.transform.position;
        reason = "Different floor";
        if (Mathf.Abs(delta.y) > 1f) return InterruptionCommandResult.NoValidTarget;
        delta.y = 0f;
        reason = "Out of range";
        if (delta.magnitude > commandRange) return InterruptionCommandResult.NoValidTarget;
        reason = "Obstructed";
        if (Physics.Linecast(player.transform.position + Vector3.up, ctx.transform.position + Vector3.up,
            worldLayers, QueryTriggerInteraction.Ignore)) return InterruptionCommandResult.TeleportFailed;
        if (player.interruptionCommand == null || !player.interruptionCommand.defensiveBlockEnabled ||
            (GameplayCameraController.Instance != null && !GameplayCameraController.Instance.GameplayInputEnabled))
            return InterruptionCommandResult.SkillRejected;
        reason = "Ready";
        return InterruptionCommandResult.Success;
    }
    InterruptionCommandResult Result(InterruptionCommandResult result, string message)
    { LastResult = message; return result; }

    bool HasPassedTarget(CharacteContext target) => target != null &&
        passedTargets.TryGetValue(target, out int generation) && generation == target.LifeGeneration;

    // Called only after this execution actually applies damage. Never listens to unrelated damage.
    public void NotifyDamageApplied(SkillHitboxSequenceRuntime runtime, int id, int casterLife, CharacteContext target)
    {
        if (runtime == null || runtime != execution || id != requestId || ctx == null ||
            casterLife != life || ctx.LifeGeneration != life || resolved || target == null) return;
        passedTargets[target] = target.LifeGeneration;
        if (target == protectedPlayer) RejectLateGuard();
    }

    void RejectLateGuard()
    {
        if (protectedPlayer != null) passedTargets[protectedPlayer] = protectedPlayer.LifeGeneration;
        LastResult = "Missed: Player contacted before guard";
        // Release only the defender. The enemy's cast, remaining hitboxes and paid costs continue.
        defender?.CancelFor(this, requestId);
    }

    float PlayerContactFraction(SkillHitboxSequenceRuntime runtime, Vector3 previous, Bounds hitbox,
        Vector3 forward, out float playerFront)
    {
        float earliest = float.PositiveInfinity;
        playerFront = float.NegativeInfinity;
        if (protectedPlayer == null) return earliest;
        Vector3 movement = protectedPlayer.transform.position - previousPlayerRoot;
        foreach (var collider in playerColliders)
        {
            if (!runtime.CanDamageCollider(collider) ||
                DamageableResolver.ResolveFrom(collider) != protectedPlayer.HealthSystem) continue;
            Bounds target = collider.bounds;
            if (DefensiveBlockGeometry.TrySweepBounds(previous, hitbox.center, hitbox.extents,
                target.center - movement, target, out float fraction) && fraction <= earliest)
            {
                float front = Vector3.Dot(target.center - movement, forward) +
                    Mathf.Abs(forward.x) * target.extents.x + Mathf.Abs(forward.z) * target.extents.z;
                playerFront = fraction < earliest ? front : Mathf.Max(playerFront, front);
                earliest = fraction;
            }
        }
        return earliest;
    }

    public bool TryIntercept(SkillHitboxSequenceRuntime runtime)
    {
        if (!isActiveAndEnabled || resolved || runtime != execution || ctx == null || !WindowOpen ||
            ctx.HealthSystem == null || !ctx.HealthSystem.IsAlive ||
            ctx.LifeGeneration != life || !profile.AllowsStep(runtime.ActiveStepIndex) || defender == null ||
            !defender.IsReadyFor(this, requestId)) return false;
        if (!runtime.TryGetActiveBounds(out Bounds bounds)) return false;
        var guard = defender;
        if (HasPassedTarget(guard.ProtectedPlayer)) { RejectLateGuard(); return false; }
        Vector3 forward = guard.transform.forward;
        if (Vector3.Dot(previousRoot - guard.ActorContext.transform.position, forward) < 0f) return false;
        if (Vector3.Dot(ctx.transform.forward, forward) > -0.25f) return false;
        float radius = Mathf.Abs(forward.x) * bounds.extents.x + Mathf.Abs(forward.z) * bounds.extents.z;
        Vector3 previous = bounds.center + previousRoot - ctx.transform.position;
        LastProbe = $"step={runtime.ActiveStepIndex} previous={previous:F2} current={bounds.center:F2} radius={radius:F2} guard={guard.GuardCenter:F2} forward={forward:F2}";
        if (!DefensiveBlockGeometry.TrySweepGuard(previous, bounds.center, radius,
            guard.GuardCenter, forward, guard.guardHalfWidth, guard.guardHalfHeight, guard.guardHalfDepth,
            out float guardFraction)) return false;
        float playerFraction = PlayerContactFraction(runtime, previous, bounds, forward, out float playerFront);
        float guardLead = Vector3.Dot(guard.GuardCenter, forward) + guard.guardHalfDepth - playerFront;
        LastProbe += $" guardT={guardFraction:F3} playerT={playerFraction:F3} guardLead={guardLead:F3}";
        if (!DefensiveBlockGeometry.GuardContactWins(guardFraction, playerFraction, guardLead))
        {
            RejectLateGuard();
            return false;
        }
        resolved = true;
        // Must happen synchronously before any collider contact can damage another actor.
        runtime.StopExecution(requestId);
        ctx.AnimDriver?.CancelSkillCastRequest(requestId);
        var kb = KnockbackData.FromOrigin(guard.ActorContext.transform.position, ctx.transform.position,
            knockbackDistance, knockbackSeconds, ImpactReactionKind.MiniStun, true, null);
        bool pushed = ctx.KnockbackMotor != null && ctx.KnockbackMotor.ApplyKnockback(kb, forceReplace: true);
        guard.ConfirmImpact(this, requestId);
        SuccessCount++;
        LastResult = pushed ? "Blocked: Rector knocked back" : "Blocked: knockback rejected";
        return true;
    }
    void LateUpdate()
    {
        if (ctx == null) return;
        if (Time.deltaTime > 0f)
        {
            Vector3 movement = ctx.transform.position - previousRoot;
            movement.y = 0f;
            observedChargeSpeed = Mathf.Max(0f, Vector3.Dot(movement, ctx.transform.forward) / Time.deltaTime);
        }
        if (execution != null) TryIntercept(execution);
        previousRoot = ctx.transform.position;
        if (protectedPlayer != null) previousPlayerRoot = protectedPlayer.transform.position;
    }
    public bool Matches(int request) => isActiveAndEnabled && !resolved && OwnsCurrentSkill &&
        request == requestId && ctx != null && ctx.LifeGeneration == life && ctx.HealthSystem != null && ctx.HealthSystem.IsAlive;
    public void ResetExecution()
    {
        ReleaseWindup();
        if (ctx != null && ctx.LifeGeneration == life)
        {
            execution?.StopExecution(requestId);
            ctx.AnimDriver?.CancelSkillCastRequest(requestId);
        }
        resolved = true;
        execution = null;
        defender?.CancelFor(this, requestId);
        ally = null; defender = null;
        requestId = 0;
        profile = null;
        passedTargets.Clear();
        playerColliders.Clear();
        protectedPlayer = null;
    }
    public void ReleaseDefender(DefensiveBlockController released, int id)
    {
        if (requestId == id && defender == released) { ally = null; defender = null; }
    }
    void OnDisable()
    {
        ResetExecution();
        if (subscribedManager != null) subscribedManager.CastStarted -= OnCastStarted;
        subscribedManager = null;
    }
}
