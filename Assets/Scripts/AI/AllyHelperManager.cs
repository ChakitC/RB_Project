using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime;

[DefaultExecutionOrder(100)]
public class AllyHelperManager : MonoBehaviour
{
    const int MaxChainTargetColliders = 64;

    enum ChainAttackPhase
    {
        None = 0,
        WaitingForWarpCastMoment = 1,
        WaitingForWarpComplete = 2,
        WaitingForChainStart = 3,
        WaitingForChainCastMoment = 4,
        WaitingForChainComplete = 5,
    }

    sealed class PendingHelperSkill
    {
        public int requestId;
        public SkillGemDefinition skillDef;
        public SkillHelperDef helperProc;

        /// <summary>Target locked before the animation started. Never retargeted mid-cast.</summary>
        public SkillTargetHandle target = SkillTargetHandle.None;

        /// <summary>
        /// Non-null only for the targeted path, which routes through the helper's own
        /// <see cref="CharacterSkillManager"/> so the skill keeps a real charge pool.
        /// </summary>
        public SkillCastCostPolicy? costPolicy;
        public bool stampCooldown = true;
    }

    sealed class PendingChainAttackSequence
    {
        public int executionId;
        public HelperChainAttackSequenceDef sequenceDef;
        public SkillGemDefinition chainAttackSkillDef;
        public SkillHelperDef helperProc;
        public SkillCastCostPolicy? costPolicy;
        public bool stampCooldown = true;
        public GameObject targetObject;
        public Transform targetTransform;
        public Transform anchorTransform;
        public ChainStepContinueMode continueMode;
        public float continueNormalizedTime;
        public bool continueReleased;
        public int warpRequestId;
        public int chainAttackRequestId;
        public ChainAttackPhase phase;
    }

    [SerializeField] private PlayerContext playerContext;
    [SerializeField] private AllyContext allyContext;
    [SerializeField] private GameObject allyHelper;
    [SerializeField] private bool logHelperExecution;

    [Header("Summon")]
    [SerializeField] private float summonRadius = 2.5f;
    [SerializeField] private float minSummonRadius = 1.2f;
    [SerializeField] private float navMeshSampleDistance = 2f;
    [SerializeField] private bool facePlayerForward = true;

    [Header("Chain Attack Teleport")]
    [SerializeField] private Collider allyHelperTeleportProbeCollider;
    
    CharacterAnimBrain allyAnimBrain;
    CharacterAnimDriver allyAnimDriver;
    ISkillUser allySkillUser;
    ASPHelperDitherFader allyHelperFader;
    HealthSystem allyHealthSystem;
    AITargetInfo allyTargetInfo;
    NavMeshAgent allyAgent;
    CharacterController allyCharacterController;
    BehaviorTree allyBehaviorTree;
    CharacterAudioEmitter allyAudioEmitter;
    SkillCastOrchestrator helperSkillCastOrchestrator;
    Component helperSkillCastOwner;
    PendingHelperSkill pendingHelperSkill;

    /// <summary>
    /// Set for the duration of <see cref="TrySummonAllyHelper"/>. Activating the helper actor runs
    /// other systems' OnEnable and registration callbacks synchronously, and those can reach a
    /// trigger that summons again before this call has recorded its own pending skill - at which
    /// point the outer call overwrites the inner one's dispatch and then cancels everything.
    /// </summary>
    bool helperExecutionStartInProgress;
    PendingChainAttackSequence pendingChainAttackSequence;
    bool hideHelperOnSkillComplete;
    int nextHelperSkillRequestId = 1;
    int nextHelperExecutionId = 1;
    int lastCompletedChainAttackExecutionId;
    bool lastCompletedChainAttackExecutionSucceeded;
    readonly Collider[] _chainTargetBuffer = new Collider[MaxChainTargetColliders];
    readonly HashSet<int> _chainTargetIds = new();
    CharacterContextPartyLoader allyPartyLoader;
    bool _warnedInvalidHelperCharacter;
    bool _helperProtectionApplied;
    int _helperInvincibilityToken;
    int _helperUntargetableToken;
    bool _cinematicHold;
    CharacterSkillManager _helperProcLoadoutSubscribedSkillManager;

    public event Action<CharacterAnimBrain, CharacterAnimBrain> HelperAnimBrainChanged;

    /// <summary>
    /// Raised when the character loaded into the helper rig changes, so its manual command and
    /// helper procs now come from a different character asset.
    /// </summary>
    public event Action HelperLoadoutChanged;

    public bool IsHelperActive => allyHelper != null && allyHelper.activeSelf;
    public GameObject HelperObject => allyHelper;
    public CharacterAnimBrain HelperAnimBrain => allyAnimBrain;
    public CharacterSkillManager HelperSkillManager
    {
        get
        {
            CacheHelperReferences();
            if (allyContext == null)
                return null;

            if (allyContext.SkillManager == null && allyHelper != null)
                allyContext.SkillManager = allyHelper.GetComponent<CharacterSkillManager>();

            return allyContext.SkillManager;
        }
    }
    public bool LastExecutionSucceeded { get; private set; } = true;
    public int ActiveChainAttackExecutionId => pendingChainAttackSequence != null ? pendingChainAttackSequence.executionId : 0;
    public bool IsHelperBusy =>
        helperExecutionStartInProgress ||
        pendingHelperSkill != null ||
        pendingChainAttackSequence != null ||
        HasHelperRuntimeCast() ||
        (allyAnimBrain != null && allyAnimBrain.IsExclusiveLocomotionActive);

    bool HasHelperRuntimeCast()
    {
        CharacterSkillManager skillManager = HelperSkillManager;
        return skillManager != null && skillManager.TryGetActiveCast(out _);
    }

    public void BindHelper(AllyContext helperContext)
    {
        if (helperContext == null)
            throw new ArgumentNullException(nameof(helperContext));

        bool changed = allyContext != helperContext;

        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        allyContext = helperContext;
        allyHelper = helperContext.gameObject;
        _warnedInvalidHelperCharacter = false;
        CacheHelperReferences();
        HelperSkillManager?.RefreshCharacterOwnedLoadout();
        ValidateHelperCharacter();

        if (changed)
            HelperLoadoutChanged?.Invoke();
    }

    public bool IsChainAttackExecutionReadyToContinue(int executionId)
    {
        if (executionId <= 0)
            return false;

        if (pendingChainAttackSequence != null && pendingChainAttackSequence.executionId == executionId)
            return pendingChainAttackSequence.continueReleased;

        return lastCompletedChainAttackExecutionId == executionId;
    }

    public bool TryGetCompletedChainAttackExecutionResult(int executionId, out bool success)
    {
        if (executionId > 0 && lastCompletedChainAttackExecutionId == executionId)
        {
            success = lastCompletedChainAttackExecutionSucceeded;
            return true;
        }

        success = false;
        return false;
    }

    public bool HasChainAttackTarget(HelperChainAttackSequenceDef sequenceDef)
    {
        return TryResolveChainAttackTarget(
            sequenceDef,
            out _,
            out _,
            out _);
    }

    public bool CanStartManualCommand(bool requireNotBusy = true)
    {
        if (requireNotBusy && IsHelperBusy)
            return false;

        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        if (playerContext == null || allyHelper == null)
            return false;

        CacheHelperReferences();
        return allyAnimBrain != null && allyAnimDriver != null;
    }

    public bool HasConfiguredCommandSlot(int slotIndex = 0)
    {
        CharacterSkillManager skillManager = HelperSkillManager;
        return skillManager != null && skillManager.HasConfiguredPlayerCommandSkill;
    }

    bool TryBeginHelperExecutionStart()
    {
        // The helper actor can run registration callbacks synchronously when it is activated. A
        // single guard across every entry point makes the outer request the only owner of that
        // activation transaction and prevents a callback from cancelling its pending dispatch.
        if (helperExecutionStartInProgress ||
            pendingHelperSkill != null ||
            pendingChainAttackSequence != null ||
            HasHelperRuntimeCast())
        {
            return false;
        }

        helperExecutionStartInProgress = true;
        return true;
    }

    void EndHelperExecutionStart()
    {
        helperExecutionStartInProgress = false;
    }

    public bool TryExecuteCommandSlot(int slotIndex = 0, bool hideOnSkillComplete = true)
    {
        if (!TryBeginHelperExecutionStart())
            return false;

        try
        {
            return TryExecuteCommandSlotCore(slotIndex, hideOnSkillComplete);
        }
        finally
        {
            EndHelperExecutionStart();
        }
    }

    bool TryExecuteCommandSlotCore(int slotIndex, bool hideOnSkillComplete)
    {
        if (!TryPrepareHelperForSummon(out bool activatedNow))
            return false;

        CharacterSkillManager skillManager = HelperSkillManager;
        bool hasManualSkill = skillManager != null && skillManager.HasConfiguredPlayerCommandSkill;
        if (!hasManualSkill)
        {
            // Player-initiated, so this is the right moment to say why nothing happened.
            ValidateHelperCharacter();

            if (activatedNow)
                HideHelperImmediate();

            return false;
        }

        LastExecutionSucceeded = false;
        hideHelperOnSkillComplete = hideOnSkillComplete;
        CancelPendingHelperSkill();
        CompletePendingChainAttackSequence(false);

        ApplyTemporaryHelperSkillAutonomy();
        allyHelperFader?.BeginAnimationLifecycle(hideOnSkillComplete);

        SkillCastStartResult result = skillManager.TryStartPlayerCommandSkill();
        if (!result.Started)
        {
            RestoreHelperSkillAutonomy();
            hideHelperOnSkillComplete = false;

            if (activatedNow)
                HideHelperImmediate();

            return false;
        }

        if (result.Kind == SkillCastStartKind.ImmediateSuccess)
        {
            RestoreHelperSkillAutonomy();
            LastExecutionSucceeded = true;

            if (hideHelperOnSkillComplete)
            {
                hideHelperOnSkillComplete = false;

                if (allyHelperFader != null && allyHelper != null && allyHelper.activeSelf)
                    allyHelperFader.FinalizeAfterAnimation();
                else
                    AllyHelperOut();
            }
        }

        return true;
    }

