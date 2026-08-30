using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns one enemy's Special Shoot Point challenge: the round state machine, anchor rotation, the
/// runtime point pool, the round timers, the outcome a Behavior Tree reads, and the stagger
/// transaction plus Special Point Mini Stun request that a completed round produces.
///
/// Opt-in and enemy-only. An enemy participates when its prefab carries this component, a profile,
/// and at least as many usable anchors as the round needs; anything less rejects the trigger rather
/// than starting a degraded round. Resolved through <see cref="EnemyContext"/> so peer modules are
/// read from the context hub instead of directional hierarchy walks.
/// </summary>
[DefaultExecutionOrder(120)]
[DisallowMultipleComponent]
public sealed class SpecialShootPointController : MonoBehaviour
{
    [Header("Profile")]
    [Tooltip("Shared timing, HP/stagger ratios, pool prefab, and presentation. Required.")]
    [SerializeField] private SpecialShootPointProfileSO profile;

    [Header("Anchors")]
    [Tooltip("Anchor list on the visual model. Resolved automatically and re-resolved on a model " +
             "rebuild; assign only to pin a specific set.")]
    [SerializeField] private SpecialShootPointAnchorSet anchorSet;

    [Tooltip("Fallback anchors authored directly on this object. Used only when no " +
             "SpecialShootPointAnchorSet is present — appropriate for actors whose bones are not " +
             "rebuilt at runtime, such as turrets.")]
    [SerializeField] private List<SpecialShootPointAnchor> anchors = new();

    [Header("Refs")]
    [SerializeField] private EnemyContext ctx;

    [Tooltip("Optional. Pooled points are parked here between rounds. Created at runtime when empty.")]
    [SerializeField] private Transform poolRoot;

    readonly SpecialShootPointShuffleBag _shuffleBag = new();
    readonly List<int> _drawnAnchorIndices = new();
    readonly List<SpecialShootPointInstance> _pool = new();
    readonly List<SpecialShootPointInstance> _activePoints = new();

    SpecialShootPointPhase _phase = SpecialShootPointPhase.Idle;
    int _requestIdCounter;
    int _currentRequestId;
    int _lastOutcomeRequestId;
    SpecialShootPointOutcome _lastOutcome = SpecialShootPointOutcome.None;

    float _phaseTimeRemaining;
    float _activeDuration;
    float _cooldownRemaining;

    int _pointsRemaining;
    bool _warningActive;
    bool _authoringWarningLogged;

    StaggerMeter _meter;
    HealthSystem _health;
    CharacterAnimBrain _brain;
    CharacterAnimDriver _animDriver;
    bool _subscribed;

    int _reactionRequestId;
    bool _reactionPending;

    /// <summary>Phase changed. Carries the new phase.</summary>
    public event Action<SpecialShootPointPhase> PhaseChanged;

    /// <summary>Remaining and total active-window seconds. The HUD owns the shared countdown.</summary>
    public event Action<float, float> ActiveTimeChanged;

    /// <summary>Points still standing, and how many the round started with.</summary>
    public event Action<int, int> PointsRemainingChanged;

    /// <summary>The round reached a terminal outcome. Carries the round's request id.</summary>
    public event Action<SpecialShootPointOutcome, int> RoundResolved;

    /// <summary>
    /// A round opened anywhere in the scene. The HUD is a single shared presenter rather than one
    /// widget per enemy, so it needs to hear about rounds it has no reference to.
    /// </summary>
    public static event Action<SpecialShootPointController> AnyRoundStarted;

    /// <summary>A round anywhere in the scene reached a terminal outcome.</summary>
    public static event Action<SpecialShootPointController, SpecialShootPointOutcome> AnyRoundResolved;

    public SpecialShootPointPhase Phase => _phase;

    /// <summary>What a Behavior Tree calls "a round is running".</summary>
    public bool IsRoundActive =>
        _phase == SpecialShootPointPhase.Telegraph ||
        _phase == SpecialShootPointPhase.Active ||
        _phase == SpecialShootPointPhase.Resolving;

