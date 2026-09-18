using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;

public class AiFaceTarget : Action
{
    [Header("Target")]
    public SharedVariable<GameObject> target;
    [Tooltip("Optional model/pivot to rotate. Defaults to the task transform.")]
    public SharedVariable<GameObject> rotateRoot;

    [Header("Rotate Settings")]
    [Min(0f)] public float rotateSpeed = 720f;
    [Tooltip("Return Success when the remaining angle is within this many degrees.")]
    [Range(0f, 180f)] public float angleTolerance = 3f;
    public bool ignoreYAxis = true;

    [Header("NavMeshAgent")]
    public bool stopAgentWhileAiming = true;
    public bool disableAgentAutoRotation = true;

    private CharacteContext _ctx;
    private NavMeshAgent _agent;
    private Transform _rotateTransform;
    private bool _cachedUpdateRotation;
    private bool _cachedIsStopped;
    private bool _changedUpdateRotation;
    private bool _changedIsStopped;

    public override void OnStart()
    {
        _ctx = CharacterContextModuleLookup.ResolveContext(gameObject);
        if (_ctx != null && _ctx.stateHub == null)
            _ctx.ResolveReferences();

        _rotateTransform = rotateRoot != null && rotateRoot.Value != null
            ? rotateRoot.Value.transform : transform;

        _agent = null;
        if (_ctx is AllyContext ally)
            _agent = ally.agent;
        else if (_ctx is EnemyContext enemy)
            _agent = enemy.Agent;
        else if (_ctx is SummonContext summon)
            _agent = summon.Agent;

        if (_agent == null)
        {
            GameObject owner = _ctx != null ? _ctx.gameObject : gameObject;
            _agent = owner.GetComponent<NavMeshAgent>();
            if (_agent == null)
                _agent = owner.GetComponentInParent<NavMeshAgent>();
            if (_agent == null)
                _agent = owner.GetComponentInChildren<NavMeshAgent>(true);
        }

        _changedUpdateRotation = false;
        _changedIsStopped = false;
        if (_agent == null)
            return;

        if (disableAgentAutoRotation)
        {
            _cachedUpdateRotation = _agent.updateRotation;
            _agent.updateRotation = false;
            _changedUpdateRotation = true;
        }

        if (stopAgentWhileAiming && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
        {
            _cachedIsStopped = _agent.isStopped;
            _agent.isStopped = true;
            _changedIsStopped = true;
        }
    }

    public override TaskStatus OnUpdate()
    {
        if (target == null || target.Value == null || _rotateTransform == null)
            return TaskStatus.Failure;

        if (_ctx == null || _ctx.stateHub == null || !_ctx.stateHub.CanRotate())
            return TaskStatus.Failure;

        Vector3 direction = target.Value.transform.position - _rotateTransform.position;
        if (ignoreYAxis)
            direction.y = 0f;

        // Coincident positions have no facing direction to correct.
        if (direction.sqrMagnitude <= 0.0001f)
            return TaskStatus.Success;

        Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        float tolerance = Mathf.Clamp(angleTolerance, 0f, 180f);
        if (Quaternion.Angle(_rotateTransform.rotation, desiredRotation) <= tolerance)
            return TaskStatus.Success;

        if (rotateSpeed <= 0f)
            return TaskStatus.Failure;

        float deltaTime = _ctx.UsesWorldSlow && TimeSlowManager.Instance != null
            ? TimeSlowManager.Instance.WorldDeltaTime : Time.deltaTime;
        _rotateTransform.rotation = Quaternion.RotateTowards(
            _rotateTransform.rotation, desiredRotation, rotateSpeed * deltaTime);

        return Quaternion.Angle(_rotateTransform.rotation, desiredRotation) <= tolerance
            ? TaskStatus.Success : TaskStatus.Running;
    }

    public override void OnEnd()
    {
        if (_agent != null)
        {
            if (_changedUpdateRotation)
                _agent.updateRotation = _cachedUpdateRotation;
            if (_changedIsStopped && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
                _agent.isStopped = _cachedIsStopped;
        }
        _changedUpdateRotation = false;
        _changedIsStopped = false;
    }

    public override void Reset()
    {
        target = null;
        rotateRoot = null;
        rotateSpeed = 720f;
        angleTolerance = 3f;
        ignoreYAxis = true;
        stopAgentWhileAiming = true;
        disableAgentAutoRotation = true;
    }
}