    void Awake()
    {
        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        CacheHelperReferences();
    }

    void Start()
    {
        if (allyHelper == null)
        {
            Debug.LogWarning("AllyHelper is null", this);
            return;
        }

        CacheHelperReferences();

        // A cinematic (e.g. the MapRun stage intro) can start before this Start runs — MapRunController
        // has the default execution order while this manager is at 100 — so respect an active hold
        // instead of yanking the helper off screen mid-shot.
        if (_cinematicHold)
            return;

        allyHelperFader?.SetHiddenImmediate(preserveWhileDisabled: true);

        if (allyHelper.activeSelf)
            allyHelper.SetActive(false);
    }

    void OnDestroy()
    {
        RestoreHelperSkillAutonomy();
        RestoreHelperProtection();
        SubscribeToHelperProcLoadout(null);
        SubscribeToHelperFader(null);
        SubscribeToAnimBrain(null);
        SubscribeToHelperPartyLoader(null);
    }

    CharacterContextPartyLoader ResolveHelperPartyLoader()
    {
        if (allyHelper == null)
            return null;

        CharacterContextPartyLoader loader = allyContext != null ? allyContext.CharacterLoad : null;
        if (loader == null)
            loader = allyHelper.GetComponent<CharacterContextPartyLoader>();

        return loader;
    }

    void SubscribeToHelperPartyLoader(CharacterContextPartyLoader loader)
    {
        if (allyPartyLoader == loader)
            return;

        if (allyPartyLoader != null)
            allyPartyLoader.BaseStatsChanged -= OnHelperCharacterChanged;

        allyPartyLoader = loader;

        if (allyPartyLoader != null)
            allyPartyLoader.BaseStatsChanged += OnHelperCharacterChanged;
    }

    void SubscribeToHelperProcLoadout(CharacterSkillManager nextSkillManager)
    {
        if (_helperProcLoadoutSubscribedSkillManager == nextSkillManager)
            return;

        if (_helperProcLoadoutSubscribedSkillManager != null)
            _helperProcLoadoutSubscribedSkillManager.HelperProcLoadoutChanged -= OnHelperProcLoadoutChanged;

        _helperProcLoadoutSubscribedSkillManager = nextSkillManager;

        if (_helperProcLoadoutSubscribedSkillManager != null)
            _helperProcLoadoutSubscribedSkillManager.HelperProcLoadoutChanged += OnHelperProcLoadoutChanged;
    }

    void OnHelperProcLoadoutChanged()
    {
        // A proc variant can change while its wind-up is still owned by this manager. Drop both
        // the animation-side pending request and the runtime-side cast so the old snapshot can
        // never execute after the loadout switch.
        bool cancelledProcSkill = pendingHelperSkill != null && pendingHelperSkill.helperProc != null;
        bool cancelledProcChain = pendingChainAttackSequence != null && pendingChainAttackSequence.helperProc != null;

        if (cancelledProcSkill)
            CancelPendingHelperSkill();

        if (cancelledProcChain)
            CompletePendingChainAttackSequence(false);

        if (cancelledProcSkill || cancelledProcChain)
        {
            RestoreHelperSkillAutonomy();
            hideHelperOnSkillComplete = false;
        }
    }

    /// <summary>
    /// The helper rig is shared, so loading a different character into it replaces both the manual
    /// command and the helper procs. Anything already running belonged to the previous character
    /// and is dropped rather than finished with the new one's skill.
    /// </summary>
    void OnHelperCharacterChanged(CharacterStats previous, CharacterStats current)
    {
        _warnedInvalidHelperCharacter = false;

        CancelPendingHelperSkill();
        CompletePendingChainAttackSequence(false);
        RestoreHelperSkillAutonomy();
        RestoreHelperProtection();
        helperExecutionStartInProgress = false;
        hideHelperOnSkillComplete = false;

        HelperSkillManager?.RefreshCharacterOwnedLoadout();
        ValidateHelperCharacter();

        HelperLoadoutChanged?.Invoke();
    }

    /// <summary>
    /// A helper rig loaded with a character that is not authored as a Helper contributes nothing -
    /// no manual command, no procs. That is an authoring mistake worth one line in the console, but
    /// only one: the check runs again on every character swap. An empty skill slot on a valid
    /// Helper is silent, because "this helper has no manual command" is a real authoring choice.
    /// </summary>
    void ValidateHelperCharacter()
    {
        if (_warnedInvalidHelperCharacter)
            return;

        CharacterStats stats = allyContext != null ? allyContext.baseStats : null;
        if (stats == null)
        {
            _warnedInvalidHelperCharacter = true;
            Debug.LogWarning(
                "[AllyHelperManager] Helper rig has no character stats loaded; helper procs and the manual command are unavailable.",
                this);
            return;
        }

        if (!stats.IsHelperRole)
        {
            _warnedInvalidHelperCharacter = true;
            Debug.LogWarning(
                $"[AllyHelperManager] Character '{stats.name}' is loaded into the helper rig but is authored as {stats.partyRole}; helper procs and the manual command are unavailable.",
                this);
        }
    }

    void OnDisable()
    {
        helperExecutionStartInProgress = false;
        RestoreHelperSkillAutonomy();
        RestoreHelperProtection();
        CancelPendingHelperSkill();
        CompletePendingChainAttackSequence(false);
        hideHelperOnSkillComplete = false;
        LastExecutionSucceeded = false;
        SubscribeToHelperFader(null);
    }

    void Update()
    {
        TryRestoreProtectionIfHelperInactive();
        TryStartQueuedChainAttack();
        TryReleasePendingChainAttackContinueSignal();
    }

    public void SummonAllyHelper()
    {
        TrySummonAllyHelper(null);
    }

    /// <summary>
    /// Summons the helper next to <paramref name="targetContext"/> and casts an assist aimed at it.
    ///
    /// The target is locked before the helper is even placed, and never re-resolved afterwards: an
    /// assist that re-picked its recipient halfway through the animation would heal whoever happened
    /// to be worst off at the moment of impact rather than the one the player watched it fly toward.
    /// </summary>
    public bool TrySummonAllyHelperToTarget(
        SkillGemDefinition skillDef,
        CharacteContext targetContext,
        bool hideOnSkillComplete = true,
        SkillCastCostPolicy costPolicy = SkillCastCostPolicy.IgnoreEnergyRespectCharge)
    {
        if (skillDef == null || targetContext == null)
            return false;

        SkillTargetHandle target = SkillTargetHandle.For(targetContext);

        // Placement is preflight. Failing here must leave no trace: no cast, no reservation, and
        // above all no cooldown, because nothing was deployed.
        Vector3? preferredPosition = ResolvePositionNearTarget(targetContext);

        return TrySummonAllyHelper(skillDef, hideOnSkillComplete, target, costPolicy, preferredPosition);
    }

    public bool TrySummonAllyHelper(
        SkillGemDefinition skillDef,
        bool hideOnSkillComplete = true)
    {
        return TrySummonAllyHelper(skillDef, hideOnSkillComplete, SkillTargetHandle.None, null, null);
    }

    bool TrySummonAllyHelper(
        SkillGemDefinition skillDef,
        bool hideOnSkillComplete,
        SkillTargetHandle target,
        SkillCastCostPolicy? costPolicy,
        Vector3? preferredPosition)
    {
        // Re-entrant summons destroy each other. Activating the helper actor below runs other
        // systems synchronously - party registration above all - and those reach triggers that
        // summon again while this call has not yet recorded its pending skill. The inner call then
        // gets the earlier request id and starts the animation, and the outer call's
        // CancelPendingHelperSkill() wipes that dispatch before its own TryPlaySkill is refused
        // (the brain will not start a second skill over the first). Result: the helper is hidden
        // again, nothing was cast, and the trigger asks once more next frame - forever.
        if (!TryBeginHelperExecutionStart())
            return false;

        try
        {
            return TrySummonAllyHelperCore(
                skillDef,
                hideOnSkillComplete,
                target,
                costPolicy,
                preferredPosition,
                helperProc: null,
                stampCooldown: costPolicy.HasValue);
        }
        finally
        {
            EndHelperExecutionStart();
        }
    }

    /// <summary>Starts a selected Helper proc while preserving its proc-specific runtime entry.</summary>
    public bool TrySummonAllyHelperProc(
        SkillHelperDef helperProc,
        bool hideOnSkillComplete = true,
        SkillCastCostPolicy costPolicy = SkillCastCostPolicy.IgnoreEnergyAndCharge,
        bool stampCooldown = false)
    {
        if (helperProc == null || helperProc.executionSkill == null)
            return false;

        if (!TryBeginHelperExecutionStart())
            return false;

        try
        {
            return TrySummonAllyHelperCore(
                helperProc.executionSkill,
                hideOnSkillComplete,
                SkillTargetHandle.None,
                costPolicy,
                preferredPosition: null,
                helperProc,
                stampCooldown);
        }
        finally
        {
            EndHelperExecutionStart();
        }
    }

