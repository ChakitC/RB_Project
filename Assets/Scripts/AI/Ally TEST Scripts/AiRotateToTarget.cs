using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;

public class AiRotateToTarget : Action
{
    [Header("Target")]
    public SharedVariable<GameObject> target;

    [Tooltip("ถ้าใส่ จะหมุน transform นี้แทนตัว root หลัก เช่น model/pivot")]
    public SharedVariable<Transform> rotateRoot;

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

    public override void OnAwake()
    {
        _self = transform;
        _agent = gameObject.GetComponent<NavMeshAgent>();
    }

    public override void OnStart()
    {
        _rotateTransform =
            (rotateRoot != null && rotateRoot.Value != null)
            ? rotateRoot.Value
            : _self;

        if (_agent != null)
        {
            _cachedUpdateRotation = _agent.updateRotation;
            _cachedIsStopped = _agent.isStopped;

            if (disableAgentAutoRotation)
                _agent.updateRotation = false;

            if (stopAgentWhileAiming)
                _agent.isStopped = true;
        }
    }

    public override TaskStatus OnUpdate()
    {
        if (target == null || target.Value == null)
            return failWhenTargetLost ? TaskStatus.Failure : TaskStatus.Success;

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
                rotateSpeed * Time.deltaTime
            );
        }

        // มี target อยู่ = หมุนต่อไปเรื่อย ๆ และค้าง Running
        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        if (_agent != null)
        {
            if (disableAgentAutoRotation)
                _agent.updateRotation = _cachedUpdateRotation;

            if (stopAgentWhileAiming)
                _agent.isStopped = _cachedIsStopped;
        }
    }

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