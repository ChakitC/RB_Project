using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime;

[DefaultExecutionOrder(100)]
public class AllyHelperManager : MonoBehaviour
{
    sealed class PendingHelperSkill
    {
        public int requestId;
        public int skillLevel;
        public SkillGemDefinition skillDef;
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
    
    LayerMask layerMaskDefall;
    CharacterAnimBrain allyAnimBrain;
    ISkillUser allySkillUser;
    ASPHelperDitherFader allyHelperFader;
    NavMeshAgent allyAgent;
    BehaviorTree allyBehaviorTree;
    PendingHelperSkill pendingHelperSkill;
    bool hideHelperOnSkillComplete;
    int nextHelperSkillRequestId = 1;
    
    

    public bool IsHelperActive => allyHelper != null && allyHelper.activeSelf;
    public bool IsHelperBusy =>
        pendingHelperSkill != null ||
        (allyAnimBrain != null && allyAnimBrain.IsSkillActive);

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
        RestoreCollisionMask();
        SubscribeToAnimBrain(null);
    }

    void OnDisable()
    {
        RestoreHelperSkillAutonomy();
        RestoreCollisionMask();
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

        hideHelperOnSkillComplete = hideOnSkillComplete;
        CancelPendingHelperSkill();

        if (skillDef == null)
        {
            allyAnimBrain.PlaySkill();

            if (!allyAnimBrain.IsSkillActive)
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
        ApplyTemporaryNoCollision();
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
        RestoreCollisionMask();
        CancelPendingHelperSkill();
        hideHelperOnSkillComplete = false;

        if (activatedNow)
            HideHelperImmediate();

        return false;
    }

    public void AllyHelperOut()
    {
        RestoreHelperSkillAutonomy();
        RestoreCollisionMask();
        CancelPendingHelperSkill();
        hideHelperOnSkillComplete = false;

        if (allyHelper == null || !allyHelper.activeSelf)
            return;

        if (allyHelperFader != null)
            allyHelperFader.FadeOutThenDeactivate();
        else
            allyHelper.SetActive(false);
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

        allyHelperFader = allyHelper.GetComponent<ASPHelperDitherFader>();
        if (allyHelperFader == null)
            allyHelperFader = allyHelper.GetComponentInChildren<ASPHelperDitherFader>(true);
    }

    void SubscribeToAnimBrain(CharacterAnimBrain nextAnimBrain)
    {
        if (allyAnimBrain == nextAnimBrain)
            return;

        if (allyAnimBrain != null)
        {
            allyAnimBrain.SkillCastMomentReached -= OnAllySkillCastMomentReached;
            allyAnimBrain.SkillCastInterrupted -= OnAllySkillCastInterrupted;
            allyAnimBrain.SkillCompleted -= OnAllySkillCompleted;
        }

        allyAnimBrain = nextAnimBrain;

        if (allyAnimBrain != null)
        {
            allyAnimBrain.SkillCastMomentReached += OnAllySkillCastMomentReached;
            allyAnimBrain.SkillCastInterrupted += OnAllySkillCastInterrupted;
            allyAnimBrain.SkillCompleted += OnAllySkillCompleted;
        }
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

        return true;
    }

    void HideHelperImmediate()
    {
        RestoreHelperSkillAutonomy();
        RestoreCollisionMask();
        allyHelperFader?.SetHiddenImmediate();

        if (allyHelper != null && allyHelper.activeSelf)
            allyHelper.SetActive(false);
    }

    Vector3 ResolveSummonPosition(Vector3 playerPos)
    {
        Vector2 random2D = Random.insideUnitCircle.normalized * Random.Range(minSummonRadius, summonRadius);
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

        if (allySkillUser == null)
        {
            Debug.LogWarning($"Helper skill '{helperSkill.skillDef.name}' requires an ISkillUser on the helper actor.", this);
            return;
        }

        var runtimeSkill = new SkillInstance
        {
            def = helperSkill.skillDef,
            level = Mathf.Max(1, helperSkill.skillLevel),
        };

        if (!runtimeSkill.CanCast(allySkillUser, out _))
        {
            Debug.LogWarning($"Helper skill '{helperSkill.skillDef.name}' could not execute. Check helper energy and cast setup.", this);
            return;
        }

        ApplyHelperSkillFacing(helperSkill.skillDef);

        if (logHelperExecution)
            Debug.Log($"[AllyHelperManager] Executing helper skill '{helperSkill.skillDef.name}'.", this);

        runtimeSkill.Cast(allySkillUser);
    }

    void CancelPendingHelperSkill()
    {
        pendingHelperSkill = null;
    }

    int NextHelperSkillRequestId()
    {
        if (nextHelperSkillRequestId == int.MaxValue)
            nextHelperSkillRequestId = 1;

        return nextHelperSkillRequestId++;
    }

    void OnAllySkillCastMomentReached(int requestId)
    {
        ExecutePendingHelperSkill(requestId);
    }

    void OnAllySkillCastInterrupted(int requestId)
    {
        if (pendingHelperSkill == null || pendingHelperSkill.requestId != requestId)
            return;

        if (logHelperExecution && pendingHelperSkill.skillDef != null)
        {
            Debug.Log(
                $"[AllyHelperManager] Helper skill '{pendingHelperSkill.skillDef.name}' was interrupted before release.",
                this);
        }

        RestoreHelperSkillAutonomy();
        RestoreCollisionMask();
        CancelPendingHelperSkill();

        if (hideHelperOnSkillComplete)
            AllyHelperOut();
    }

    void OnAllySkillCompleted()
    {
        RestoreHelperSkillAutonomy();
        RestoreCollisionMask();
        CancelPendingHelperSkill();

        if (!hideHelperOnSkillComplete)
            return;

        hideHelperOnSkillComplete = false;

        if (allyHelperFader != null && allyHelper != null && allyHelper.activeSelf)
            allyHelperFader.FinalizeAfterAnimation();
        else
            AllyHelperOut();
    }

    bool _helperAutonomyCaptured;
    bool _defaultHelperBehaviorTreeEnabled;
    bool _defaultHelperAgentIsStopped;
    bool _defaultHelperAgentUpdatePosition;
    bool _defaultHelperAgentUpdateRotation;

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
            _defaultHelperAgentIsStopped = allyAgent.isStopped;
            _defaultHelperAgentUpdatePosition = allyAgent.updatePosition;
            _defaultHelperAgentUpdateRotation = allyAgent.updateRotation;

            allyAgent.isStopped = true;
            allyAgent.updatePosition = false;
            allyAgent.updateRotation = false;

            if (allyAgent.isOnNavMesh)
                allyAgent.nextPosition = allyHelper.transform.position;

            capturedAny = true;
        }

