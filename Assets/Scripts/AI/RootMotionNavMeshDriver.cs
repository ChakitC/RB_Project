using UnityEngine;
using UnityEngine.AI;

public class RootMotionNavMeshDriver : MonoBehaviour
{
    [SerializeField] private CharacterAnimBrain brain;
    [SerializeField] private bool zeroY = true;
    [SerializeField] private bool applyRootRotation = false;

    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

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
