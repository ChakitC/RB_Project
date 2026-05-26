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

    [Header("Player Keepout")]
    [SerializeField] private bool keepDestinationOutsidePlayer = true;
    [SerializeField, Min(0f)] private float playerKeepoutRadius = 0.65f;
    [SerializeField, Min(0f)] private float destinationMargin = 0.1f;
    [SerializeField, Min(0f)] private float destinationSampleRadius = 1.5f;

    private NavMeshAgent _agent;
    private CharacteContext _ctx;
    private CharacterAnimBrain _animBrain;

    public override void OnStart()
    {
        CacheReferences();

        if (_agent != null && stopDistance != null)
        {
            _agent.stoppingDistance = stopDistance.Value;
            ResumeAgentIfAllowed();
        }
    }

    public override TaskStatus OnUpdate()
    {
        CacheReferences();
        
        // ไม่มี Agent = ทำอะไรไม่ได้ → Fail
        if (_agent == null || !_agent.isActiveAndEnabled)
        {
            return TaskStatus.Failure;
        }
        
        
        // ไม่มี Taget ให้ตาม → หยุดแล้ว Fail
        if (Taget == null || Taget.Value == null)
        {
            StopAgentIfReady();
            return TaskStatus.Failure;
        }

        if (IsRootMotionActive())
        {
            return TaskStatus.Running;
        }

        if (!_agent.isOnNavMesh && !TryRecoverAgentOnNavMesh())
        {
            return TaskStatus.Failure;
        }

        // ตำแหน่งเป้าหมาย = ตำแหน่ง Taget + offset (หมุนตาม orientation ของ Player)
        Transform playerTransform = Taget.Value.transform;

        // แปลง offset จาก local → world (ให้ (0,0,-2) หมายถึง "ด้านหลัง" Taget จริง ๆ)
        Vector3 worldOffset = playerTransform.TransformDirection(offsetFromPlayer.Value);

        Vector3 targetPos = playerTransform.position + worldOffset;

        // ล็อกความสูงตาม Agent (ป้องกันหลุด navmesh บางเคส)
        targetPos.y = _agent.transform.position.y;
        targetPos = ResolveDestinationOutsidePlayerKeepout(playerTransform, targetPos);

        // สั่งให้ Agent เดินไปตำแหน่งนี้
        ResumeAgentIfAllowed();

        if (!_agent.SetDestination(targetPos))
        {
            return TaskStatus.Failure;
        }

        // ถ้า path คำนวณเสร็จแล้ว และเข้าใกล้ในระยะที่ต้องการ → Success
        if (!_agent.pathPending)
        {
            float remaining = _agent.remainingDistance;
            float stop = _agent.stoppingDistance;

            if (remaining <= stop + 0.05f)
            {
                StopAgentIfReady();
                return TaskStatus.Success;
            }
        }
        
        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        StopAgentIfReady();
    }

    public override void Reset()
    {
       
        offsetFromPlayer = Vector3.zero;
        stopDistance = 1.5f;
    }

    private void CacheReferences()
    {
        if (_agent == null)
        {
            _agent = gameObject.GetComponentInParent<NavMeshAgent>();
        }

        if (_ctx == null)
        {
            _ctx = gameObject.GetComponentInParent<CharacteContext>();
        }

        if (_animBrain != null)
        {
            return;
        }

        if (_ctx != null && _ctx.AnimBrain != null)
        {
            _animBrain = _ctx.AnimBrain;
            return;
        }

        _animBrain = gameObject.GetComponentInChildren<CharacterAnimBrain>(true);
        if (_animBrain == null)
        {
            _animBrain = gameObject.GetComponentInParent<CharacterAnimBrain>();
        }
    }

    private bool IsRootMotionActive()
    {
        return _animBrain != null && _animBrain.RootMotionActive;
    }

    private void ResumeAgentIfAllowed()
    {
        if (!CanControlAgent() || IsRootMotionActive())
        {
            return;
        }

        if (!_agent.updatePosition)
        {
            _agent.updatePosition = true;
            _agent.nextPosition = _agent.transform.position;
        }

        if (_agent.isStopped)
        {
            _agent.isStopped = false;
        }
    }

    private bool TryRecoverAgentOnNavMesh()
    {
        if (_agent == null || !_agent.isActiveAndEnabled)
        {
            return false;
        }

        if (_agent.isOnNavMesh)
        {
            return true;
        }

        if (!NavMesh.SamplePosition(_agent.transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            return false;
        }

        return _agent.Warp(hit.position);
    }

    private bool CanControlAgent()
    {
        return _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh;
    }

    private void StopAgentIfReady()
    {
        if (CanControlAgent())
        {
            _agent.isStopped = true;
        }
    }

    private Vector3 ResolveDestinationOutsidePlayerKeepout(Transform playerTransform, Vector3 targetPos)
    {
        if (!keepDestinationOutsidePlayer || playerTransform == null || _agent == null)
            return targetPos;

        Transform playerRoot = ResolvePlayerRoot(playerTransform);
        Vector3 playerPosition = playerRoot.position;
        float keepoutRadius = ResolvePlayerKeepoutRadius(playerRoot) + Mathf.Max(_agent.radius, 0f) + destinationMargin;
        Vector3 offset = targetPos - playerPosition;
        offset.y = 0f;

        if (offset.sqrMagnitude < keepoutRadius * keepoutRadius)
        {
            Vector3 direction = offset.sqrMagnitude > 0.0001f
                ? offset.normalized
                : ResolveFallbackKeepoutDirection(playerRoot, playerPosition);

            targetPos = playerPosition + direction * keepoutRadius;
        }

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, destinationSampleRadius, _agent.areaMask))
            targetPos = hit.position;

        return targetPos;
    }

    private Transform ResolvePlayerRoot(Transform playerTransform)
    {
        CharacteContext playerContext = playerTransform.GetComponentInParent<CharacteContext>();
        return playerContext != null ? playerContext.transform : playerTransform;
    }

    private float ResolvePlayerKeepoutRadius(Transform playerRoot)
    {
        float radius = Mathf.Max(playerKeepoutRadius, 0f);
        CharacterController playerController = playerRoot.GetComponent<CharacterController>();
        if (playerController != null)
            radius = Mathf.Max(radius, playerController.radius);

        NavMeshObstacle obstacle = playerRoot.GetComponent<NavMeshObstacle>();
        if (obstacle != null)
            radius = Mathf.Max(radius, obstacle.radius);

        return Mathf.Max(radius, 0.01f);
    }

    private Vector3 ResolveFallbackKeepoutDirection(Transform playerRoot, Vector3 playerPosition)
    {
        Vector3 direction = _agent.transform.position - playerPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = -playerRoot.forward;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        return direction.normalized;
    }
}
