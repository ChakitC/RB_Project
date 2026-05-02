using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;

[Opsive.Shared.Utility.Category("Ally/Patrol")]
[Opsive.Shared.Utility.Description("ตั้งจุดหมายลาดตระเวนให้ NavMeshAgent หนึ่งจุด แล้วสำเร็จเมื่อ Agent เดินถึงจุดนั้น การตัดสินใจต่อสู้ควรอยู่ใน Task อื่น")]
public class Patrol : Action
{
    [Tooltip("จุดลาดตระเวนแบบเรียงลำดับ ถ้าไม่กำหนด จะสุ่มจุดรอบ patrolCenter หรือรอบ Agent ตัวนี้")]
    public Transform[] patrolPoints;

    [Tooltip("วัตถุศูนย์กลางสำหรับสุ่มจุดลาดตระเวน ถ้าไม่กำหนด จะใช้ตำแหน่งของ Agent ตัวนี้")]
    public SharedVariable<GameObject> patrolCenter;

    [Tooltip("ถ้าเป็น true จะจำตำแหน่ง patrolCenter ครั้งแรกไว้เป็นศูนย์กลางคงที่ เหมาะกับการตั้ง center เป็นตัวเองแล้วไม่ให้วง patrol ไหลตามตัว")]
    public SharedVariable<bool> lockPatrolCenterPosition;

    [Tooltip("รัศมีสุ่มจุดลาดตระเวน เมื่อไม่ได้กำหนด patrolPoints")]
    public SharedVariable<float> patrolRadius;

    [Tooltip("ระยะที่ NavMesh.SamplePosition ใช้ค้นหาจุดบน NavMesh จากตำแหน่งที่สุ่มหรือ waypoint")]
    public SharedVariable<float> navMeshSampleRadius;

    [Tooltip("จำนวนครั้งที่จะลองสุ่มจุด ก่อนคืนค่า Failure")]
    public SharedVariable<int> randomSampleAttempts;

    [Tooltip("ระยะหยุดของ NavMeshAgent ที่จะตั้งค่าก่อนสั่ง destination")]
    public SharedVariable<float> stopDistance;

    [Tooltip("เวลารอหลังเดินถึงจุดลาดตระเวน ก่อนจบ Task เพื่อให้รอบถัดไปเลือกจุดใหม่")]
    public SharedVariable<float> waitTime;

    [Tooltip("ถ้าเป็น true จะเลือก patrolPoints แบบสุ่ม ถ้าเป็น false จะเดินตามลำดับ")]
    public SharedVariable<bool> randomizePatrolPoints;

    [Tooltip("ตัวแปรปลายทางสำหรับส่งออกให้ Task อื่นใน Behavior Tree ใช้ต่อ")]
    public SharedVariable<Vector3> destination;

    private NavMeshAgent _agent;
    private int _nextPatrolPointIndex;
    private bool _destinationSet;
    private bool _isWaiting;
    private float _waitUntilTime;
    private bool _hasLockedPatrolCenterPosition;
    private Vector3 _lockedPatrolCenterPosition;

    public override void OnBehaviorTreeStarted()
    {
        _hasLockedPatrolCenterPosition = false;
    }

    public override void OnStart()
    {
        CacheAgent();
        CachePatrolCenterPositionIfNeeded();
        _destinationSet = false;
        _isWaiting = false;
        _waitUntilTime = 0f;
    }

    public override TaskStatus OnUpdate()
    {
        CacheAgent();

        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
        {
            return TaskStatus.Failure;
        }

        if (!_destinationSet && !TrySetPatrolDestination())
        {
            return TaskStatus.Failure;
        }

        if (_isWaiting)
        {
            return Time.time >= _waitUntilTime
                ? TaskStatus.Success
                : TaskStatus.Running;
        }

        if (HasArrived())
        {
            return StartWaitOrSucceed();
        }

        return TaskStatus.Running;
    }

    public override void Reset()
    {
        patrolRadius = 5f;
        lockPatrolCenterPosition = true;
        navMeshSampleRadius = 2f;
        randomSampleAttempts = 8;
        stopDistance = 0.3f;
        waitTime = 0f;
        randomizePatrolPoints = false;
        destination = Vector3.zero;
    }