    public SpecialShootPointOutcome LastOutcome => _lastOutcome;

    /// <summary>The round the outcome belongs to. Zero when no round has ever resolved.</summary>
    public int LastOutcomeRequestId => _lastOutcomeRequestId;

    public int CurrentRequestId => _currentRequestId;

    public SpecialShootPointProfileSO Profile => profile;

    public IReadOnlyList<SpecialShootPointInstance> ActivePoints => _activePoints;

    public int PointsRemaining => _pointsRemaining;

    public float ActiveTimeRemaining => _phase == SpecialShootPointPhase.Active ? _phaseTimeRemaining : 0f;

    public float ActiveDuration => _activeDuration;

    public Transform PoolRoot => poolRoot != null ? poolRoot : transform;

    /// <summary>The owning enemy's stagger meter, for the direct-hit transaction.</summary>
    public StaggerMeter Meter => _meter;

    /// <summary>
    /// The owning enemy as a damageable. The direct-hit scope compares against this so a shot that
    /// resolved some other actor can never feed this enemy's points.
    /// </summary>
    public IDamageable OwnerDamageable => _health;

    public EnemyContext Context => ctx;

    /// <summary>
    /// Gameplay time, not unscaled time: the whole challenge stretches with world slow exactly like
    /// the combat around it.
    /// </summary>
    public float DeltaTime =>
        ctx == null || ctx.UsesWorldSlow
            ? TimeSlowManager.Instance.WorldDeltaTime
            : Time.deltaTime;

    // ----- Unity lifecycle -----

    void Awake()
    {
        ResolveRefs();
    }

    void OnEnable()
    {
        ResolveRefs();
        Subscribe();

        if (_phase == SpecialShootPointPhase.Disabled)
            SetPhase(SpecialShootPointPhase.Idle);
    }

    void OnDisable()
    {
        Unsubscribe();
        CancelRound(SpecialShootPointOutcome.Cancelled, startCooldown: false);
        SetPhase(SpecialShootPointPhase.Disabled);
    }

    void Update()
    {
        // Idle with no cooldown and nothing left fading is the common case for every enemy that
        // owns this component, so it costs a couple of comparisons per frame and nothing else.
        // _activePoints is part of the check because a resolved round's points still need their
        // fade ticked to completion or they never return to the pool.
        if (_phase == SpecialShootPointPhase.Idle && _cooldownRemaining <= 0f && _activePoints.Count == 0)
            return;

        if (_phase == SpecialShootPointPhase.Disabled)
            return;

        float dt = DeltaTime;
        if (dt <= 0f)
            return;

        TickCooldown(dt);
        TickPoints(dt);

        switch (_phase)
        {
            case SpecialShootPointPhase.Telegraph:
                TickTelegraph(dt);
                break;

            case SpecialShootPointPhase.Active:
                TickActive(dt);
                break;
        }
    }

    // ----- Public round API -----

