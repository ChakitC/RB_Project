using System;
using UnityEngine;

// One owner for the request and all three participants. Local guard scopes never resolve outcomes.
public sealed class CooperativeBlockSession
{
    readonly DefensiveBlockAttack source;
    readonly DefensiveBlockController playerGuard, allyGuard;
    readonly CooperativeBlockDamageSnapshot damage;
    readonly CooperativeBlockSettings settings;
    readonly CharacteContext caster;
    readonly int request, playerLife, allyLife, casterLife;
    readonly CharacterStats playerDefinition, allyDefinition, casterDefinition;
    CooperativeBlockProtection playerProtection, allyProtection, casterProtection;
    DefensiveBlockApproachMotor motor;
    CooperativeBlockMeter meter;
    GameplayCameraController camera;
    GameObject contactVfx;
    PlayerScreenEffectHandle struggleWind;
    GlobalTimeScaleManager clock;
    TimeSlowManager slow;
    int slowHandle;
    double lastTime, preparingElapsed, finishingElapsed;
    float finishingTimeout;
    bool committed, resolving, cleaned, paused;
    Vector3 playerPosition, allyPosition;
    public PlayerContext Player { get; }
    public CharacteContext Ally => allyGuard != null ? allyGuard.ActorContext : null;
    public CharacteContext Caster => caster;
    public int RequestId => request;
    public CooperativeBlockState State { get; private set; } = CooperativeBlockState.Preparing;
    public CooperativeBlockState? Outcome { get; private set; }
    public bool IsActive => State != CooperativeBlockState.Finished;
    public float Meter => meter != null ? meter.Value : settings.initialMeter;
    public float Remaining => meter != null ? meter.Remaining : settings.duration;
    public event Action<CooperativeBlockSession> Changed;
    public event Action<CooperativeBlockSession> PressAccepted;
    public event Action<CooperativeBlockSession, CooperativeBlockState> Ended;

    internal CooperativeBlockSession(DefensiveBlockAttack source, PlayerContext player,
        DefensiveBlockController helper, CooperativeBlockDamageSnapshot snapshot, CooperativeBlockSettings settings)
    {
        this.source = source; Player = player; playerGuard = player.DefensiveBlock; allyGuard = helper;
        caster = source.CasterContext; request = source.RequestId; damage = snapshot; this.settings = settings.Copy();
        playerLife = player.LifeGeneration; allyLife = Ally.LifeGeneration; casterLife = caster.LifeGeneration;
        playerDefinition = player.baseStats; allyDefinition = Ally.baseStats; casterDefinition = caster.baseStats;
    }

    internal bool Begin(CharacterPlacementRequest playerPose, CharacterPlacementRequest allyPose,
        Vector3 destination, CharacterPlacementFootprint footprint, float approachSeconds)
    {
        lastTime = Time.realtimeSinceStartupAsDouble;
        clock = GlobalTimeScaleManager.Instance; paused = clock.IsPaused;
        clock.PauseChanged += OnPauseChanged;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
        if (!playerGuard.TryBeginCooperative(this, Player, source, playerPose) ||
            !allyGuard.TryBeginCooperative(this, Player, source, allyPose) || !IsActive) return false;
        playerPosition = Player.transform.position; allyPosition = Ally.transform.position;
        if (!source.BeginCooperativeApproach(request, approachSeconds)) return false;
        committed = true;
        playerProtection = new CooperativeBlockProtection(Player);
        allyProtection = new CooperativeBlockProtection(Ally);
        casterProtection = new CooperativeBlockProtection(caster);
        motor = new DefensiveBlockApproachMotor(caster, footprint, destination, approachSeconds, source.worldLayers);
        slow = TimeSlowManager.Instance; slowHandle = slow.BeginPresentationSlow(settings.worldScale);
        camera = GameplayCameraController.Instance;
        camera?.BeginCooperativeBlockShot(this, Player, Ally.transform, caster.transform, playerGuard.Settings);
        return true;
    }

    static bool ValidActor(CharacteContext actor, int generation, CharacterStats definition) =>
        actor != null && actor.isActiveAndEnabled && actor.LifeGeneration == generation && actor.baseStats == definition &&
        actor.HealthSystem != null && actor.HealthSystem.IsAlive &&
        (actor.stateHub == null || actor.stateHub.LifeSM.CurrentId == LifeStateId.Alive);

    bool IsValid => source != null && source.isActiveAndEnabled && source.RequestId == request &&
        ValidActor(Player, playerLife, playerDefinition) && ValidActor(Ally, allyLife, allyDefinition) &&
        ValidActor(caster, casterLife, casterDefinition) && Player.interruptionCommand != null &&
        Player.interruptionCommand.isActiveAndEnabled && Player.interruptionCommand.defensiveBlockEnabled &&
        !CutsceneDirector.IsCinematicPlaying && !NpcPresentationController.IsActive;