    /// <summary>Starts a targeted Helper proc with the selected recipient locked up front.</summary>
    public bool TrySummonAllyHelperProcToTarget(
        SkillHelperDef helperProc,
        CharacteContext targetContext,
        bool hideOnSkillComplete = true,
        SkillCastCostPolicy costPolicy = SkillCastCostPolicy.IgnoreEnergyRespectCharge,
        bool stampCooldown = true)
    {
        if (helperProc == null || helperProc.executionSkill == null || targetContext == null)
            return false;

        SkillTargetHandle target = SkillTargetHandle.For(targetContext);
        Vector3? preferredPosition = ResolvePositionNearTarget(targetContext);

        if (!TryBeginHelperExecutionStart())
            return false;

        try
        {
            return TrySummonAllyHelperCore(
                helperProc.executionSkill,
                hideOnSkillComplete,
                target,
                costPolicy,
                preferredPosition,
                helperProc,
                stampCooldown);
        }
        finally
        {
            EndHelperExecutionStart();
        }
    }

    bool TrySummonAllyHelperCore(
        SkillGemDefinition skillDef,
        bool hideOnSkillComplete,
        SkillTargetHandle target,
        SkillCastCostPolicy? costPolicy,
        Vector3? preferredPosition,
        SkillHelperDef helperProc,
        bool stampCooldown)
    {
        if (!TryPrepareHelperForSummon(out bool activatedNow, preferredPosition))
            return false;

        LastExecutionSucceeded = false;
        hideHelperOnSkillComplete = hideOnSkillComplete;
        CancelPendingHelperSkill();
        CompletePendingChainAttackSequence(false);

        if (skillDef == null)
        {
            allyAnimDriver.PlaySkill();

            if (!allyAnimBrain.IsSkillPlaybackActive)
            {
                hideHelperOnSkillComplete = false;
                if (activatedNow)
                    HideHelperImmediate();
                return false;
            }

            allyHelperFader?.BeginAnimationLifecycle(hideOnSkillComplete);
            return true;
        }

        int requestId = NextHelperSkillRequestId();
        pendingHelperSkill = new PendingHelperSkill
        {
            requestId = requestId,
            skillDef = skillDef,
            helperProc = helperProc,
            target = target ?? SkillTargetHandle.None,
            costPolicy = costPolicy,
            stampCooldown = stampCooldown,
        };

        if (SkillTargetHandle.IsAssigned(pendingHelperSkill.target) &&
            pendingHelperSkill.target.TryResolveLiveContext(out CharacteContext lockedTarget))
        {
            FaceHelperAt(lockedTarget.transform.position);
        }

        if (logHelperExecution)
            Debug.Log($"[AllyHelperManager] Starting helper skill '{skillDef.name}' with request {requestId}.", this);

        ApplyTemporaryHelperSkillAutonomy();
        bool started = allyAnimDriver.TryPlaySkill(
            requestId,
            skillDef,
            skillDef.GetCastPointNormalized());

        if (started)
        {
            allyHelperFader?.BeginAnimationLifecycle(hideOnSkillComplete);
            return true;
        }

        RestoreHelperSkillAutonomy();
        CancelPendingHelperSkill();
        hideHelperOnSkillComplete = false;

        if (activatedNow)
            HideHelperImmediate();

        return false;
    }

    public bool TryStartChainAttackHelper(
        HelperChainAttackSequenceDef sequenceDef,
        SkillGemDefinition chainAttackSkillDef,
        bool hideOnSkillComplete = true,
        ChainStepContinueMode continueMode = ChainStepContinueMode.OnStepComplete,
        float continueNormalizedTime = 1f)
    {
        if (sequenceDef == null || chainAttackSkillDef == null)
        {
            Log(sequenceDef, "Chain attack start failed: sequence config is incomplete.");
            return false;
        }

        if (!TryResolveChainAttackTarget(sequenceDef, out GameObject targetObject, out Transform targetTransform, out Transform anchorTransform))
        {
            Log(sequenceDef, "Chain attack start failed: no valid target near the player's aim target.");
            return false;
        }

        return TryStartChainAttackHelperInternal(
            sequenceDef,
            chainAttackSkillDef,
            targetObject,
            targetTransform,
            anchorTransform,
            hideOnSkillComplete,
            continueMode,
            continueNormalizedTime);
    }

    /// <summary>Starts a chain proc using the selected Helper proc runtime entry.</summary>
    public bool TryStartChainAttackHelperProc(
        SkillHelperDef helperProc,
        bool hideOnSkillComplete = true,
        ChainStepContinueMode continueMode = ChainStepContinueMode.OnStepComplete,
        float continueNormalizedTime = 1f,
        SkillCastCostPolicy costPolicy = SkillCastCostPolicy.IgnoreEnergyAndCharge,
        bool stampCooldown = false)
    {
        if (helperProc == null || helperProc.executionSkill == null || helperProc.chainAttackSequence == null)
        {
            Log(helperProc != null ? helperProc.chainAttackSequence : null,
                "Chain proc start failed: proc config is incomplete.");
            return false;
        }

        if (!TryResolveChainAttackTarget(
                helperProc.chainAttackSequence,
                out GameObject targetObject,
                out Transform targetTransform,
                out Transform anchorTransform))
        {
            Log(helperProc.chainAttackSequence, "Chain proc start failed: no valid target near the player's aim target.");
            return false;
        }

        return TryStartChainAttackHelperInternal(
            helperProc.chainAttackSequence,
            helperProc.executionSkill,
            targetObject,
            targetTransform,
            anchorTransform,
            hideOnSkillComplete,
            continueMode,
            continueNormalizedTime,
            helperProc,
            costPolicy,
            stampCooldown);
    }

    public bool TryStartChainAttackHelperToTarget(
        HelperChainAttackSequenceDef sequenceDef,
        SkillGemDefinition chainAttackSkillDef,
        Transform explicitTargetTransform,
        bool hideOnSkillComplete = true,
        ChainStepContinueMode continueMode = ChainStepContinueMode.OnStepComplete,
        float continueNormalizedTime = 1f)
    {
        if (!ChainAttackTargetingUtility.TryResolveExplicitTarget(
                explicitTargetTransform,
                playerContext,
                allyHelper,
                out GameObject targetObject,
                out Transform targetTransform,
                out Transform anchorTransform))
        {
            Log(sequenceDef, "Chain attack start failed: explicit target is invalid.");
            return false;
        }

        return TryStartChainAttackHelperInternal(
            sequenceDef,
            chainAttackSkillDef,
            targetObject,
            targetTransform,
            anchorTransform,
            hideOnSkillComplete,
            continueMode,
            continueNormalizedTime);
    }

    bool TryStartChainAttackHelperInternal(
        HelperChainAttackSequenceDef sequenceDef,
        SkillGemDefinition chainAttackSkillDef,
        GameObject targetObject,
        Transform targetTransform,
        Transform anchorTransform,
        bool hideOnSkillComplete,
        ChainStepContinueMode continueMode,
        float continueNormalizedTime,
        SkillHelperDef helperProc = null,
        SkillCastCostPolicy? costPolicy = null,
        bool stampCooldown = true)
    {
        if (sequenceDef == null || chainAttackSkillDef == null || targetObject == null || targetTransform == null || anchorTransform == null)
        {
            Log(sequenceDef, "Chain attack start failed: target data is incomplete.");
            return false;
        }

        if (!TryBeginHelperExecutionStart())
            return false;

        try
        {
            return TryStartChainAttackHelperInternalCore(
                sequenceDef,
                chainAttackSkillDef,
                targetObject,
                targetTransform,
                anchorTransform,
                hideOnSkillComplete,
                continueMode,
                continueNormalizedTime,
                helperProc,
                costPolicy,
                stampCooldown);
        }
        finally
        {
            EndHelperExecutionStart();
        }
    }

    bool TryStartChainAttackHelperInternalCore(
        HelperChainAttackSequenceDef sequenceDef,
        SkillGemDefinition chainAttackSkillDef,
        GameObject targetObject,
        Transform targetTransform,
        Transform anchorTransform,
        bool hideOnSkillComplete,
        ChainStepContinueMode continueMode,
        float continueNormalizedTime,
        SkillHelperDef helperProc,
        SkillCastCostPolicy? costPolicy,
        bool stampCooldown)
    {
        if (!TryPrepareHelperForSummon(out bool activatedNow))
            return false;

        LastExecutionSucceeded = false;
        hideHelperOnSkillComplete = hideOnSkillComplete;
        CancelPendingHelperSkill();
        CompletePendingChainAttackSequence(false);

        pendingChainAttackSequence = new PendingChainAttackSequence
        {
            executionId = NextHelperExecutionId(),
            sequenceDef = sequenceDef,
            chainAttackSkillDef = chainAttackSkillDef,
            helperProc = helperProc,
            costPolicy = costPolicy,
            stampCooldown = stampCooldown,
            targetObject = targetObject,
            targetTransform = targetTransform,
            anchorTransform = anchorTransform,
            continueMode = continueMode,
            continueNormalizedTime = Mathf.Clamp(continueNormalizedTime, 0f, 0.999f),
            warpRequestId = NextHelperSkillRequestId(),
            phase = ChainAttackPhase.WaitingForWarpCastMoment,
        };

        if (logHelperExecution || sequenceDef.debugLogging)
        {
            Debug.Log(
                $"[AllyHelperManager] Starting chain attack helper on target '{targetObject.name}' using utility warp-out.",
                this);
        }

        ApplyTemporaryHelperSkillAutonomy();

        bool started = allyAnimDriver.TryPlayUtilityWarpOut(
            pendingChainAttackSequence.warpRequestId);

        if (started)
        {
            allyHelperFader?.BeginAnimationLifecycle(sequenceDef.hideHelperAtWarpCastMoment);
            return true;
        }

        RestoreHelperSkillAutonomy();
        CompletePendingChainAttackSequence(false);
        hideHelperOnSkillComplete = false;

        if (activatedNow)
            HideHelperImmediate();

        return false;
    }