    /// <summary>
    /// Starts a round. Every rejection reason is a plain false: the Behavior Tree trigger reports
    /// <c>Failure</c> and the tree decides what to do instead.
    /// </summary>
    /// <param name="requestedCount">
    /// Behavior Tree override, clamped to the profile ceiling and the usable anchor count. Zero or
    /// less uses the profile default.
    /// </param>
    public bool TryStartRound(int requestedCount = 0)
    {
        if (!CanStartRound())
            return false;

        int count = profile.ResolvePointCount(requestedCount, CountUsableAnchors());
        if (count <= 0)
            return false;

        // The bag resets itself when the source count changes, which is what a model rebuild with a
        // different anchor set looks like from here.
        if (!_shuffleBag.TryDraw(AnchorCount, count, IsAnchorUsable, _drawnAnchorIndices))
            return false;

        _currentRequestId = ++_requestIdCounter;
        _activePoints.Clear();

        float pointHealth = profile.ResolvePointHealth(ResolveOwnerMaxHealth());

        for (int i = 0; i < _drawnAnchorIndices.Count; i++)
        {
            SpecialShootPointAnchor anchor = GetAnchor(_drawnAnchorIndices[i]);
            SpecialShootPointInstance point = RentPoint();
            if (point == null)
            {
                // Authoring is broken mid-round. Undo rather than run a partial challenge.
                ReleaseAllPoints(silent: true);
                _currentRequestId = 0;
                LogAuthoringErrorOnce("Runtime point prefab could not be instantiated.");
                return false;
            }

            point.Bind(this, profile, anchor, pointHealth);
            _activePoints.Add(point);
        }

        _pointsRemaining = _activePoints.Count;
        _warningActive = false;
        _activeDuration = Mathf.Max(0.05f, profile.activeDuration);

        SetPhase(SpecialShootPointPhase.Telegraph);
        _phaseTimeRemaining = Mathf.Max(0f, profile.telegraphDuration);
        PointsRemainingChanged?.Invoke(_pointsRemaining, _activePoints.Count);
        AnyRoundStarted?.Invoke(this);

        // A zero telegraph is legal authoring; opening the window immediately keeps the phase
        // machine honest instead of leaving a frame of unhittable points.
        if (_phaseTimeRemaining <= 0f)
            EnterActivePhase();

        return true;
    }

