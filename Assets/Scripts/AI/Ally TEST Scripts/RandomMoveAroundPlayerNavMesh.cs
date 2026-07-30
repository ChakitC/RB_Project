using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;

[Opsive.Shared.Utility.Category("Ally/Follow")]
[Opsive.Shared.Utility.Description("เดินสุ่มรอบ ๆ Player ด้วย NavMeshAgent ถ้า Player หนีไกลเกิน followMax จะหยุดและ Fail")]
public class RandomMoveAroundPlayerNavMesh : Action
{
    [Tooltip("ตัวแปร Taget ที่แชร์ใน Behavior Tree")]
    public SharedVariable<GameObject> Taget;

    [Tooltip("รัศมีสุ่มรอบตัว Taget")]
    public SharedVariable<float> wanderRadius;

    [Tooltip("ระยะที่ถ้า Agent ห่างจาก Taget มากกว่านี้ จะหยุด Random แล้ว Fail")]
    public SharedVariable<float> followMax;

    [Tooltip("ระยะหยุดของ Agent")]
    public SharedVariable<float> stopDistance;

    [Tooltip("เวลาที่ Agent จะหยุดรอเมื่อถึงจุด ก่อนจะสุ่มเดินรอบต่อไป (วินาที)")]
    public SharedVariable<float> waitTime;

    private NavMeshAgent _agent;
    private Vector3 _currentTarget;

    // state สำหรับการรอ
    private bool _isWaiting;
    private float _waitUntilTime;

    public override void OnStart()
    {
        if (_agent == null)
        {
            _agent = GetComponent<NavMeshAgent>();
            
            if (_agent == null)
            {
                _agent = gameObject.GetComponentInParent<NavMeshAgent>();
            }
        }

        if (_agent != null && stopDistance != null)
        {
            _agent.stoppingDistance = stopDistance.Value;
            _agent.isStopped = false;
        }

        _isWaiting = false;
        _waitUntilTime = 0f;

        PickNewDestination();
    }

    private void PickNewDestination()
    {
        if (Taget == null || Taget.Value == null || _agent == null)
        {
            
        }
        
        Transform playerTransform = Taget.Value.transform;
        Vector3 playerPos = playerTransform.position;

        // สุ่มจุดในวงกลมรอบ Player บนระนาบ XZ
        Vector2 randomCircle = Random.insideUnitCircle * wanderRadius.Value;
        Vector3 candidate = playerPos + new Vector3(randomCircle.x, 0f, randomCircle.y);

        // Sample ให้แน่ใจว่าอยู่บน NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(candidate, out hit, 2f, NavMesh.AllAreas))
        {
            _currentTarget = hit.position;
        }
        else
        {
            _currentTarget = candidate;
        }

        _currentTarget.y = _agent.transform.position.y;

        _agent.isStopped = false;
        _agent.SetDestination(_currentTarget);
    }

    public override TaskStatus OnUpdate()
    {
        if (_agent == null || Taget == null || Taget.Value == null)
        {
            return TaskStatus.Failure;
            
        }

        // 1) เช็คว่าตอนนี้ Player ไกลเกิน followMax หรือยัง
        Vector3 myPos = transform.position;
        Vector3 playerPos = Taget.Value.transform.position;
        myPos.y = 0f;
        playerPos.y = 0f;

        float distToPlayer = Vector3.Distance(myPos, playerPos);

        if (distToPlayer > followMax.Value)
        {
            
            _agent.isStopped = true;
            Debug.LogWarning("Player is too far. Stopping and returning Failure.");
            return TaskStatus.Failure;
        }

        // ถ้ากำลังอยู่ในช่วง "รอ" หลังจากถึงจุดแล้ว
        if (_isWaiting)
        {
            // ถ้ารอครบเวลาแล้ว → จบ cycle นี้ด้วย Success
            // ให้ Repeater/Sequence ภายนอกสั่งเริ่ม Task ใหม่ → จะสุ่มจุดใหม่ใน OnStart
            if (TimeSlowManager.Instance.WorldTime >= _waitUntilTime)
            {
                _isWaiting = false;
                return TaskStatus.Success;
            }

            // ยังไม่ครบเวลา → ยืนรอ (Running)
            return TaskStatus.Running;
        }

        // 2) ถ้ายังไม่ไกลไป → ดูว่าถึงจุดสุ่มแล้วยัง
        if (!_agent.pathPending)
        {
            float remaining = _agent.remainingDistance;
            float stop = _agent.stoppingDistance;

            if (remaining <= stop + 0.05f)
            {
                // ถึงจุดสุ่มแล้ว → เริ่มเข้าสู่ state "รอ"
                _agent.isStopped = true;

                float wt = (waitTime != null) ? waitTime.Value : 0f;

                if (wt > 0f)
                {
                    _isWaiting = true;
                    _waitUntilTime = TimeSlowManager.Instance.WorldTime + wt;
                    // ตอนนี้ Enter โหมดรอ → Running ไปก่อน
                    return TaskStatus.Running;
                }
                else
                {
                    // ไม่ได้ตั้งเวลาให้รอ → Success ทันทีเหมือนเวอร์ชันเดิม
                    return TaskStatus.Success;
                }
            }
        }

        // 3) ยังเดินอยู่ → Running
        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        if (_agent != null)
        {
            _agent.isStopped = true;
        }

        _isWaiting = false;
    }

    public override void Reset()
    {
        wanderRadius = 2f;
        followMax = 6f;
        stopDistance = 0.3f;
        waitTime = 0f;  
    }
}