    public void AllyHelperOut()
    {
        RestoreHelperSkillAutonomy();
        CancelPendingHelperSkill();
        CompletePendingChainAttackSequence(false);
        hideHelperOnSkillComplete = false;

        if (allyHelper == null || !allyHelper.activeSelf)
        {
            RestoreHelperProtection();
            return;
        }

        if (allyHelperFader != null)
            allyHelperFader.FadeOutThenDeactivate();
        else
        {
            allyHelper.SetActive(false);
            RestoreHelperProtection();
        }
    }

    void CacheHelperReferences()
    {
        if (allyHelper == null)
            return;

        allyContext = allyHelper.GetComponent<AllyContext>();
        allyContext?.ResolveReferences();
        CharacterSkillManager helperSkillManager = allyContext != null
            ? allyContext.SkillManager
            : allyHelper.GetComponent<CharacterSkillManager>();
        if (helperSkillManager == null)
            helperSkillManager = allyHelper.GetComponent<CharacterSkillManager>();
        SubscribeToHelperProcLoadout(helperSkillManager);
        allyBehaviorTree = allyHelper.GetComponent<BehaviorTree>();

        SubscribeToHelperPartyLoader(ResolveHelperPartyLoader());

        if (allyContext != null && allyContext.AITargetSensor == null)
            allyContext.AITargetSensor = allyHelper.GetComponent<AITargetSensor>();

        allyAgent = allyContext != null ? allyContext.agent : null;
        if (allyAgent == null)
            allyAgent = allyHelper.GetComponent<NavMeshAgent>();

        if (allyContext != null && allyContext.agent == null)
            allyContext.agent = allyAgent;

        CharacterAnimBrain nextAnimBrain = allyContext != null ? allyContext.AnimBrain : null;
        if (nextAnimBrain == null)
            nextAnimBrain = allyHelper.GetComponent<CharacterAnimBrain>();

        if (allyContext != null && allyContext.AnimBrain == null)
            allyContext.AnimBrain = nextAnimBrain;

        SubscribeToAnimBrain(nextAnimBrain);

        allyAnimDriver = allyContext != null ? allyContext.AnimDriver : null;
        if (allyAnimDriver == null)
            allyAnimDriver = allyHelper.GetComponent<CharacterAnimDriver>();

        if (allyContext != null && allyContext.AnimDriver == null)
            allyContext.AnimDriver = allyAnimDriver;

        allySkillUser = allyHelper.GetComponent<ISkillUser>();
        if (allySkillUser == null && allyContext != null && allyContext.EnegySystem != null)
            allySkillUser = allyContext.EnegySystem;

        allyHealthSystem = allyContext != null ? allyContext.HealthSystem : null;
        if (allyHealthSystem == null)
            allyHealthSystem = allyHelper.GetComponent<HealthSystem>();

        if (allyContext != null && allyContext.HealthSystem == null)
            allyContext.HealthSystem = allyHealthSystem;

        allyCharacterController = allyContext != null ? allyContext.cc : null;
        if (allyCharacterController == null)
            allyCharacterController = allyHelper.GetComponent<CharacterController>();

        if (allyContext != null && allyContext.cc == null)
            allyContext.cc = allyCharacterController;

        Collider contextPositionCollider = ResolveCharacterPositionCollider(allyContext);
        if (contextPositionCollider != null)
            allyHelperTeleportProbeCollider = contextPositionCollider;

        allyTargetInfo = allyHelper.GetComponent<AITargetInfo>();
        if (allyTargetInfo == null)
            allyTargetInfo = allyHelper.GetComponentInChildren<AITargetInfo>(true);

        allyAudioEmitter = allyHelper.GetComponent<CharacterAudioEmitter>();
        if (allyAudioEmitter == null)
            allyAudioEmitter = allyHelper.GetComponentInChildren<CharacterAudioEmitter>(true);

        ASPHelperDitherFader nextHelperFader = allyHelper.GetComponent<ASPHelperDitherFader>();
        if (nextHelperFader == null)
            nextHelperFader = allyHelper.GetComponentInChildren<ASPHelperDitherFader>(true);

        SubscribeToHelperFader(nextHelperFader);
        EnsureHelperSkillCastOrchestrator();
    }

    Collider ResolveHelperTeleportProbeCollider()
    {
        if (allyHelper == null)
            return null;

        CharacteContext context = allyContext != null
            ? allyContext
            : allyHelper.GetComponent<CharacteContext>();

        Collider contextPositionCollider = ResolveCharacterPositionCollider(context);
        if (contextPositionCollider != null)
            return contextPositionCollider;

        if (allyHelperTeleportProbeCollider != null)
            return allyHelperTeleportProbeCollider;

        Collider rootCollider = allyHelper.GetComponent<Collider>();
        if (rootCollider != null)
            return rootCollider;

        if (allyContext != null)
        {
            Collider contextCollider = allyContext.GetComponent<Collider>();
            if (contextCollider != null)
                return contextCollider;
        }

        return null;
    }

    static Collider ResolveCharacterPositionCollider(CharacteContext context)
    {
        if (context == null)
            return null;

        if (context.ColliderRefs == null)
            context.ResolveReferences();

        return context.ColliderRefs != null
            ? context.ColliderRefs.CharacterPositionCollider
            : null;
    }

    void SubscribeToAnimBrain(CharacterAnimBrain nextAnimBrain)
    {
        if (allyAnimBrain == nextAnimBrain)
            return;

        CharacterAnimBrain prev = allyAnimBrain;

        if (allyAnimBrain != null)
        {
            allyAnimBrain.PlaybackEvent -= OnAllyPlaybackEvent;
        }

        allyAnimBrain = nextAnimBrain;

        if (allyAnimBrain != null)
        {
            allyAnimBrain.PlaybackEvent += OnAllyPlaybackEvent;
        }

        HelperAnimBrainChanged?.Invoke(prev, allyAnimBrain);
    }

    void SubscribeToHelperFader(ASPHelperDitherFader nextHelperFader)
    {
        if (allyHelperFader == nextHelperFader)
            return;

        if (allyHelperFader != null)
            allyHelperFader.Deactivated -= OnHelperFaderDeactivated;

        allyHelperFader = nextHelperFader;

        if (allyHelperFader != null)
            allyHelperFader.Deactivated += OnHelperFaderDeactivated;
    }

    bool TryPrepareHelperForSummon(out bool activatedNow, Vector3? preferredPosition = null)
    {
        activatedNow = false;

        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        if (playerContext == null || allyHelper == null)
        {
            Debug.LogWarning("Summon failed: playerContext or allyHelper is null", this);
            return false;
        }

        CacheHelperReferences();
        if (allyAnimBrain == null || allyAnimDriver == null)
        {
            Debug.LogWarning("Summon failed: animation Brain or Driver is null", this);
            return false;
        }

        Vector3 playerPos = playerContext.transform.position;

        // A targeted assist wants to arrive next to its recipient, but a spot that no longer fits
        // is not worth failing the whole cast over - the ring around the player is always valid,
        // and the delivery itself travels to the target regardless of where the helper stands.
        Vector3 finalSpawnPos = preferredPosition ?? ResolveSummonPosition(playerPos);

        allyHelper.transform.position = finalSpawnPos;
        
     
        ApplySummonRotation(finalSpawnPos, playerPos);

        if (!allyHelper.activeSelf)
        {
            allyHelper.SetActive(true);
            activatedNow = true;
            allyHelperFader?.SetHiddenImmediate();
        }

        ApplyHelperProtection();

        return true;
    }

    void HideHelperImmediate()
    {
        RestoreHelperSkillAutonomy();

        // While a cinematic holds the helper on screen, nothing else may hide it.
        if (_cinematicHold)
            return;

        allyHelperFader?.SetHiddenImmediate(preserveWhileDisabled: true);

        if (allyHelper != null && allyHelper.activeSelf)
            allyHelper.SetActive(false);

        RestoreHelperProtection();
    }

    /// <summary>
    /// Keeps the helper visible for a cinematic that wants the full party on screen. The helper is
    /// normally a summon: it is deactivated in <c>Start</c> and hidden again after each command, so a
    /// cinematic has to hold it explicitly. Positioning stays with the caller — this deliberately does
    /// not reuse the summon path, which randomises a spot around the player.
    /// Always pair with <see cref="EndCinematicAppearance"/>.
    /// </summary>
    public void BeginCinematicAppearance()
    {
        if (allyHelper == null)
            return;

        CacheHelperReferences();
        _cinematicHold = true;

        if (!allyHelper.activeSelf)
            allyHelper.SetActive(true);

        // false = fade in and stay; true would arm the auto-hide monitor meant for skill playback.
        allyHelperFader?.BeginAnimationLifecycle(false);
    }

    /// <summary>Releases the cinematic hold and returns the helper to its normal hidden state.</summary>
    public void EndCinematicAppearance()
    {
        if (!_cinematicHold)
            return;

        _cinematicHold = false;
        HideHelperImmediate();
    }