    void OnPauseChanged(bool value)
    {
        AdvanceClock();
        paused = value;
    }

    void OnSceneChanged(UnityEngine.SceneManagement.Scene previous, UnityEngine.SceneManagement.Scene next) => Cancel();

    double AdvanceClock() => AdvanceClock(Time.realtimeSinceStartupAsDouble);

    double AdvanceClock(double now)
    {
        double delta = Math.Max(0d, now - lastTime); lastTime = Math.Max(lastTime, now);
        if (paused) return 0d;
        if (State == CooperativeBlockState.Struggling)
        {
            meter.Advance(delta);
            camera?.AdvanceCooperativeBlockShake(this, (float)delta);
            struggleWind?.Advance((float)delta);
        }
        return delta;
    }

    public void Tick()
    {
        if (!IsActive || resolving) return;
        if (!IsValid) { Cancel(); return; }
        double delta = AdvanceClock();
        if (State == CooperativeBlockState.Success)
        {
            if (paused) return;
            camera?.AdvanceCooperativeBlockShake(this, (float)delta);
            if (!playerGuard.IsExecuting || Player.AnimBrain.BlockImpactProgress >= .8f ||
                Player.AnimBrain.BlockPhase == BlockAnimationPhase.Exit) ReleasePresentation();
            // Active guards are World Slow exempt. Match their animation clock so HitLag
            // and global slow motion cannot consume the recovery watchdog early.
            finishingElapsed += Time.deltaTime;
            if (!playerGuard.IsExecuting && !allyGuard.IsExecuting) Finish();
            else if (finishingElapsed >= finishingTimeout)
            {
                source.TraceBlock("Cooperative recovery watchdog expired before guard completion");
                Finish();
            }
            return;
        }
        if (!source.OwnsCurrentSkill || !playerGuard.IsExecuting || !allyGuard.IsExecuting)
        { Cancel(); return; }
        if (paused) return;
        if (State == CooperativeBlockState.Preparing)
        {
            preparingElapsed += delta;
            if (preparingElapsed > 2d || Vector3.Distance(playerPosition, Player.transform.position) > .15f ||
                Vector3.Distance(allyPosition, Ally.transform.position) > .15f || !motor.Advance((float)delta))
            { Cancel(); return; }
            if (!source.AdvanceApproachPlayback(motor.Progress, out bool impactReady)) { Cancel(); return; }
            if (motor.Progress >= 1f && impactReady)
            {
                if (!caster.AnimDriver.TryHoldSkillApproach(request)) { Cancel(); return; }
                if (playerGuard.IsReadyFor(source, request) && allyGuard.IsReadyFor(source, request))
                {
                    if (!caster.AnimDriver.TryBeginEnemyCooperativeStruggle(request, settings.enemyStruggleClip, settings.enemyStruggleFadeSeconds))
                    { Cancel(); return; }
                    meter = new CooperativeBlockMeter(settings); State = CooperativeBlockState.Struggling;
                    camera?.BeginCooperativeBlockStruggle(this);
                    Vector3 windDirection = caster.transform.position - Player.transform.position;
                    struggleWind = Player.UIManager?.BeginScreenWindFullscreenEffect(windDirection, settings.duration);
                    Vector3 contactPoint = (playerGuard.GuardCenter + allyGuard.GuardCenter) * .5f;
                    contactVfx = playerGuard.SpawnImpactVfx(contactPoint);
                    playerGuard.PlayImpactCue(contactPoint);
                    Player.AnimDriver.BeginCooperativeStruggle(request); Ally.AnimDriver.BeginCooperativeStruggle(request);
                }
            }
        }
        if (State == CooperativeBlockState.Struggling && meter.State != CooperativeBlockState.Struggling)
            Resolve(meter.State);
        Changed?.Invoke(this);
    }

    public bool Press() => PressAt(Time.realtimeSinceStartupAsDouble);

    public bool PressAt(double eventTime)
    {
        // Input System timestamps share realtimeSinceStartup's epoch. Process event time before
        // this frame's Update, so queued edges are independent of render frame rate. Never rewind.
        if (!IsActive || paused || State != CooperativeBlockState.Struggling || double.IsNaN(eventTime) ||
            double.IsInfinity(eventTime) || eventTime < lastTime) return false;
        if (!IsValid || !source.OwnsCurrentSkill) { Cancel(); return false; }
        AdvanceClock(Math.Min(eventTime, Time.realtimeSinceStartupAsDouble));
        if (meter.State == CooperativeBlockState.Failure) { Resolve(CooperativeBlockState.Failure); return false; }
        if (!meter.Press()) return false;
        PressAccepted?.Invoke(this);
        if (meter.State == CooperativeBlockState.Success) Resolve(CooperativeBlockState.Success);
        Changed?.Invoke(this);
        return true;
    }