    /// <summary>
    /// Whether a trigger would be accepted right now. Kept separate from
    /// <see cref="TryStartRound"/> so authoring validation and the editor can ask without side
    /// effects.
    /// </summary>
    public bool CanStartRound()
    {
        if (!isActiveAndEnabled || _phase != SpecialShootPointPhase.Idle || _cooldownRemaining > 0f)
            return false;

        if (profile == null || profile.runtimePointPrefab == null)
        {
            LogAuthoringErrorOnce("Missing profile or runtime point prefab.");
            return false;
        }

        if (CountUsableAnchors() <= 0)
        {
            LogAuthoringErrorOnce("No usable Special Shoot Point anchors are authored.");
            return false;
        }

        ResolveRefs();

        if (_health != null && !_health.IsAlive)
            return false;

        if (_meter != null &&
            (_meter.IsStaggered ||
             _meter.IsChainReady ||
             _meter.IsChainExecutionActive ||
             _meter.HasPendingSpecialPointBreak))
        {
            return false;
        }

        // Post-Stagger immunity rejects a new round outright: a challenge that could not award its
        // reward is not worth starting.
        if (_meter != null && _meter.IsInPostStaggerImmunity)
            return false;

        if (_brain != null &&
            (_brain.IsStageIntroPlaybackActive ||
             _brain.IsChainPlaybackActive ||
             _brain.IsSpecialReactionPlaybackActive))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Eligibility gate for the direct-hit path. A point may only take damage in the Active phase,
    /// from a hit credited to the player.
    /// </summary>
    public bool AcceptsPointDamageFrom(SpecialShootPointInstance point, GameObject creditedActor)
    {
        if (_phase != SpecialShootPointPhase.Active)
            return false;

        if (point == null || point.Owner != this || !point.IsHittable)
            return false;

        return IsPlayerCredit(creditedActor);
    }

    /// <summary>
    /// Applies one accepted direct-hit result to a point and, when it was the last one, resolves the
    /// round as a success.
    ///
    /// Called from inside <see cref="SpecialShootPointHitScope"/>, which has already opened the
    /// meter's direct-hit deferral, so the reward lands in the same transaction as the shot's own
    /// stagger.
    /// </summary>
    /// <returns>True when the point broke.</returns>
    public bool ApplyPointDamage(SpecialShootPointInstance point, float appliedDamage, GameObject creditedActor)
    {
        if (!AcceptsPointDamageFrom(point, creditedActor))
            return false;

        if (!point.ApplyPointDamage(appliedDamage))
            return false;

        point.PlayBreak();
        _pointsRemaining = Mathf.Max(0, _pointsRemaining - 1);
        PointsRemainingChanged?.Invoke(_pointsRemaining, _activePoints.Count);

        if (_pointsRemaining > 0)
            return true;

        ResolveSuccess(creditedActor);
        return true;
    }

    /// <summary>
    /// Ends the round with no reward and no reaction. Used by death, down, a cinematic, an
    /// unrelated ChainReady, and component teardown.
    /// </summary>
    public void CancelRound(SpecialShootPointOutcome outcome = SpecialShootPointOutcome.Cancelled, bool startCooldown = true)
    {
        if (!IsRoundActive)
            return;

        CancelPendingReaction();
        ReleaseAllPoints(silent: true);

        if (_meter != null)
            _meter.CancelPendingSpecialPointBreak();

        FinishRound(outcome, startCooldown);
    }

    // ----- Phase machine -----

    void TickTelegraph(float dt)
    {
        _phaseTimeRemaining -= dt;
        if (_phaseTimeRemaining > 0f)
            return;

        if (ShouldCancelForOwnership())
        {
            CancelRound();
            return;
        }

        EnterActivePhase();
    }

    void EnterActivePhase()
    {
        SetPhase(SpecialShootPointPhase.Active);
        _phaseTimeRemaining = _activeDuration;

        for (int i = 0; i < _activePoints.Count; i++)
            _activePoints[i].SetHittable(true);

        ActiveTimeChanged?.Invoke(_phaseTimeRemaining, _activeDuration);
    }

    void TickActive(float dt)
    {
        if (ShouldCancelForOwnership())
        {
            CancelRound();
            return;
        }

        _phaseTimeRemaining = Mathf.Max(0f, _phaseTimeRemaining - dt);
        ActiveTimeChanged?.Invoke(_phaseTimeRemaining, _activeDuration);

        bool warning = _phaseTimeRemaining <= Mathf.Max(0f, profile.lastSecondWarningThreshold);
        if (warning != _warningActive)
        {
            _warningActive = warning;
            for (int i = 0; i < _activePoints.Count; i++)
                _activePoints[i].SetWarningActive(warning);
        }

        if (_phaseTimeRemaining > 0f)
            return;

        ResolveTimeout();
    }

    void TickCooldown(float dt)
    {
        if (_cooldownRemaining > 0f)
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - dt);

        // The phase transition is checked independently of whether time was actually burned this
        // frame. Folding it into the countdown made an expired timer that is still in the Cooldown
        // phase unrecoverable: the early-out fired first and the controller stayed in Cooldown
        // forever, rejecting every future trigger.
        if (_cooldownRemaining <= 0f && _phase == SpecialShootPointPhase.Cooldown)
            SetPhase(SpecialShootPointPhase.Idle);
    }

    void TickPoints(float dt)
    {
        bool lostAPoint = false;

        // Points fade on the round's clock, not their own: a fade that outlived a world slow would
        // disagree with the challenge it belongs to.
        for (int i = _activePoints.Count - 1; i >= 0; i--)
        {
            SpecialShootPointInstance point = _activePoints[i];
            if (point == null)
            {
                _activePoints.RemoveAt(i);
                lostAPoint = true;
                continue;
            }

            point.TickPresentation(dt);

            // The fade finished and the point parked itself back in the pool.
            if (!point.IsBound)
                _activePoints.RemoveAt(i);
        }

        // A live point can only vanish if something destroyed it — a model rebuild takes the bones
        // the points are parented to with it. The round can no longer be completed or failed
        // fairly, so it is cancelled rather than left with an unreachable points-remaining count.
        if (lostAPoint && (_phase == SpecialShootPointPhase.Telegraph || _phase == SpecialShootPointPhase.Active))
            CancelRound();
    }

    // ----- Outcomes -----

