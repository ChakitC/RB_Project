using UnityEngine;

// Opt-in on a test actor: does not alter ordinary pre-cast interruption.
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
    SkillHitboxSequenceRuntime execution;
    int requestId;
    int life;
    bool resolved;
    Vector3 previousRoot;
    public string LastResult { get; private set; } = "Idle";
    public string LastProbe { get; private set; } = "No probe";
    public int SuccessCount { get; private set; }
    public bool WindowOpen => OwnsCurrentSkill && ctx.AnimBrain != null &&
        ctx.AnimBrain.TryGetActiveSkillNormalizedTime(requestId, out float time) &&
        time >= windowStartNormalized && time <= windowEndNormalized && !resolved;
    public bool OwnsCurrentSkill => isActiveAndEnabled && ctx != null && ctx.LifeGeneration == life &&
        requestId > 0 && ctx.AnimBrain != null && ctx.AnimBrain.TryGetActiveSkillNormalizedTime(requestId, out _);

    void Awake()
    {
        ctx = GetComponentInParent<CharacteContext>(); ctx?.ResolveReferences();
        if (ctx != null && ctx.SkillManager != null) ctx.SkillManager.CastStarted += OnCastStarted;
    }
    void OnCastStarted(ActiveSkillCastInfo cast)
    {
        if (cast.SkillDef != skill) return;
        requestId = cast.RequestId; life = ctx.LifeGeneration; resolved = false;
        previousRoot = ctx.transform.position;
    }
    public void Bind(SkillHitboxSequenceRuntime runtime, SkillCastContext cast)
    {
        if (!isActiveAndEnabled || cast.SkillDef != skill || cast.CasterContext != ctx) return;
        execution = runtime;
        requestId = cast.RequestId;
        life = ctx.LifeGeneration;
        previousRoot = ctx.transform.position;
        resolved = false;
    }
    public InterruptionCommandResult RequestBlock(PlayerContext player)
    {
        var eligibility = EvaluateBlockCommand(player, out string reason);
        if (eligibility != InterruptionCommandResult.Success) return Result(eligibility, reason);
        if (!ally.DefensiveBlock.TryBegin(player, this, requestId))
            return Result(InterruptionCommandResult.NoAvailableAlly, "Ally unavailable or unsafe placement");
        previousRoot = ctx.transform.position;
        return Result(InterruptionCommandResult.Success, "Guard requested");
    }

    // Presentation uses the same dry-run rules as input, without reserving or moving Aires.
    public bool CanRequestBlock(PlayerContext player) =>
        EvaluateBlockCommand(player, out _) == InterruptionCommandResult.Success;

    InterruptionCommandResult EvaluateBlockCommand(PlayerContext player, out string reason)
    {
        reason = "Charge window closed";
        if (!WindowOpen || ctx.LifeGeneration != life)
            return InterruptionCommandResult.TargetWindowClosed;
        reason = "Actor unavailable";
        if (player == null || !player.isActiveAndEnabled || player.HealthSystem == null || !player.HealthSystem.IsAlive ||
            (player.stateHub != null && player.stateHub.LifeSM.CurrentId != LifeStateId.Alive) ||
            ctx.HealthSystem == null || !ctx.HealthSystem.IsAlive)
            return InterruptionCommandResult.NoValidTarget;
        Vector3 delta = ctx.transform.position - player.transform.position;
        reason = "Different floor";
        if (Mathf.Abs(delta.y) > 1f) return InterruptionCommandResult.NoValidTarget;
        delta.y = 0f;
        reason = "Out of range";
        if (delta.magnitude > commandRange) return InterruptionCommandResult.NoValidTarget;
        reason = "Obstructed";
        if (Physics.Linecast(player.transform.position + Vector3.up, ctx.transform.position + Vector3.up,
            worldLayers, QueryTriggerInteraction.Ignore)) return InterruptionCommandResult.TeleportFailed;
        reason = "Ally unavailable or unsafe placement";
        if (ally == null || ally.DefensiveBlock == null || !ally.DefensiveBlock.CanBegin(player, this))
            return InterruptionCommandResult.NoAvailableAlly;
        reason = "Ready";
        return InterruptionCommandResult.Success;
    }
    InterruptionCommandResult Result(InterruptionCommandResult result, string message)
    { LastResult = message; return result; }

    public bool TryIntercept(SkillHitboxSequenceRuntime runtime)
    {
        if (!isActiveAndEnabled || resolved || runtime != execution || ctx == null || !WindowOpen ||
            ctx.HealthSystem == null || !ctx.HealthSystem.IsAlive ||
            ctx.LifeGeneration != life || runtime.ActiveStepIndex < 0 || runtime.ActiveStepIndex > 1 || ally == null ||
            ally.DefensiveBlock == null || !ally.DefensiveBlock.IsReadyFor(this, requestId)) return false;
        if (!runtime.TryGetActiveBounds(out Bounds bounds)) return false;
        var guard = ally.DefensiveBlock;
        Vector3 forward = guard.transform.forward;
        if (Vector3.Dot(previousRoot - ally.transform.position, forward) < 0f) return false;
        if (Vector3.Dot(ctx.transform.forward, forward) > -0.25f) return false;
        float radius = Mathf.Abs(forward.x) * bounds.extents.x + Mathf.Abs(forward.z) * bounds.extents.z;
        Vector3 previous = bounds.center + previousRoot - ctx.transform.position;
        LastProbe = $"step={runtime.ActiveStepIndex} previous={previous:F2} current={bounds.center:F2} radius={radius:F2} guard={guard.GuardCenter:F2} forward={forward:F2}";
        if (!DefensiveBlockGeometry.SweepsGuard(previous, bounds.center, radius,
            guard.GuardCenter, forward, guard.guardHalfWidth, guard.guardHalfHeight)) return false;
        resolved = true;
        // Must happen synchronously before any collider contact can damage another actor.
        runtime.StopExecution(requestId);
        ctx.AnimDriver?.CancelSkillCastRequest(requestId);
        var kb = KnockbackData.FromOrigin(ally.transform.position, ctx.transform.position,
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
        if (execution != null) TryIntercept(execution);
        previousRoot = ctx.transform.position;
    }
    public bool Matches(int request) => isActiveAndEnabled && !resolved && OwnsCurrentSkill &&
        request == requestId && ctx != null && ctx.LifeGeneration == life && ctx.HealthSystem != null && ctx.HealthSystem.IsAlive;
    public void ResetExecution()
    {
        if (ctx != null && ctx.LifeGeneration == life)
        {
            execution?.StopExecution(requestId);
            ctx.AnimDriver?.CancelSkillCastRequest(requestId);
        }
        resolved = true;
        execution = null;
        ally?.DefensiveBlock?.CancelFor(this, requestId);
    }
    void OnDisable() { ResetExecution(); }
    void OnDestroy() { if (ctx != null && ctx.SkillManager != null) ctx.SkillManager.CastStarted -= OnCastStarted; }
}