    private bool TrySetPatrolDestination()
    {
        if (!TryGetPatrolDestination(out Vector3 patrolDestination))
        {
            return false;
        }

        if (stopDistance != null)
        {
            _agent.stoppingDistance = Mathf.Max(0f, stopDistance.Value);
        }

        _agent.isStopped = false;

        if (!_agent.SetDestination(patrolDestination))
        {
            return false;
        }

        _destinationSet = true;

        if (destination != null)
        {
            destination.Value = patrolDestination;
        }

        return true;
    }

    private TaskStatus StartWaitOrSucceed()
    {
        float waitSeconds = waitTime != null ? Mathf.Max(0f, waitTime.Value) : 0f;
        if (waitSeconds <= 0f)
        {
            return TaskStatus.Success;
        }

        _agent.isStopped = true;
        _isWaiting = true;
        _waitUntilTime = Time.time + waitSeconds;
        return TaskStatus.Running;
    }

    private bool HasArrived()
    {
        if (!_destinationSet || _agent.pathPending)
        {
            return false;
        }

        if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            return false;
        }

        float arrivalDistance = _agent.stoppingDistance + 0.05f;
        if (_agent.remainingDistance > arrivalDistance)
        {
            return false;
        }

        return !_agent.hasPath || _agent.velocity.sqrMagnitude < 0.01f;
    }

    private void CacheAgent()
    {
        if (_agent != null)
        {
            return;
        }

        _agent = gameObject.GetComponent<NavMeshAgent>();
        if (_agent == null)
        {
            _agent = gameObject.GetComponentInParent<NavMeshAgent>();
        }
    }

    private bool TryGetPatrolDestination(out Vector3 patrolDestination)
    {
        if (TryGetWaypointDestination(out patrolDestination))
        {
            return true;
        }

        return TryGetRandomDestination(out patrolDestination);
    }

    private bool TryGetWaypointDestination(out Vector3 patrolDestination)
    {
        patrolDestination = default;

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return false;
        }

        int pointCount = patrolPoints.Length;
        bool randomize = randomizePatrolPoints != null && randomizePatrolPoints.Value;
        int startIndex = randomize ? Random.Range(0, pointCount) : Mathf.Clamp(_nextPatrolPointIndex, 0, pointCount - 1);

        for (int offset = 0; offset < pointCount; offset++)
        {
            int pointIndex = (startIndex + offset) % pointCount;
            Transform point = patrolPoints[pointIndex];
            if (point == null)
            {
                continue;
            }

            if (!TrySampleNavMesh(point.position, out patrolDestination))
            {
                continue;
            }

            _nextPatrolPointIndex = (pointIndex + 1) % pointCount;
            return true;
        }

        return false;
    }

    private bool TryGetRandomDestination(out Vector3 patrolDestination)
    {
        patrolDestination = default;

        Vector3 center = GetPatrolCenterPosition();

        float radius = patrolRadius != null ? Mathf.Max(0f, patrolRadius.Value) : 0f;
        int attempts = randomSampleAttempts != null ? Mathf.Max(1, randomSampleAttempts.Value) : 1;

        for (int i = 0; i < attempts; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (TrySampleNavMesh(candidate, out patrolDestination))
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetPatrolCenterPosition()
    {
        bool lockCenter = lockPatrolCenterPosition != null && lockPatrolCenterPosition.Value;
        if (lockCenter)
        {
            CachePatrolCenterPositionIfNeeded();
            if (_hasLockedPatrolCenterPosition)
            {
                return _lockedPatrolCenterPosition;
            }
        }

        return ResolveCurrentPatrolCenterPosition();
    }

    private void CachePatrolCenterPositionIfNeeded()
    {
        bool lockCenter = lockPatrolCenterPosition != null && lockPatrolCenterPosition.Value;
        if (!lockCenter || _hasLockedPatrolCenterPosition)
        {
            return;
        }

        _lockedPatrolCenterPosition = ResolveCurrentPatrolCenterPosition();
        _hasLockedPatrolCenterPosition = true;
    }

    private Vector3 ResolveCurrentPatrolCenterPosition()
    {
        if (patrolCenter != null && patrolCenter.Value != null)
        {
            return patrolCenter.Value.transform.position;
        }

        return _agent != null ? _agent.transform.position : transform.position;
    }

    private bool TrySampleNavMesh(Vector3 candidate, out Vector3 patrolDestination)
    {
        float sampleRadius = navMeshSampleRadius != null ? Mathf.Max(0f, navMeshSampleRadius.Value) : 0f;
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
        {
            patrolDestination = default;
            return false;
        }

        patrolDestination = hit.position;
        return true;
    }
}
