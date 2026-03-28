using UnityEngine;
using UnityEngine.AI;

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

    CharacterAnimBrain allyAnimBrain;
    ISkillUser allySkillUser;
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

        if (allyHelper.activeSelf)
            allyHelper.SetActive(false);
    }

    void OnDestroy()
    {
        SubscribeToAnimBrain(null);
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
                    allyHelper.SetActive(false);
                return false;
            }

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

        bool started = allyAnimBrain.TryPlaySkill(
            requestId,
            skillDef,
            skillDef.GetCastPointNormalized());

        if (started)
            return true;

        CancelPendingHelperSkill();
        hideHelperOnSkillComplete = false;

        if (activatedNow)
            allyHelper.SetActive(false);

        return false;
    }

    public void AllyHelperOut()
    {
        CancelPendingHelperSkill();
        hideHelperOnSkillComplete = false;

        if (allyHelper != null && allyHelper.activeSelf)
            allyHelper.SetActive(false);
    }

    void CacheHelperReferences()
    {
        if (allyHelper == null)
            return;

        allyContext = allyHelper.GetComponent<AllyContext>();

        CharacterAnimBrain nextAnimBrain = allyContext != null ? allyContext.AnimBrain : null;
        if (nextAnimBrain == null)
            nextAnimBrain = allyHelper.GetComponent<CharacterAnimBrain>();

        if (allyContext != null && allyContext.AnimBrain == null)
            allyContext.AnimBrain = nextAnimBrain;

        SubscribeToAnimBrain(nextAnimBrain);

        allySkillUser = allyHelper.GetComponent<ISkillUser>();
        if (allySkillUser == null && allyContext != null && allyContext.EnegySystem != null)
            allySkillUser = allyContext.EnegySystem;
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
        }

        return true;
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

        CancelPendingHelperSkill();

        if (hideHelperOnSkillComplete)
            AllyHelperOut();
    }

    void OnAllySkillCompleted()
    {
        CancelPendingHelperSkill();

        if (!hideHelperOnSkillComplete)
            return;

        AllyHelperOut();
    }
}