    void ResolveTimeout()
    {
        // Deliberately does NOT clear _activePoints. Extinguishing starts a fade, and the fade is
        // driven by TickPoints — dropping the points here would strand them mid-fade, still active
        // and still parented to a bone, and they would never return to the pool.
        for (int i = 0; i < _activePoints.Count; i++)
            _activePoints[i].PlayTimeoutExtinguish();

        FinishRound(SpecialShootPointOutcome.TimedOut, startCooldown: true);
    }

    /// <summary>
    /// The final point broke. Runs inside the still-open direct-hit deferral, so the reward is part
    /// of the same atomic result as the shot's HP damage and its own stagger.
    /// </summary>
    void ResolveSuccess(GameObject creditedActor)
    {
        SetPhase(SpecialShootPointPhase.Resolving);
        StartCooldown();

        if (_meter != null)
        {
            float reward = profile.ResolveStaggerReward(_meter.MaxStagger);
            _meter.ApplySpecialPointReward(reward, creditedActor);

            // Pins the meter only when it actually filled. Below max the round simply produces the
            // Mini Stun, which is what "success always causes a Mini Stun" means.
            _meter.BeginPendingSpecialPointBreak(creditedActor);

            // One owner for the Behavior Tree and NavMesh suspension across the reaction and the
            // ChainReady that may follow it.
            _meter.BeginSpecialPointReactionHold();
        }

        StartSpecialReaction();
    }

    void StartSpecialReaction()
    {
        ResolveRefs();

        _reactionRequestId = _currentRequestId;
        _reactionPending = true;

        bool started = _animDriver != null &&
                       _animDriver.TryPlaySpecialReaction(_reactionRequestId, profile.missingClipFallbackSeconds);

        if (started)
            return;

        // Nothing could take the reaction — death, a cinematic, or an active chain owns locomotion.
        // The round still succeeded, so the meter has to be unwound rather than left pinned.
        _reactionPending = false;
        _reactionRequestId = 0;

        if (_meter != null)
            _meter.EndSpecialPointReactionHold();

        FinishRound(SpecialShootPointOutcome.Succeeded, startCooldown: false);
    }

    void OnSpecialReactionCompleted(int requestId)
    {
        if (!_reactionPending || requestId != _reactionRequestId)
            return;

        _reactionPending = false;
        _reactionRequestId = 0;

        // Ends the hold and, when the meter is pinned, enters ChainReady in this same call stack.
        _meter?.EndSpecialPointReactionHold();

        FinishRound(SpecialShootPointOutcome.Succeeded, startCooldown: false);
    }

    void OnSpecialReactionInterrupted(int requestId, CharacterAnimBrain.SpecialReactionInterruptReason reason)
    {
        if (!_reactionPending || requestId != _reactionRequestId)
            return;

        _reactionPending = false;
        _reactionRequestId = 0;

        // Whatever took the reaction — death, down, a cinematic — outranks ChainReady too, so the
        // pinned meter is dropped rather than handed off.
        _meter?.CancelPendingSpecialPointBreak();

        FinishRound(SpecialShootPointOutcome.Cancelled, startCooldown: false);
    }

    void CancelPendingReaction()
    {
        if (!_reactionPending)
            return;

        int requestId = _reactionRequestId;
        _reactionPending = false;
        _reactionRequestId = 0;

        _animDriver?.CancelSpecialReaction(requestId);
    }

    void FinishRound(SpecialShootPointOutcome outcome, bool startCooldown)
    {
        _lastOutcome = outcome;
        _lastOutcomeRequestId = _currentRequestId;
        int resolvedRequestId = _currentRequestId;

        _currentRequestId = 0;
        _pointsRemaining = 0;
        _warningActive = false;
        _phaseTimeRemaining = 0f;

        if (startCooldown)
            StartCooldown();

        SetPhase(_cooldownRemaining > 0f
            ? SpecialShootPointPhase.Cooldown
            : SpecialShootPointPhase.Idle);

        RoundResolved?.Invoke(outcome, resolvedRequestId);
        AnyRoundResolved?.Invoke(this, outcome);
    }

