using UnityEngine;
using UnityEngine.AI;

public class RootMotionNavMeshDriver : MonoBehaviour
{
    [SerializeField] private CharacterAnimBrain brain;
    [SerializeField] private bool zeroY = false;
    [SerializeField] private bool applyRootRotation = false;

    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("Character Push")]
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private LayerMask pushLayers;

    private bool _prevRM;
    private bool _cachedAgentIsStopped;
    private bool _cachedAgentUpdatePosition;
    private bool _cachedAgentUpdateRotation;
    private bool _hasCachedAgentState;
    private Transform _actorRoot;

    Transform ActorRoot => _actorRoot != null
        ? _actorRoot
        : ctx != null
            ? ctx.transform
            : transform;

    void Awake()
    {
        if (!brain) brain = GetComponent<CharacterAnimBrain>();
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponent<Animator>();
        if (!ctx) ctx = GetComponent<CharacteContext>();
        if (!ctx) ctx = GetComponentInParent<CharacteContext>();
        _actorRoot = ctx != null ? ctx.transform : transform;

        if (animator)
            animator.applyRootMotion = false;

        _prevRM = brain && brain.RootMotionActive;
    }

    public void Configure(
        CharacterAnimBrain animBrain,
        NavMeshAgent navMeshAgent,
        Animator sourceAnimator,
        CharacteContext context,
        Transform actorRoot,
        RootMotionNavMeshDriver settingsSource = null)
    {
        if (settingsSource != null && settingsSource != this)
        {
            zeroY = settingsSource.zeroY;
            applyRootRotation = settingsSource.applyRootRotation;
            pushLayers = settingsSource.pushLayers;
        }

        brain = animBrain;
        agent = navMeshAgent;
        animator = sourceAnimator;
        ctx = context;
        _actorRoot = actorRoot != null
            ? actorRoot
            : context != null
                ? context.transform
                : transform;

        if (animator)
            animator.applyRootMotion = false;
    }

    void Update()
    {
        bool rm = brain && brain.RootMotionActive;

        if (rm != _prevRM)
        {
            if (rm) EnterRootMotion();
            else ExitRootMotion();
            _prevRM = rm;
        }

        if (rm && agent && agent.enabled)
            agent.nextPosition = ActorRoot.position;
    }

    private void EnterRootMotion()
    {
        if (animator)
            animator.applyRootMotion = true;

        if (!agent || !agent.enabled)
        {
            _hasCachedAgentState = false;
            return;
        }

        _cachedAgentIsStopped = agent.isStopped;
        _cachedAgentUpdatePosition = agent.updatePosition;
        _cachedAgentUpdateRotation = agent.updateRotation;
        _hasCachedAgentState = true;

        agent.isStopped = true;
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.nextPosition = ActorRoot.position;
    }

    private void ExitRootMotion()
    {
        if (animator)
            animator.applyRootMotion = false;

        if (agent && agent.enabled)
            agent.nextPosition = ActorRoot.position;

        if (_hasCachedAgentState && agent && agent.enabled)
        {
            agent.updatePosition = _cachedAgentUpdatePosition;
            agent.updateRotation = _cachedAgentUpdateRotation;
            agent.isStopped = _cachedAgentIsStopped;
        }

        _hasCachedAgentState = false;
    }

    void OnAnimatorMove()
    {
        if (!brain || !brain.RootMotionActive) return;
        if (!animator) return;

        Vector3 delta = RootMotionDeltaUtility.GetPositionDelta(
            animator,
            zeroY || brain.RootMotionPlanarOnly);

        Transform actorRoot = ActorRoot;
        actorRoot.position += delta;

        if (agent && agent.enabled)
            agent.nextPosition = actorRoot.position;

        if (applyRootRotation || brain.RootMotionYawActive)
        {
            float yawDelta = RootMotionDeltaUtility.GetYawDelta(animator);
            actorRoot.rotation *= Quaternion.AngleAxis(yawDelta, Vector3.up);
        }

        if (pushLayers != 0)
            PushOverlappingCharacters();
    }

    private void PushOverlappingCharacters()
    {
        if (ctx == null || ctx.ColliderRefs == null) return;
        Collider aiCol = ctx.ColliderRefs.CharacterPositionCollider;
        if (aiCol == null) return;

        Bounds b = aiCol.bounds;
        Collider[] hits = Physics.OverlapSphere(b.center, b.extents.magnitude, pushLayers, QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (hit == aiCol) continue;

            if (!Physics.ComputePenetration(
                aiCol, aiCol.transform.position, aiCol.transform.rotation,
                hit, hit.transform.position, hit.transform.rotation,
                out Vector3 dir, out float dist))
                continue;

            var cc = hit.GetComponentInParent<CharacterController>();
            if (!cc) continue;

            Vector3 planarDirection = ResolvePlanarPushDirection(aiCol, hit, -dir, cc.transform);
            if (planarDirection.sqrMagnitude > 0.0001f)
                cc.Move(planarDirection * dist);
        }
    }

    static Vector3 ResolvePlanarPushDirection(
        Collider source,
        Collider hit,
        Vector3 penetrationDirection,
        Transform fallbackRoot)
    {
        Vector3 planarDirection = Vector3.ProjectOnPlane(penetrationDirection, Vector3.up);
        if (planarDirection.sqrMagnitude <= 0.0001f)
        {
            planarDirection = hit.bounds.center - source.bounds.center;
            planarDirection.y = 0f;
        }

        if (planarDirection.sqrMagnitude <= 0.0001f && fallbackRoot != null)
            planarDirection = Vector3.ProjectOnPlane(fallbackRoot.forward, Vector3.up);

        return planarDirection.sqrMagnitude > 0.0001f
            ? planarDirection.normalized
            : Vector3.zero;
    }

    public void ResyncAgent(float warpIfDistanceGreaterThan = 0.5f)
    {
        if (!agent || !agent.enabled) return;
        if (!agent.isOnNavMesh) return;

        float d = Vector3.Distance(agent.nextPosition, ActorRoot.position);
        if (d > warpIfDistanceGreaterThan)
            agent.Warp(ActorRoot.position);
        else
            agent.nextPosition = ActorRoot.position;
    }
}