        _helperAutonomyCaptured = capturedAny;
    }

    void RestoreHelperSkillAutonomy()
    {
        if (!_helperAutonomyCaptured)
            return;

        if (allyBehaviorTree != null)
            allyBehaviorTree.enabled = _defaultHelperBehaviorTreeEnabled;

        if (allyAgent != null && allyAgent.enabled)
        {
            Vector3 helperPosition = allyHelper != null ? allyHelper.transform.position : allyAgent.transform.position;
            if (allyAgent.isOnNavMesh)
                allyAgent.nextPosition = helperPosition;

            allyAgent.updatePosition = _defaultHelperAgentUpdatePosition;
            allyAgent.updateRotation = _defaultHelperAgentUpdateRotation;
            allyAgent.isStopped = _defaultHelperAgentIsStopped;
        }

        _helperAutonomyCaptured = false;
    }

    void ApplyHelperSkillFacing(SkillGemDefinition skillDef)
    {
        if (skillDef == null ||
            skillDef.helperFacingMode != SkillGemDefinition.HelperFacingMode.FaceDetectedTargetOnCast ||
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

    private bool _excludeCaptured;
    private LayerMask _defaultExcludeLayers;

    void ApplyTemporaryNoCollision()
    {
        if (allyContext == null || allyContext.rb == null) return;

        if (!_excludeCaptured)
        {
            _defaultExcludeLayers = allyContext.rb.excludeLayers;
            _excludeCaptured = true;
        }

        allyContext.rb.excludeLayers = Physics.AllLayers;
    }

    void RestoreCollisionMask()
    {
        if (allyContext == null || allyContext.rb == null) return;
        if (!_excludeCaptured) return;

        allyContext.rb.excludeLayers = _defaultExcludeLayers;
        _excludeCaptured = false;
    }
}