    void StartCooldown()
    {
        // Cooldown is measured from the moment the challenge resolves, not from the end of the Mini
        // Stun or the ChainReady that may follow it.
        if (profile != null)
            _cooldownRemaining = Mathf.Max(_cooldownRemaining, profile.cooldown);
    }

    void SetPhase(SpecialShootPointPhase phase)
    {
        if (_phase == phase)
            return;

        _phase = phase;
        PhaseChanged?.Invoke(phase);
    }

    // ----- Pool -----

    SpecialShootPointInstance RentPoint()
    {
        for (int i = _pool.Count - 1; i >= 0; i--)
        {
            SpecialShootPointInstance pooled = _pool[i];

            // A model rebuild can destroy a point that was still parented to a bone, so the pool is
            // pruned on rent rather than trusted to only ever grow.
            if (pooled == null)
            {
                _pool.RemoveAt(i);
                continue;
            }

            if (!pooled.gameObject.activeSelf)
                return pooled;
        }

        if (profile == null || profile.runtimePointPrefab == null)
            return null;

        SpecialShootPointInstance instance = Instantiate(profile.runtimePointPrefab, PoolRoot);
        instance.gameObject.SetActive(false);
        _pool.Add(instance);
        return instance;
    }

    void ReleaseAllPoints(bool silent)
    {
        for (int i = 0; i < _activePoints.Count; i++)
        {
            SpecialShootPointInstance point = _activePoints[i];
            if (point == null)
                continue;

            if (silent)
                point.CancelSilently();
            else
                point.ReturnToPool();
        }

        _activePoints.Clear();
    }

    // ----- Refs and subscriptions -----

    void ResolveRefs()
    {
        if (ctx == null)
        {
            TryGetComponent(out ctx);
            if (ctx == null)
                ctx = GetComponentInParent<EnemyContext>();
        }

        if (ctx == null)
            return;

        ctx.ResolveReferences();

        // Peer modules come from the context hub. Prefab hierarchies are not uniform, so a
        // directional lookup must never be what decides whether this feature works.
        _meter = ctx.StaggerMeter;
        _health = ctx.HealthSystem;
        _brain = ctx.AnimBrain;
        _animDriver = ctx.AnimDriver;
    }

    void Subscribe()
    {
        if (_subscribed)
            return;

        ResolveRefs();

        if (_meter != null)
            _meter.ChainReadyStarted += OnUnrelatedChainReadyStarted;

        if (_health != null)
        {
            _health.CharacterDead += OnOwnerInactive;
            _health.CharacterDown += OnOwnerInactive;
        }

        if (_brain != null)
        {
            _brain.SpecialReactionCompleted += OnSpecialReactionCompleted;
            _brain.SpecialReactionInterrupted += OnSpecialReactionInterrupted;
        }

        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed)
            return;

        if (_meter != null)
            _meter.ChainReadyStarted -= OnUnrelatedChainReadyStarted;

        if (_health != null)
        {
            _health.CharacterDead -= OnOwnerInactive;
            _health.CharacterDown -= OnOwnerInactive;
        }

        if (_brain != null)
        {
            _brain.SpecialReactionCompleted -= OnSpecialReactionCompleted;
            _brain.SpecialReactionInterrupted -= OnSpecialReactionInterrupted;
        }

