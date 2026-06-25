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
        public int skillLevel;
        public SkillGemDefinition skillDef;
    }

    sealed class PendingChainAttackSequence
    {
        public int executionId;
        public HelperChainAttackSequenceDef sequenceDef;
        public SkillGemDefinition chainAttackSkillDef;
        public GameObject targetObject;
        public Transform targetTransform;
        public Transform anchorTransform;
        public int requestedSkillLevel;
        public int chainAttackSkillLevel;
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
    PendingChainAttackSequence pendingChainAttackSequence;
    bool hideHelperOnSkillComplete;
    int nextHelperSkillRequestId = 1;
    int nextHelperExecutionId = 1;
    int lastCompletedChainAttackExecutionId;
    bool lastCompletedChainAttackExecutionSucceeded;
    readonly Collider[] _chainTargetBuffer = new Collider[MaxChainTargetColliders];
    readonly HashSet<int> _chainTargetIds = new();
    bool _helperProtectionApplied;
    int _helperInvincibilityToken;
    int _helperUntargetableToken;

    public event Action<CharacterAnimBrain, CharacterAnimBrain> HelperAnimBrainChanged;

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
        pendingHelperSkill != null ||
        pendingChainAttackSequence != null ||
        (allyAnimBrain != null && allyAnimBrain.IsExclusiveLocomotionActive);

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
        return allyAnimBrain != null;
    }

    public bool HasConfiguredCommandSlot(int slotIndex = 0)
    {
        CharacterSkillManager skillManager = HelperSkillManager;
        return skillManager != null &&
               (skillManager.HasConfiguredPlayerCommandSkill || skillManager.HasConfiguredCommandSlot(slotIndex));
    }

    public bool TryExecuteCommandSlot(int slotIndex = 0, bool hideOnSkillComplete = true)
    {
        if (!TryPrepareHelperForSummon(out bool activatedNow))
            return false;

        CharacterSkillManager skillManager = HelperSkillManager;
        bool hasManualSkill = skillManager != null &&
                              (skillManager.HasConfiguredPlayerCommandSkill ||
                               skillManager.HasConfiguredCommandSlot(slotIndex));
        if (!hasManualSkill)
        {
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

        SkillCastStartResult result = skillManager.HasConfiguredPlayerCommandSkill
            ? skillManager.TryStartPlayerCommandSkill()
            : skillManager.TryStartCastSlot(slotIndex);
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
        allyHelperFader?.SetHiddenImmediate();

        if (allyHelper.activeSelf)
            allyHelper.SetActive(false);
    }

    void OnDestroy()
    {
        RestoreHelperSkillAutonomy();
        RestoreHelperProtection();
        SubscribeToHelperFader(null);
        SubscribeToAnimBrain(null);
    }

    void OnDisable()
    {
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

    public bool TrySummonAllyHelper(
        SkillGemDefinition skillDef,
        int skillLevel = 1,
        bool hideOnSkillComplete = true)
    {
        if (!TryPrepareHelperForSummon(out bool activatedNow))
            return false;

        LastExecutionSucceeded = false;
        hideHelperOnSkillComplete = hideOnSkillComplete;
        CancelPendingHelperSkill();
        CompletePendingChainAttackSequence(false);

        if (skillDef == null)
        {
            allyAnimBrain.PlaySkill();

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
            skillLevel = Mathf.Max(1, skillLevel),
        };

        if (logHelperExecution)
            Debug.Log($"[AllyHelperManager] Starting helper skill '{skillDef.name}' with request {requestId}.", this);

        ApplyTemporaryHelperSkillAutonomy();
        bool started = allyAnimBrain.TryPlaySkill(
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
        int requestedSkillLevel = 1,
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
            requestedSkillLevel,
            hideOnSkillComplete,
            continueMode,
            continueNormalizedTime);
    }

    public bool TryStartChainAttackHelperToTarget(
        HelperChainAttackSequenceDef sequenceDef,
        SkillGemDefinition chainAttackSkillDef,
        Transform explicitTargetTransform,
        int requestedSkillLevel = 1,
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
            requestedSkillLevel,
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
        int requestedSkillLevel,
        bool hideOnSkillComplete,
        ChainStepContinueMode continueMode,
        float continueNormalizedTime)
    {
        if (sequenceDef == null || chainAttackSkillDef == null || targetObject == null || targetTransform == null || anchorTransform == null)
        {
            Log(sequenceDef, "Chain attack start failed: target data is incomplete.");
            return false;
        }

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
            targetObject = targetObject,
            targetTransform = targetTransform,
            anchorTransform = anchorTransform,
            requestedSkillLevel = Mathf.Max(1, requestedSkillLevel),
            chainAttackSkillLevel = Mathf.Max(1, requestedSkillLevel),
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

        bool started = allyAnimBrain.TryPlayUtilityWarpOut(
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
        allyBehaviorTree = allyHelper.GetComponent<BehaviorTree>();

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

    bool TryPrepareHelperForSummon(out bool activatedNow)
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
        if (allyAnimBrain == null)
        {
            Debug.LogWarning("Summon failed: CharacterAnimBrain is null", this);
            return false;
        }

        Vector3 playerPos = playerContext.transform.position;
        Vector3 finalSpawnPos = ResolveSummonPosition(playerPos);

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
        allyHelperFader?.SetHiddenImmediate();

        if (allyHelper != null && allyHelper.activeSelf)
            allyHelper.SetActive(false);

        RestoreHelperProtection();
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

        if (!ExecuteHelperSkill(helperSkill.skillDef, helperSkill.skillLevel, applyFacing: true, requestId))
            LastExecutionSucceeded = false;
    }

    void CancelPendingHelperSkill()
    {
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

            if (!ExecuteHelperSkill(
                    pendingChainAttackSequence.chainAttackSkillDef,
                    pendingChainAttackSequence.chainAttackSkillLevel,
                    applyFacing: false,
                    requestId))
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

    bool ExecuteHelperSkill(SkillGemDefinition skillDef, int skillLevel, bool applyFacing, int requestId = 0)
    {
        if (skillDef == null)
            return false;

        if (allySkillUser == null)
        {
            Debug.LogWarning($"Helper skill '{skillDef.name}' requires an ISkillUser on the helper actor.", this);
            return false;
        }

        EnsureHelperSkillCastOrchestrator();
        if (helperSkillCastOrchestrator == null)
        {
            Debug.LogWarning($"Helper skill '{skillDef.name}' could not resolve a skill cast orchestrator.", this);
            return false;
        }

        var runtimeSkill = new SkillInstance
        {
            def = skillDef,
            level = Mathf.Max(1, skillLevel),
        };

        if (applyFacing)
            ApplyHelperSkillFacing(skillDef);

        if (logHelperExecution)
            Debug.Log($"[AllyHelperManager] Executing helper skill '{skillDef.name}'.", this);

        SkillCastStartResult result = helperSkillCastOrchestrator.TryStartCast(new SkillCastRequest(
            runtimeSkill,
            allySkillUser,
            animationDriver: allyAnimBrain,
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

        bool started = allyAnimBrain.TryPlaySkill(
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