    Vector3 ResolveSummonPosition(Vector3 playerPos)
    {
        Vector2 random2D = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(minSummonRadius, summonRadius);
        Vector3 rawSpawnPos = playerPos + new Vector3(random2D.x, 0f, random2D.y);

        if (NavMesh.SamplePosition(rawSpawnPos, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            return hit.position;

        return rawSpawnPos;
    }

    void ApplySummonRotation(Vector3 spawnPos, Vector3 playerPos)
    {
        if (facePlayerForward)
        {
            allyHelper.transform.rotation = playerContext.transform.rotation;
            return;
        }

        Vector3 lookDir = playerPos - spawnPos;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude > 0.001f)
            allyHelper.transform.rotation = Quaternion.LookRotation(lookDir);
    }

    void ExecutePendingHelperSkill(int requestId)
    {
        if (pendingHelperSkill == null || pendingHelperSkill.requestId != requestId)
            return;

        PendingHelperSkill helperSkill = pendingHelperSkill;
        pendingHelperSkill = null;

        if (helperSkill.skillDef == null)
            return;

        bool executed = helperSkill.helperProc != null
            ? ExecuteHelperProcSkill(
                helperSkill.helperProc,
                applyFacing: true,
                requestId,
                helperSkill.target,
                helperSkill.costPolicy ?? SkillCastCostPolicy.Normal,
                helperSkill.stampCooldown)
            : ExecuteHelperSkill(
                helperSkill.skillDef,
                applyFacing: true,
                requestId,
                helperSkill.target,
                helperSkill.costPolicy,
                helperSkill.stampCooldown);

        if (!executed)
        {
            LastExecutionSucceeded = false;
        }
    }

    void CancelPendingHelperSkill()
    {
        if (pendingHelperSkill != null && pendingHelperSkill.requestId > 0 && allyAnimDriver != null)
            allyAnimDriver.CancelSkillCastRequest(pendingHelperSkill.requestId);

        pendingHelperSkill = null;
    }

    void CancelPendingChainAttackSequence()
    {
        pendingChainAttackSequence = null;
    }

    void CompletePendingChainAttackSequence(bool success)
    {
        if (pendingChainAttackSequence != null)
        {
            if (!success && allyAnimDriver != null)
            {
                if (pendingChainAttackSequence.warpRequestId > 0)
                    allyAnimDriver.CancelSkillCastRequest(pendingChainAttackSequence.warpRequestId);

                if (pendingChainAttackSequence.chainAttackRequestId > 0)
                    allyAnimDriver.CancelSkillCastRequest(pendingChainAttackSequence.chainAttackRequestId);
            }

            lastCompletedChainAttackExecutionId = pendingChainAttackSequence.executionId;
            lastCompletedChainAttackExecutionSucceeded = success;
        }

        pendingChainAttackSequence = null;
    }

    int NextHelperSkillRequestId()
    {
        if (nextHelperSkillRequestId == int.MaxValue)
            nextHelperSkillRequestId = 1;

        return nextHelperSkillRequestId++;
    }

    int NextHelperExecutionId()
    {
        if (nextHelperExecutionId == int.MaxValue)
            nextHelperExecutionId = 1;

        return nextHelperExecutionId++;
    }

    void ReleasePendingChainAttackContinueSignal(string reason)
    {
        if (pendingChainAttackSequence == null || pendingChainAttackSequence.continueReleased)
            return;

        if (pendingChainAttackSequence.continueMode == ChainStepContinueMode.OnStepComplete)
            return;

        pendingChainAttackSequence.continueReleased = true;
        Log(
            pendingChainAttackSequence.sequenceDef,
            $"Released helper chain continue signal for execution {pendingChainAttackSequence.executionId} at {reason}.");
    }

    void TryReleasePendingChainAttackContinueSignal()
    {
        if (pendingChainAttackSequence == null ||
            pendingChainAttackSequence.phase != ChainAttackPhase.WaitingForChainComplete ||
            pendingChainAttackSequence.continueMode != ChainStepContinueMode.OnAttackNormalizedTime ||
            allyAnimBrain == null)
        {
            return;
        }

        if (!allyAnimBrain.TryGetActiveSkillNormalizedTime(
                pendingChainAttackSequence.chainAttackRequestId,
                out float normalizedTime))
        {
            return;
        }

        if (normalizedTime < pendingChainAttackSequence.continueNormalizedTime)
            return;

        ReleasePendingChainAttackContinueSignal($"helper skill normalized time {normalizedTime:0.###}");
    }

    void OnAllyPlaybackEvent(CharacterAnimBrain.PlaybackSignal signal)
    {
        switch (signal.Kind)
        {
            case CharacterAnimBrain.PlaybackKind.Skill:
            case CharacterAnimBrain.PlaybackKind.UtilityWarpOut:
                break;
            default:
                return;
        }

        switch (signal.Phase)
        {
            case CharacterAnimBrain.PlaybackPhase.CastMoment:
                OnAllySkillCastMomentReached(signal.RequestId);
                break;

            case CharacterAnimBrain.PlaybackPhase.Interrupted:
                OnAllySkillCastInterrupted(signal.RequestId);
                break;

            case CharacterAnimBrain.PlaybackPhase.Completed:
                OnAllySkillCompleted();
                break;
        }
    }

    void OnAllySkillCastMomentReached(int requestId)
    {
        if (HandlePendingChainAttackCastMoment(requestId))
            return;

        ExecutePendingHelperSkill(requestId);
    }

    void OnAllySkillCastInterrupted(int requestId)
    {
        if (HandlePendingChainAttackInterrupted(requestId))
            return;

        if (pendingHelperSkill == null || pendingHelperSkill.requestId != requestId)
            return;

        if (logHelperExecution && pendingHelperSkill.skillDef != null)
        {
            Debug.Log(
                $"[AllyHelperManager] Helper skill '{pendingHelperSkill.skillDef.name}' was interrupted before release.",
                this);
        }

        RestoreHelperSkillAutonomy();
        CancelPendingHelperSkill();
        LastExecutionSucceeded = false;

        if (hideHelperOnSkillComplete)
            AllyHelperOut();
    }

    void OnAllySkillCompleted()
    {
        if (HandlePendingChainAttackCompleted())
            return;

        RestoreHelperSkillAutonomy();
        CancelPendingHelperSkill();
        LastExecutionSucceeded = true;

        if (!hideHelperOnSkillComplete)
            return;

        hideHelperOnSkillComplete = false;

        if (allyHelperFader != null && allyHelper != null && allyHelper.activeSelf)
            allyHelperFader.FinalizeAfterAnimation();
        else
            AllyHelperOut();
    }

    bool HandlePendingChainAttackCastMoment(int requestId)
    {
        if (pendingChainAttackSequence == null)
            return false;

        if (requestId == pendingChainAttackSequence.warpRequestId)
        {
            if (pendingChainAttackSequence.phase != ChainAttackPhase.WaitingForWarpCastMoment)
                return true;

            if (!IsChainAttackTargetAlive(pendingChainAttackSequence.targetTransform))
            {
                Log(pendingChainAttackSequence.sequenceDef, "Chain attack cancelled: target died before teleport.");
                CancelActiveChainAttackSequence(interrupted: false);
                return true;
            }

            Vector3 originalHelperPosition = allyHelper != null ? allyHelper.transform.position : Vector3.zero;
            Quaternion originalHelperRotation = allyHelper != null ? allyHelper.transform.rotation : Quaternion.identity;

            if (!TryResolveChainAttackTeleportPose(
                    pendingChainAttackSequence.sequenceDef,
                    pendingChainAttackSequence.anchorTransform,
                    out Vector3 teleportPosition,
                    out _,
                    (candidatePosition, candidateRotation) =>
                    {
                        TeleportHelperTo(candidatePosition, candidateRotation);
                        if (IsCurrentHelperChainTeleportProbeClear(pendingChainAttackSequence.sequenceDef))
                            return true;

                        TeleportHelperTo(originalHelperPosition, originalHelperRotation);
                        return false;
                    }))
            {
                Log(pendingChainAttackSequence.sequenceDef, "Chain attack cancelled: no safe teleport pose was found.");
                CancelActiveChainAttackSequence(interrupted: false);
                return true;
            }

            if (pendingChainAttackSequence.sequenceDef.hideHelperAtWarpCastMoment)
                allyHelperFader?.SetHiddenImmediate();

            pendingChainAttackSequence.phase = ChainAttackPhase.WaitingForWarpComplete;
            Log(pendingChainAttackSequence.sequenceDef, $"Teleported helper to chain attack pose at {teleportPosition}.");
            return true;
        }

        if (requestId == pendingChainAttackSequence.chainAttackRequestId)
        {
            if (pendingChainAttackSequence.phase != ChainAttackPhase.WaitingForChainCastMoment)
                return true;

            bool executed = pendingChainAttackSequence.helperProc != null
                ? ExecuteHelperProcSkill(
                    pendingChainAttackSequence.helperProc,
                    applyFacing: false,
                    requestId,
                    SkillTargetHandle.None,
                    pendingChainAttackSequence.costPolicy ?? SkillCastCostPolicy.Normal,
                    pendingChainAttackSequence.stampCooldown)
                : ExecuteHelperSkill(
                    pendingChainAttackSequence.chainAttackSkillDef,
                    applyFacing: false,
                    requestId,
                    target: null,
                    costPolicy: pendingChainAttackSequence.costPolicy,
                    stampCooldown: pendingChainAttackSequence.stampCooldown);

            if (!executed)
            {
                Log(pendingChainAttackSequence.sequenceDef, "Chain attack cancelled: follow-up skill payload failed.");
                CancelActiveChainAttackSequence(interrupted: false);
                return true;
            }

            pendingChainAttackSequence.phase = ChainAttackPhase.WaitingForChainComplete;

            if (pendingChainAttackSequence.continueMode == ChainStepContinueMode.OnAttackCastMoment)
                ReleasePendingChainAttackContinueSignal("helper skill cast moment");

            return true;
        }

        return false;
    }

    bool HandlePendingChainAttackInterrupted(int requestId)
    {
        if (pendingChainAttackSequence == null)
            return false;

        if (requestId != pendingChainAttackSequence.warpRequestId &&
            requestId != pendingChainAttackSequence.chainAttackRequestId)
        {
            return false;
        }

        Log(
            pendingChainAttackSequence.sequenceDef,
            $"Chain attack skill request {requestId} was interrupted during phase '{pendingChainAttackSequence.phase}'.");

        CancelActiveChainAttackSequence(interrupted: true);
        return true;
    }

    bool HandlePendingChainAttackCompleted()
    {
        if (pendingChainAttackSequence == null)
            return false;

        if (pendingChainAttackSequence.phase == ChainAttackPhase.WaitingForWarpComplete)
        {
            if (!IsChainAttackTargetAlive(pendingChainAttackSequence.targetTransform))
            {
                Log(pendingChainAttackSequence.sequenceDef, "Chain attack cancelled: target died before the follow-up attack started.");
                CancelActiveChainAttackSequence(interrupted: false);
                return true;
            }

            pendingChainAttackSequence.phase = ChainAttackPhase.WaitingForChainStart;
            Log(
                pendingChainAttackSequence.sequenceDef,
                $"Queued follow-up chain attack skill '{pendingChainAttackSequence.chainAttackSkillDef.name}'.");
            return true;
        }

        if (pendingChainAttackSequence.phase == ChainAttackPhase.WaitingForChainComplete)
        {
            if (!pendingChainAttackSequence.continueReleased &&
                pendingChainAttackSequence.continueMode != ChainStepContinueMode.OnStepComplete)
            {
                ReleasePendingChainAttackContinueSignal("helper skill completed");
            }

            RestoreHelperSkillAutonomy();
            CompletePendingChainAttackSequence(true);
            LastExecutionSucceeded = true;

            if (!hideHelperOnSkillComplete)
                return true;

            hideHelperOnSkillComplete = false;

            if (allyHelperFader != null && allyHelper != null && allyHelper.activeSelf)
                allyHelperFader.FinalizeAfterAnimation();
            else
                AllyHelperOut();

            return true;
        }

        return true;
    }

    void CancelActiveChainAttackSequence(bool interrupted)
    {
        RestoreHelperSkillAutonomy();
        CompletePendingChainAttackSequence(false);
        LastExecutionSucceeded = false;

        if (!hideHelperOnSkillComplete)
            return;

        hideHelperOnSkillComplete = false;

        if (interrupted)
            AllyHelperOut();
        else if (allyHelperFader != null && allyHelper != null && allyHelper.activeSelf)
            allyHelperFader.FinalizeAfterAnimation();
        else
            AllyHelperOut();
    }

    bool _helperAutonomyCaptured;
    bool _defaultHelperBehaviorTreeEnabled;
    bool _defaultHelperAgentEnabled;
    bool _defaultHelperAgentIsStopped;
    bool _defaultHelperAgentUpdatePosition;
    bool _defaultHelperAgentUpdateRotation;
    bool _defaultHelperAgentHadPath;
    Vector3 _defaultHelperAgentDestination;

    void ApplyTemporaryHelperSkillAutonomy()
    {
        if (_helperAutonomyCaptured || allyHelper == null)
            return;

        CacheHelperReferences();

        bool capturedAny = false;

        if (allyBehaviorTree != null)
        {
            _defaultHelperBehaviorTreeEnabled = allyBehaviorTree.enabled;
            allyBehaviorTree.enabled = false;
            capturedAny = true;
        }

        if (allyAgent != null && allyAgent.enabled)
        {
            _defaultHelperAgentEnabled = true;
            _defaultHelperAgentIsStopped = allyAgent.isStopped;
            _defaultHelperAgentUpdatePosition = allyAgent.updatePosition;
            _defaultHelperAgentUpdateRotation = allyAgent.updateRotation;
            _defaultHelperAgentHadPath = allyAgent.hasPath || allyAgent.pathPending;
            _defaultHelperAgentDestination = allyAgent.isOnNavMesh
                ? allyAgent.destination
                : allyHelper.transform.position;

            allyAgent.isStopped = true;
            allyAgent.updatePosition = false;
            allyAgent.updateRotation = false;

            if (allyAgent.isOnNavMesh)
                allyAgent.nextPosition = allyHelper.transform.position;

            allyAgent.enabled = false;
            capturedAny = true;
        }
        else
        {
            _defaultHelperAgentEnabled = false;
            _defaultHelperAgentHadPath = false;
            _defaultHelperAgentDestination = allyHelper != null ? allyHelper.transform.position : transform.position;
        }

        _helperAutonomyCaptured = capturedAny;
    }

    void RestoreHelperSkillAutonomy()
    {
        if (!_helperAutonomyCaptured)
            return;

        if (allyBehaviorTree != null)
            allyBehaviorTree.enabled = _defaultHelperBehaviorTreeEnabled;

        if (allyAgent != null && _defaultHelperAgentEnabled)
        {
            if (!allyAgent.enabled)
                allyAgent.enabled = true;

            SyncHelperAgentToTransform();

            allyAgent.updatePosition = _defaultHelperAgentUpdatePosition;
            allyAgent.updateRotation = _defaultHelperAgentUpdateRotation;
            allyAgent.isStopped = _defaultHelperAgentIsStopped;

            if (!_defaultHelperAgentIsStopped &&
                _defaultHelperAgentHadPath &&
                allyAgent.isOnNavMesh &&
                !allyAgent.pathPending &&
                !allyAgent.hasPath)
            {
                allyAgent.SetDestination(_defaultHelperAgentDestination);
            }
        }

        _defaultHelperAgentEnabled = false;
        _defaultHelperAgentHadPath = false;
        _defaultHelperAgentDestination = allyHelper != null ? allyHelper.transform.position : transform.position;
        _helperAutonomyCaptured = false;
    }

    void ApplyHelperSkillFacing(SkillGemDefinition skillDef)
    {
        if (skillDef == null ||
            skillDef.payload == null ||
            skillDef.payload.HelperFacingMode != SkillHelperFacingMode.FaceDetectedTargetOnCast ||
            allyHelper == null)
        {
            return;
        }

        if (!TryResolveHelperSkillAimPoint(out Vector3 aimPoint))
            return;

        Transform facingOrigin = allySkillUser != null && allySkillUser.CastOrigin != null
            ? allySkillUser.CastOrigin
            : allyHelper.transform;

        Vector3 lookDir = aimPoint - facingOrigin.position;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude <= 0.001f)
            return;

        allyHelper.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

        if (allyAgent != null && allyAgent.enabled && allyAgent.isOnNavMesh)
            allyAgent.nextPosition = allyHelper.transform.position;
    }

