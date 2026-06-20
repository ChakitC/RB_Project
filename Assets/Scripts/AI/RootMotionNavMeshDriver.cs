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

    void Awake()
    {
        if (!brain) brain = GetComponent<CharacterAnimBrain>();
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponent<Animator>();
        if (!ctx) ctx = GetComponent<CharacteContext>();
        if (!ctx) ctx = GetComponentInParent<CharacteContext>();

        if (animator)
            animator.applyRootMotion = false;

        _prevRM = brain && brain.RootMotionActive;
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
            agent.nextPosition = transform.position;
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
        agent.nextPosition = transform.position;
    }

    private void ExitRootMotion()
    {
        if (animator)
            animator.applyRootMotion = false;

        if (agent && agent.enabled)
            agent.nextPosition = transform.position;

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

        Vector3 delta = animator.deltaPosition;
        if (zeroY) delta.y = 0f;

        transform.position += delta;

        if (agent && agent.enabled)
            agent.nextPosition = transform.position;

        if (applyRootRotation)
            transform.rotation *= animator.deltaRotation;

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
            if (cc) cc.Move(-dir * dist);
        }
    }

    public void ResyncAgent(float warpIfDistanceGreaterThan = 0.5f)
    {
        if (!agent || !agent.enabled) return;
        if (!agent.isOnNavMesh) return;

        float d = Vector3.Distance(agent.nextPosition, transform.position);
        if (d > warpIfDistanceGreaterThan)
            agent.Warp(transform.position);
        else
            agent.nextPosition = transform.position;
    }
}