        _subscribed = false;
    }

    /// <summary>
    /// An unrelated hit filled the meter before the player finished the round. The ordinary
    /// ChainReady flow wins and the incomplete challenge is dropped with no reward.
    /// </summary>
    void OnUnrelatedChainReadyStarted()
    {
        // The success path enters ChainReady deliberately, after the reaction. Only a ChainReady the
        // round did not ask for cancels it.
        if (_phase == SpecialShootPointPhase.Telegraph || _phase == SpecialShootPointPhase.Active)
            CancelRound();
    }

    void OnOwnerInactive()
    {
        CancelPendingReaction();
        ReleaseAllPoints(silent: true);

        if (_meter != null)
            _meter.CancelPendingSpecialPointBreak();

        if (IsRoundActive)
            FinishRound(SpecialShootPointOutcome.Cancelled, startCooldown: false);

        _cooldownRemaining = 0f;
    }

    bool ShouldCancelForOwnership()
    {
        if (_health != null && !_health.IsAlive)
            return true;

        if (_brain != null && (_brain.IsStageIntroPlaybackActive || _brain.IsChainPlaybackActive))
            return true;

        return false;
    }

    // ----- Anchors -----

    /// <summary>
    /// Re-resolves the model's anchor set when it is missing or was destroyed by a model rebuild.
    ///
    /// The reference is checked against Unity's lifetime operator rather than cached once, because
    /// a rebuilt model destroys the previous set along with the bones it pointed at.
    /// </summary>
    void ResolveAnchorSet()
    {
        if (anchorSet != null)
            return;

        // The model is the authoritative place to look. Falling back to a children search covers a
        // prefab whose model is not managed by CharacterVisualController.
        Transform searchRoot = ctx != null ? ctx.transform : transform;
        anchorSet = searchRoot.GetComponentInChildren<SpecialShootPointAnchorSet>(true);
    }

    /// <summary>Number of authored anchor slots, from whichever source is in use.</summary>
    int AnchorCount
    {
        get
        {
            ResolveAnchorSet();
            return anchorSet != null ? anchorSet.Count : anchors.Count;
        }
    }

    SpecialShootPointAnchor GetAnchor(int index)
    {
        ResolveAnchorSet();

        if (anchorSet != null)
            return anchorSet.GetAnchor(index);

        if (index < 0 || index >= anchors.Count)
            return null;

        return anchors[index];
    }

    bool IsAnchorUsable(int index)
    {
        SpecialShootPointAnchor anchor = GetAnchor(index);
        return anchor != null && anchor.IsUsable;
    }

    int CountUsableAnchors()
    {
        int count = AnchorCount;
        int usable = 0;
        for (int i = 0; i < count; i++)
        {
            if (IsAnchorUsable(i))
                usable++;
        }

        return usable;
    }

    float ResolveOwnerMaxHealth()
    {
        if (_health != null && _health.maximumHealth > 0f)
            return _health.maximumHealth;

        return ctx != null ? ctx.basemaxHealth : 0f;
    }

    static bool IsPlayerCredit(GameObject creditedActor)
    {
        if (creditedActor == null)
            return false;

        CharacteContext creditContext = creditedActor.GetComponent<CharacteContext>();
        if (creditContext == null)
            creditContext = creditedActor.GetComponentInParent<CharacteContext>();

        return creditContext != null && creditContext.TargetIdentity == AITargetIdentity.Player;
    }

    void LogAuthoringErrorOnce(string message)
    {
        if (_authoringWarningLogged)
            return;

        _authoringWarningLogged = true;
        Debug.LogWarning($"[{nameof(SpecialShootPointController)}] {message}", this);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Authoring-validation accessors. Public rather than internal because the validator lives in
    /// Assembly-CSharp-Editor, which cannot see this assembly's internals. Not part of the runtime
    /// contract — runtime code reads <see cref="Profile"/> and the anchors it already owns.
    /// </summary>
    public IReadOnlyList<SpecialShootPointAnchor> EditorAnchors
    {
        get
        {
            ResolveAnchorSet();
            return anchorSet != null ? anchorSet.Anchors : anchors;
        }
    }

    /// <summary>True when anchors come from a model-owned set rather than the local fallback list.</summary>
    public bool EditorUsesAnchorSet
    {
        get
        {
            ResolveAnchorSet();
            return anchorSet != null;
        }
    }

    public SpecialShootPointProfileSO EditorProfile => profile;
#endif
}