    bool ExecuteHelperSkill(
        SkillGemDefinition skillDef,
        bool applyFacing,
        int requestId = 0,
        SkillTargetHandle target = null,
        SkillCastCostPolicy? costPolicy = null,
        bool stampCooldown = true)
    {
        if (skillDef == null)
            return false;

        if (allySkillUser == null)
        {
            Debug.LogWarning($"Helper skill '{skillDef.name}' requires an ISkillUser on the helper actor.", this);
            return false;
        }

        // A cast that declared a cost policy is asking to be metered, so it has to go through the
        // helper's own CharacterSkillManager - that is the only path that binds the runtime skill
        // to a charge pool which survives the helper being hidden between summons. Legacy helper
        // procs deliberately keep the old private-orchestrator path: they have always been free and
        // uncapped, and quietly putting every existing proc on a cooldown is not this change's job.
        if (costPolicy.HasValue)
        {
            return ExecuteMeteredHelperSkill(
                skillDef, applyFacing, requestId, target, costPolicy.Value, stampCooldown);
        }

        EnsureHelperSkillCastOrchestrator();
        if (helperSkillCastOrchestrator == null)
        {
            Debug.LogWarning($"Helper skill '{skillDef.name}' could not resolve a skill cast orchestrator.", this);
            return false;
        }

        var runtimeSkill = new SkillInstance { def = skillDef };

        if (applyFacing)
            ApplyHelperSkillFacing(skillDef);

        if (logHelperExecution)
            Debug.Log($"[AllyHelperManager] Executing helper skill '{skillDef.name}'.", this);

        SkillCastStartResult result = helperSkillCastOrchestrator.TryStartCast(new SkillCastRequest(
            runtimeSkill,
            allySkillUser,
            animationDriver: allyAnimDriver,
            requestedId: requestId,
            ignoreResourceCosts: true,
            useAnimationDriver: false,
            debugSource: $"helper:{skillDef.name}"));

        if (result.Started)
        {
            TryPlayHelperSkillVoice(skillDef);
            return true;
        }

        Debug.LogWarning($"Helper skill '{skillDef.name}' could not execute. Check helper payload or legacy projectile setup.", this);
        return false;
    }