    void Resolve(CooperativeBlockState result)
    {
        if (resolving || Outcome.HasValue || !IsActive) return;
        if (!IsValid || !source.OwnsCurrentSkill) { Cancel(); return; }
        resolving = true;
        Outcome = result; State = result;
        struggleWind?.Dispose(); struggleWind = null;
        camera?.EndCooperativeBlockStruggle(this);
        source.ResolveCooperative(request, result);
        if (!IsValid)
        {
            Outcome = null; resolving = false; Cancel(); return;
        }
        motor?.Release(); motor = null;
        casterProtection?.Dispose(); casterProtection = null;
        if (result == CooperativeBlockState.Success)
        {
            camera?.PlayCooperativeBlockSuccess(this);
            var kb = KnockbackData.FromOrigin((Player.transform.position + Ally.transform.position) * .5f,
                caster.transform.position, source.knockbackDistance, source.knockbackSeconds, ImpactReactionKind.MiniStun, true, null);
            caster.KnockbackMotor?.ApplyKnockback(kb, forceReplace: true);
            playerGuard.ConfirmImpact(source, request);
            allyGuard.ConfirmImpact(source, request, false);
            finishingTimeout = Mathf.Max(Player.AnimBrain.BlockRecoveryDuration,
                Ally.AnimBrain.BlockRecoveryDuration) + 1f;
        }
        else
        {
            playerGuard.Cancel(); allyGuard.Cancel();
            ReleaseProtection();
            // Callbacks from the first hit can reset/despawn actors. Check generation before each result.
            if (IsValid) damage.Apply(Player);
            if (source != null && source.RequestId == request && ValidActor(caster, casterLife, casterDefinition) &&
                ValidActor(Ally, allyLife, allyDefinition)) damage.Apply(Ally);
        }
        resolving = false;
        Ended?.Invoke(this, result);
        if (result == CooperativeBlockState.Failure) Finish();
    }

    internal void OnGuardEnded(DefensiveBlockController guard, bool impacted)
    {
        if (guard == playerGuard)
        {
            playerProtection?.Dispose(); playerProtection = null;
            if (State == CooperativeBlockState.Success) ReleasePresentation();
        }
        if (guard == allyGuard) { allyProtection?.Dispose(); allyProtection = null; }
        if (!resolving && IsActive && State != CooperativeBlockState.Success) Cancel();
    }

    void ReleasePresentation()
    {
        camera?.EndDefensiveBlockShot(this, false);
        camera = null;
        // The boss resumes normal speed as the shot returns, even while the Ally recovers.
        // Release only this session's handle; other slow effects retain their ownership.
        if (slow != null) slow.EndPresentationSlow(slowHandle);
        slowHandle = 0;
        slow = null;
    }

    public void Cancel()
    {
        if (cleaned || resolving) return;
        resolving = true;
        bool notify = !Outcome.HasValue;
        if (notify) { State = CooperativeBlockState.Cancelled; Outcome = State; }
        if (committed && source != null) source.ResolveCooperative(request, CooperativeBlockState.Cancelled);
        Cleanup();
        resolving = false;
        if (notify) Ended?.Invoke(this, CooperativeBlockState.Cancelled);
        State = CooperativeBlockState.Finished;
        Changed?.Invoke(this);
    }

    void Finish()
    {
        if (cleaned) return;
        resolving = true; Cleanup(); State = CooperativeBlockState.Finished; resolving = false;
        Changed?.Invoke(this);
    }

    void ReleaseProtection()
    {
        playerProtection?.Dispose(); playerProtection = null;
        allyProtection?.Dispose(); allyProtection = null;
        casterProtection?.Dispose(); casterProtection = null;
    }

    void Cleanup()
    {
        if (cleaned) return;
        cleaned = true;
        struggleWind?.Dispose(); struggleWind = null;
        if (contactVfx != null) UnityEngine.Object.Destroy(contactVfx);
        contactVfx = null;
        playerGuard?.Cancel(); allyGuard?.Cancel();
        motor?.Release(); motor = null;
        ReleaseProtection();
        if (clock != null) clock.PauseChanged -= OnPauseChanged;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
        ReleasePresentation();
        Player?.interruptionCommand?.ClearCooperativeSession(this);
    }
}
