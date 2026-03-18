using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;

[Opsive.Shared.Utility.Category("Ally/Follow")]
[NodeDescription("ใช้ NavMeshAgent วิ่งไปยืนตำแหน่งรอบ ๆ Player ตาม Offset")]
public class MoveToPlayerOffsetNavMesh : Action
{
    
    [Tooltip("ตัวแปร Taget ที่แชร์ใน Behavior Tree")]
    public SharedVariable<GameObject> Taget;

    [Tooltip("Offset จากตำแหน่ง Taget ใน local space เช่น (0,0,-2) = ยืนด้านหลัง")]
    public SharedVariable<Vector3> offsetFromPlayer;

    [Tooltip("ระยะที่ถือว่าเข้าใกล้พอแล้ว (เอาไว้ใช้แทน followMin)")]
    public SharedVariable<float> stopDistance;

  
    private NavMeshAgent _agent;

    public override void OnStart()
    {
        if (_agent == null)
        {
            _agent = gameObject.GetComponent<NavMeshAgent>();
        }

        if (_agent != null && stopDistance != null)
        {
            _agent.stoppingDistance = stopDistance.Value;
            _agent.isStopped = false;
        }
    }

    public override TaskStatus OnUpdate()
    {
        
        // ไม่มี Agent = ทำอะไรไม่ได้ → Fail
        if (_agent == null)
        {
            return TaskStatus.Failure;
        }
        
        
        // ไม่มี Taget ให้ตาม → หยุดแล้ว Fail
        if (Taget == null || Taget.Value == null)
        {
            _agent.isStopped = true;
            return TaskStatus.Failure;
        }

        // ตำแหน่งเป้าหมาย = ตำแหน่ง Taget + offset (หมุนตาม orientation ของ Player)
        Transform playerTransform = Taget.Value.transform;

        // แปลง offset จาก local → world (ให้ (0,0,-2) หมายถึง "ด้านหลัง" Taget จริง ๆ)
        Vector3 worldOffset = playerTransform.TransformDirection(offsetFromPlayer.Value);

        Vector3 targetPos = playerTransform.position + worldOffset;

        // ล็อกความสูงตาม Agent (ป้องกันหลุด navmesh บางเคส)
        targetPos.y = _agent.transform.position.y;

        // สั่งให้ Agent เดินไปตำแหน่งนี้
        _agent.SetDestination(targetPos);

        // ถ้า path คำนวณเสร็จแล้ว และเข้าใกล้ในระยะที่ต้องการ → Success
        if (!_agent.pathPending)
        {
            float remaining = _agent.remainingDistance;
            float stop = _agent.stoppingDistance;

            if (remaining <= stop + 0.05f)
            {
                _agent.isStopped = true;
                return TaskStatus.Success;
            }
        }
        
        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        // จบ task แล้วหยุด agent ให้เรียบร้อย
        if (_agent != null)
        {
            // _agent.isStopped = true;
        }
    }

    public override void Reset()
    {
        // ค่า default เวลา create node ใหม่ในกราฟ (จะปรับหรือปล่อยว่างก็ได้)
        offsetFromPlayer = Vector3.zero;
        stopDistance = 1.5f;
    }
}