    bool ExecuteHelperProcSkill(
        SkillHelperDef helperProc,
        bool applyFacing,
        int requestId,
        SkillTargetHandle target,
        SkillCastCostPolicy costPolicy,
        bool stampCooldown)
    {
        if (helperProc == null || helperProc.executionSkill == null)
            return false;

        CharacterSkillManager skillManager = HelperSkillManager;
        if (skillManager == null)
        {
            Debug.LogWarning(
                $"Helper proc '{helperProc.RuntimeId}' needs a CharacterSkillManager on the helper actor.",
                this);
            return false;
        }

        if (applyFacing)
            ApplyHelperSkillFacing(helperProc.executionSkill);

        if (logHelperExecution)
        {
            Debug.Log(
                $"[AllyHelperManager] Executing helper proc '{helperProc.RuntimeId}' using '{helperProc.executionSkill.name}'.",
                this);
        }

        SkillCastStartResult result = skillManager.TryStartHelperProcSkill(
            helperProc,
            debugSource: $"helper-proc:{helperProc.RuntimeId}",
            requiredTimelineEvent: CombatTimelineEventName.None,
            usePlanarRootMotion: false,
            stampCooldown: stampCooldown,
            primaryTarget: target,
            costPolicy: costPolicy,
            externalAnimationRequestId: requestId);

        if (result.Started)
        {
            TryPlayHelperSkillVoice(helperProc.executionSkill);
            return true;
        }

        Debug.LogWarning(
            $"Helper proc '{helperProc.RuntimeId}' could not execute through the helper skill manager.",
            this);
        return false;
    }

    /// <summary>
    /// Runs a helper skill through the helper's own <see cref="CharacterSkillManager"/>, which
    /// binds it to a persistent per-definition charge pool. The helper GameObject is only ever
    /// deactivated between summons, never destroyed, and charges recharge on timestamps, so a
    /// cooldown started here keeps running while the helper is hidden.
    /// </summary>
    bool ExecuteMeteredHelperSkill(
        SkillGemDefinition skillDef,
        bool applyFacing,
        int requestId,
        SkillTargetHandle target,
        SkillCastCostPolicy costPolicy,
        bool stampCooldown)
    {
        CharacterSkillManager skillManager = HelperSkillManager;
        if (skillManager == null)
        {
            Debug.LogWarning(
                $"Helper skill '{skillDef.name}' needs a CharacterSkillManager on the helper actor to run metered.",
                this);
            return false;
        }

        if (applyFacing)
            ApplyHelperSkillFacing(skillDef);

        if (logHelperExecution)
            Debug.Log($"[AllyHelperManager] Executing metered helper skill '{skillDef.name}'.", this);

        // This runs at the cast moment of the animation this manager already started, so the cast
        // attaches to that request instead of asking for playback of its own. Same contract as the
        // legacy path above (requestedId + useAnimationDriver: false); without it the payload would
        // bind to a request id no clip ever raises, and every timeline marker would go unheard.
        SkillCastStartResult result = skillManager.TryStartExternalSkill(
            skillDef,
            debugSource: $"helper:{skillDef.name}",
            requiredTimelineEvent: CombatTimelineEventName.None,
            usePlanarRootMotion: false,
            stampCooldown: stampCooldown,
            primaryTarget: target,
            costPolicy: costPolicy,
            externalAnimationRequestId: requestId);

        if (result.Started)
        {
            TryPlayHelperSkillVoice(skillDef);
            return true;
        }

        Debug.LogWarning(
            $"Helper skill '{skillDef.name}' could not execute through the helper skill manager.",
            this);
        return false;
    }

    /// <summary>
    /// NavMesh spot beside <paramref name="targetContext"/>, or null when nothing around it is
    /// usable and the caller should fall back to the ring around the player.
    /// </summary>
    Vector3? ResolvePositionNearTarget(CharacteContext targetContext)
    {
        if (targetContext == null)
            return null;

        Vector3 targetPos = targetContext.transform.position;
        float radius = Mathf.Max(minSummonRadius, 0.1f);

        // Sweep several bearings rather than one random spot: a single sample that lands in a wall
        // would send the helper back to the player even though the target was perfectly reachable
        // from the other side.
        const int CandidateCount = 8;
        float bearingStep = 360f / CandidateCount;

        // Start behind the target relative to the player so the helper does not spawn between the
        // player and whoever they are watching.
        float startBearing = 0f;
        if (playerContext != null)
        {
            Vector3 fromPlayer = targetPos - playerContext.transform.position;
            fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude > 0.0001f)
                startBearing = Quaternion.LookRotation(fromPlayer.normalized, Vector3.up).eulerAngles.y;
        }

        for (int i = 0; i < CandidateCount; i++)
        {
            float bearing = startBearing + (i * bearingStep);
            Vector3 offset = Quaternion.Euler(0f, bearing, 0f) * (Vector3.forward * radius);
            Vector3 candidate = targetPos + offset;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                return hit.position;
        }

