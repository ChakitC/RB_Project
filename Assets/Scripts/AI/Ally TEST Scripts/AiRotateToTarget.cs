using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;

public class AiRotateToTarget : Action
{
    public SharedVariable<CharacteContext> context;
    [Header("Target")]
    public SharedVariable<GameObject> target;

    [Tooltip("ถ้าใส่ จะหมุน transform นี้แทนตัว root หลัก เช่น model/pivot")]
    public SharedVariable<GameObject> rotateRoot;

    [Header("Rotate Settings")]
    [Tooltip("ความเร็วในการหมุน (deg/sec)")]
    public float rotateSpeed = 720f;

    [Tooltip("ปกติ NPC ยิงกันในระนาบพื้น เลยควรไม่สนแกน Y")]
    public bool ignoreYAxis = true;

    [Header("NavMeshAgent")]
    [Tooltip("หยุดเดินระหว่างเล็ง")]
    public bool stopAgentWhileAiming = true;

    [Tooltip("ปิด auto rotation ของ NavMeshAgent ระหว่าง task ทำงาน")]
    public bool disableAgentAutoRotation = true;

    [Header("When No Target")]
    [Tooltip("ถ้าไม่มี target ให้ return Failure, ถ้าปิดจะ return Success")]
    public bool failWhenTargetLost = true;

    private NavMeshAgent _agent;
    private Transform _self;
    private Transform _rotateTransform;

    private bool _cachedUpdateRotation;
    private bool _cachedIsStopped;
    private CharacteContext _ctx;
    int executionGeneration;
    CharacterMovementMotor executionMotor;
    int rotationToken;
    bool capturedAgent;
    private AITargetSensor _sensor;

    public override void OnAwake()
    {
        _self = transform;

    }

    public override void OnStart()
    {
        _ctx = AIContextBinding.Read(context);
        executionGeneration = _ctx != null ? _ctx.LifeGeneration : 0;
        executionMotor = _ctx != null ? _ctx.MovementMotor : null;
        _agent = _ctx != null ? _ctx.NavigationAgent : null;
        _sensor = _ctx != null ? _ctx.AITargetSensor : null;
        _self = _ctx != null ? _ctx.transform : transform;
        rotationToken = 0;
        capturedAgent = false;

        _rotateTransform =
            (rotateRoot != null && rotateRoot.Value != null)
            ? rotateRoot.Value.transform
            : _self;

        if (_ctx != null && _ctx.stateHub != null && _sensor != null && _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
        {
            capturedAgent = true;
            _cachedUpdateRotation = _agent.updateRotation;
            _cachedIsStopped = _agent.isStopped;

            if (disableAgentAutoRotation && executionMotor != null)
                rotationToken = executionMotor.AcquireAgentSuspension(_agent, false, true);
            else if (disableAgentAutoRotation)
                _agent.updateRotation = false;

            if (stopAgentWhileAiming)
                _agent.isStopped = true;
        }
    }

    public override TaskStatus OnUpdate()
    {
        if (!AIContextBinding.IsCurrent(context, _ctx, executionGeneration) ||
            _ctx.stateHub == null || _sensor == null || _ctx.AITargetSensor != _sensor)
            return TaskStatus.Failure;
        if (target == null || target.Value == null)
            return failWhenTargetLost ? TaskStatus.Failure : TaskStatus.Success;

        // The shared variable this task rotates toward is only refreshed when the tree
        // re-evaluates the branch that set it — it does not react to a mid-task retarget
        // (e.g. taunt updating AITargetSensor.CurrentTarget). Bail out so the tree re-selects
        // with the fresh target instead of continuing to face a stale one indefinitely.
        if (_sensor != null)
        {
            Transform sensorTarget = _sensor.Snapshot.CurrentTarget;
            if (sensorTarget == null || (sensorTarget.gameObject != target.Value &&
                !sensorTarget.IsChildOf(target.Value.transform) && !target.Value.transform.IsChildOf(sensorTarget)))
            {
                return TaskStatus.Failure;
            }
        }

        if (!_ctx.stateHub.CanRotate()) { return TaskStatus.Failure; }

        if (_rotateTransform == null)
            _rotateTransform = _self;

        Vector3 from = _rotateTransform.position;
        Vector3 to = target.Value.transform.position;

        if (ignoreYAxis)
            to.y = from.y;

        Vector3 dir = to - from;

        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

            _rotateTransform.rotation = Quaternion.RotateTowards(
                _rotateTransform.rotation,
                desiredRotation,
                rotateSpeed * TimeSlowManager.Instance.WorldDeltaTime
            );
        }

        // มี target อยู่ = หมุนต่อไปเรื่อย ๆ และค้าง Running
        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        if (_ctx != null && _ctx.LifeGeneration == executionGeneration)
        {
            if (rotationToken != 0 && executionMotor != null)
                executionMotor.ReleaseAgentSuspension(rotationToken);
            else if (capturedAgent && _agent != null && disableAgentAutoRotation)
                _agent.updateRotation = _cachedUpdateRotation;
            if (capturedAgent && _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh &&
                stopAgentWhileAiming && AICombatTaskUtility.CanAct(_ctx))
                _agent.isStopped = _cachedIsStopped;
        }
        rotationToken = 0;
        capturedAgent = false;
    }

    public override void OnBehaviorTreeStopped(bool paused) { if (!paused) OnEnd(); }
    public override void OnDestroy() => OnEnd();

    public override void Reset()
    {
        target = null;
        rotateRoot = null;
        rotateSpeed = 720f;
        ignoreYAxis = true;
        stopAgentWhileAiming = true;
        disableAgentAutoRotation = true;
        failWhenTargetLost = true;
    }
}