        return null;
    }

    /// <summary>Turns the helper to face a world point, keeping its NavMeshAgent in sync.</summary>
    void FaceHelperAt(Vector3 worldPoint)
    {
        if (allyHelper == null)
            return;

        Transform facingOrigin = allySkillUser != null && allySkillUser.CastOrigin != null
            ? allySkillUser.CastOrigin
            : allyHelper.transform;

        Vector3 lookDir = worldPoint - facingOrigin.position;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude <= 0.001f)
            return;

        allyHelper.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

        if (allyAgent != null && allyAgent.enabled && allyAgent.isOnNavMesh)
            allyAgent.nextPosition = allyHelper.transform.position;
    }

    void TryPlayHelperSkillVoice(SkillGemDefinition skillDef)
    {
        CharacterAudioEmitter audioEmitter = ResolveHelperAudioEmitter();
        audioEmitter?.TryPlaySkillVoice(skillDef);
    }

    CharacterAudioEmitter ResolveHelperAudioEmitter()
    {
        if (allyAudioEmitter != null)
            return allyAudioEmitter;

        if (allyHelper == null)
            return null;

        allyAudioEmitter = allyHelper.GetComponent<CharacterAudioEmitter>();
        if (allyAudioEmitter == null)
            allyAudioEmitter = allyHelper.GetComponentInChildren<CharacterAudioEmitter>(true);

        return allyAudioEmitter;
    }

    void EnsureHelperSkillCastOrchestrator()
    {
        Component nextOwner = allyAnimBrain != null
            ? allyAnimBrain
            : allyContext != null
                ? allyContext
                : allyHelper != null
                    ? allyHelper.transform
                    : null;

        if (nextOwner == null)
            return;

        if (helperSkillCastOrchestrator != null && ReferenceEquals(helperSkillCastOwner, nextOwner))
            return;

        helperSkillCastOrchestrator?.CancelPendingCast();
        helperSkillCastOwner = nextOwner;
        helperSkillCastOrchestrator = new SkillCastOrchestrator(nextOwner);
    }

    void TryStartQueuedChainAttack()
    {
        if (pendingChainAttackSequence == null ||
            pendingChainAttackSequence.phase != ChainAttackPhase.WaitingForChainStart)
        {
            return;
        }

        if (!IsChainAttackTargetAlive(pendingChainAttackSequence.targetTransform))
        {
            Log(pendingChainAttackSequence.sequenceDef, "Chain attack cancelled: target died before the follow-up attack could start.");
            CancelActiveChainAttackSequence(interrupted: false);
            return;
        }

        pendingChainAttackSequence.chainAttackRequestId = NextHelperSkillRequestId();
        pendingChainAttackSequence.phase = ChainAttackPhase.WaitingForChainCastMoment;

        bool started = allyAnimDriver.TryPlaySkill(
            pendingChainAttackSequence.chainAttackRequestId,
            pendingChainAttackSequence.chainAttackSkillDef,
            pendingChainAttackSequence.chainAttackSkillDef.GetCastPointNormalized());

        if (!started)
        {
            Log(pendingChainAttackSequence.sequenceDef, "Chain attack cancelled: follow-up attack clip could not start.");
            CancelActiveChainAttackSequence(interrupted: false);
            return;
        }

        allyHelperFader?.BeginAnimationLifecycle(hideHelperOnSkillComplete);
        Log(
            pendingChainAttackSequence.sequenceDef,
            $"Started follow-up chain attack skill '{pendingChainAttackSequence.chainAttackSkillDef.name}'.");
    }

    bool TryResolveHelperSkillAimPoint(out Vector3 aimPoint)
    {
        CacheHelperReferences();

        if (allyContext != null && allyContext.AITargetSensor != null)
        {
            allyContext.AITargetSensor.ForceScan();

            Transform currentTarget = allyContext.AITargetSensor.CurrentTarget;
            if (currentTarget != null)
            {
                aimPoint = currentTarget.position;
                return true;
            }

            if (allyContext.AITargetSensor.HasAnyTarget)
            {
                aimPoint = allyContext.AITargetSensor.LastSeenPosition;
                return true;
            }
        }

        aimPoint = Vector3.zero;
        return false;
    }

    bool TryResolveChainAttackTarget(
        HelperChainAttackSequenceDef sequenceDef,
        out GameObject targetObject,
        out Transform targetTransform,
        out Transform anchorTransform)
    {
        targetObject = null;
        targetTransform = null;
        anchorTransform = null;

        if (sequenceDef == null)
            return false;

        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        if (playerContext == null || playerContext.aimTarget == null)
            return false;

        Vector3 aimPoint = playerContext.aimTarget.position;
        int hitCount = Physics.OverlapSphereNonAlloc(
            aimPoint,
            Mathf.Max(0.1f, sequenceDef.aimSearchRadius),
            _chainTargetBuffer,
            sequenceDef.targetLayers,
            sequenceDef.targetTriggerInteraction);

        _chainTargetIds.Clear();

        float bestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _chainTargetBuffer[i];
            if (hit == null)
                continue;

            if (!TryResolveChainAttackCandidate(hit, out GameObject candidateObject, out Transform candidateTransform, out Transform candidateAnchor))
                continue;

            int targetId = candidateObject.GetInstanceID();
            if (!_chainTargetIds.Add(targetId))
                continue;

            Vector3 candidatePoint = candidateAnchor != null ? candidateAnchor.position : candidateTransform.position;
            if (sequenceDef.requireAimLineOfSight &&
                !HasAimLineOfSight(aimPoint, candidatePoint, sequenceDef))
            {
                continue;
            }

            float distSqr = (candidatePoint - aimPoint).sqrMagnitude;
            if (distSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distSqr;
            targetObject = candidateObject;
            targetTransform = candidateTransform;
            anchorTransform = candidateAnchor;
        }

        return targetObject != null && anchorTransform != null;
    }

    bool TryResolveChainAttackCandidate(
        Collider hit,
        out GameObject candidateObject,
        out Transform candidateTransform,
        out Transform candidateAnchor)
    {
        candidateObject = null;
        candidateTransform = null;
        candidateAnchor = null;

        if (hit == null)
            return false;

        CharacteContext targetContext = hit.GetComponentInParent<CharacteContext>();
        AITargetInfo targetInfo = hit.GetComponentInParent<AITargetInfo>();
        IAITargetable aiTargetable = FindInterfaceInParents<IAITargetable>(hit.transform);
        IDamageable damageable = FindInterfaceInParents<IDamageable>(hit.transform);

        bool hasCombatIdentity =
            targetContext != null ||
            targetInfo != null ||
            aiTargetable != null ||
            damageable != null;

        if (!hasCombatIdentity)
            return false;

        Transform rootTransform = targetContext != null
            ? targetContext.transform
            : hit.attachedRigidbody != null
                ? hit.attachedRigidbody.transform
                : hit.transform.root != null ? hit.transform.root : hit.transform;

        if (rootTransform == null)
            return false;

        if (playerContext != null && rootTransform == playerContext.transform.root)
            return false;

        if (allyHelper != null && rootTransform == allyHelper.transform.root)
            return false;

        if (!IsResolvedTargetAlive(rootTransform, targetContext, aiTargetable, damageable))
            return false;

        candidateTransform = rootTransform;
        candidateObject = rootTransform.gameObject;
        candidateAnchor = targetInfo != null && targetInfo.ChainAttackPoint != null
            ? targetInfo.ChainAttackPoint
            : aiTargetable?.AimPoint != null
                ? aiTargetable.AimPoint
                : rootTransform;

        return candidateAnchor != null;
    }

    bool IsResolvedTargetAlive(
        Transform rootTransform,
        CharacteContext targetContext,
        IAITargetable aiTargetable,
        IDamageable damageable)
    {
        if (targetContext != null && targetContext.stateHub != null)
            return targetContext.stateHub.IsAlive && !targetContext.stateHub.Isdown;

        if (aiTargetable != null)
            return aiTargetable.IsAlive;

        if (damageable != null)
            return damageable.IsAlive;

        return rootTransform != null;
    }

    bool IsChainAttackTargetAlive(Transform targetTransform)
    {
        if (targetTransform == null)
            return false;

        CharacteContext targetContext = targetTransform.GetComponentInParent<CharacteContext>();
        IAITargetable aiTargetable = FindInterfaceInParents<IAITargetable>(targetTransform);
        IDamageable damageable = FindInterfaceInParents<IDamageable>(targetTransform);

        return IsResolvedTargetAlive(targetTransform.root != null ? targetTransform.root : targetTransform, targetContext, aiTargetable, damageable);
    }

    bool HasAimLineOfSight(Vector3 origin, Vector3 targetPoint, HelperChainAttackSequenceDef sequenceDef)
    {
        if (sequenceDef == null || sequenceDef.aimObstacleLayers == 0)
            return true;

        Vector3 dir = targetPoint - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f)
            return true;

        return !Physics.Raycast(
            origin,
            dir / dist,
            dist,
            sequenceDef.aimObstacleLayers,
            sequenceDef.targetTriggerInteraction);
    }

    bool TryResolveChainAttackTeleportPose(
        HelperChainAttackSequenceDef sequenceDef,
        Transform anchorTransform,
        out Vector3 teleportPosition,
        out Quaternion teleportRotation,
        System.Func<Vector3, Quaternion, bool> poseValidator = null)
    {
        Quaternion fallbackBaseRotation =
            playerContext != null ? playerContext.transform.rotation :
            allyHelper != null ? allyHelper.transform.rotation :
            Quaternion.identity;

        Collider probeCollider = ResolveHelperTeleportProbeCollider();

        return ChainAttackTeleportUtility.TryResolveTeleportPose(
            sequenceDef,
            anchorTransform,
            fallbackBaseRotation,
            out teleportPosition,
            out teleportRotation,
            probeCollider,
            allyHelper != null ? allyHelper.transform : null,
            poseValidator);
    }

    void TeleportHelperTo(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (allyHelper == null)
            return;

        allyHelper.transform.SetPositionAndRotation(worldPosition, worldRotation);

        if (allyAgent == null || !allyAgent.enabled)
            return;

        if (allyAgent.isOnNavMesh)
        {
            allyAgent.nextPosition = allyHelper.transform.position;
            return;
        }

        if (NavMesh.SamplePosition(allyHelper.transform.position, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            allyAgent.Warp(navHit.position);
            allyHelper.transform.position = navHit.position;
            allyAgent.nextPosition = navHit.position;
        }
    }

    bool IsCurrentHelperChainTeleportProbeClear(HelperChainAttackSequenceDef sequenceDef)
    {
        if (sequenceDef == null || sequenceDef.obstacleLayers.value == 0)
            return true;

        return ChainAttackTeleportUtility.IsCurrentProbeColliderClear(
            ResolveHelperTeleportProbeCollider(),
            allyHelper != null ? allyHelper.transform : null,
            sequenceDef.obstacleLayers,
            sequenceDef.obstacleTriggerInteraction,
            sequenceDef.debugLogging,
            sequenceDef.name);
    }

    void SyncHelperAgentToTransform()
    {
        if (allyAgent == null || !allyAgent.enabled)
            return;

        Vector3 syncPosition = allyHelper != null ? allyHelper.transform.position : allyAgent.transform.position;

        if (allyAgent.isOnNavMesh)
        {
            allyAgent.Warp(syncPosition);
            allyAgent.nextPosition = syncPosition;
            return;
        }

        if (NavMesh.SamplePosition(syncPosition, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            syncPosition = navHit.position;

            if (allyHelper != null)
                allyHelper.transform.position = navHit.position;

            allyAgent.Warp(navHit.position);
            allyAgent.nextPosition = navHit.position;
        }
    }

    static T FindInterfaceInParents<T>(Transform start) where T : class
    {
        if (start == null)
            return null;

        MonoBehaviour[] behaviours = start.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is T match)
                return match;
        }

        return null;
    }

    void Log(HelperChainAttackSequenceDef sequenceDef, string message)
    {
        if (!logHelperExecution && (sequenceDef == null || !sequenceDef.debugLogging))
            return;

        Debug.Log($"[AllyHelperManager] {message}", this);
    }

    void OnHelperFaderDeactivated()
    {
        if (allyHelper != null && allyHelper.activeSelf)
            return;

        RestoreHelperProtection();
    }

    void TryRestoreProtectionIfHelperInactive()
    {
        if (_helperProtectionApplied && (allyHelper == null || !allyHelper.activeSelf))
            RestoreHelperProtection();
    }

    void ApplyHelperProtection()
    {
        if (_helperProtectionApplied || allyHelper == null)
            return;

        CacheHelperReferences();

        if (allyHealthSystem != null)
            _helperInvincibilityToken = allyHealthSystem.AcquireInvincibilityToken();

        if (allyTargetInfo != null)
            _helperUntargetableToken = allyTargetInfo.AcquireUntargetableToken();

        ApplyTemporaryNoCollision();
        _helperProtectionApplied = true;
    }

    void RestoreHelperProtection()
    {
        if (_helperUntargetableToken != 0 && allyTargetInfo != null)
            allyTargetInfo.ReleaseUntargetableToken(_helperUntargetableToken);

        if (_helperInvincibilityToken != 0 && allyHealthSystem != null)
            allyHealthSystem.ReleaseInvincibilityToken(_helperInvincibilityToken);

        _helperUntargetableToken = 0;
        _helperInvincibilityToken = 0;
        _helperProtectionApplied = false;

        RestoreCollisionMask();
    }

    private bool _excludeCaptured;
    private LayerMask _defaultExcludeLayers;
    private LayerMask _defaultCharacterControllerExcludeLayers;

    void ApplyTemporaryNoCollision()
    {
        if (allyContext == null || allyContext.rb == null)
            return;

        if (!_excludeCaptured)
        {
            _defaultExcludeLayers = allyContext.rb.excludeLayers;
            _defaultCharacterControllerExcludeLayers = allyCharacterController != null
                ? allyCharacterController.excludeLayers
                : allyContext.cc != null
                    ? allyContext.cc.excludeLayers
                    : 0;
            _excludeCaptured = true;
        }

        allyContext.rb.excludeLayers = Physics.AllLayers;

        if (allyCharacterController != null)
            allyCharacterController.excludeLayers = Physics.AllLayers;
        else if (allyContext.cc != null)
            allyContext.cc.excludeLayers = Physics.AllLayers;
    }

    void RestoreCollisionMask()
    {
        if (allyContext == null || allyContext.rb == null)
            return;

        if (!_excludeCaptured)
            return;

        allyContext.rb.excludeLayers = _defaultExcludeLayers;

        if (allyCharacterController != null)
            allyCharacterController.excludeLayers = _defaultCharacterControllerExcludeLayers;
        else if (allyContext.cc != null)
            allyContext.cc.excludeLayers = _defaultCharacterControllerExcludeLayers;

        _excludeCaptured = false;
    }
}
