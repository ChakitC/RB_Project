using UnityEngine;
using UnityEngine.AI;

public class AgentMoveDriver : MonoBehaviour
{
    [SerializeField] NavMeshAgent agent;
    [SerializeField] StateHub stateHub;
    [SerializeField] CharacteContext ctx;

    [Header("Ramp")]
    [SerializeField] float rampUpTime = 0.12f;
    [SerializeField] float rampDownTime = 0.18f;

    [Header("IsMoving (stable)")]
    [SerializeField] float move01Start = 0.10f;   // ต้องมากกว่านี้ถึงจะ “เริ่มเดิน”
    [SerializeField] float move01Stop  = 0.05f;   // ต้องต่ำกว่านี้ถึงจะ “หยุด”
    [SerializeField] float enterDelay  = 0.06f;   // หน่วงก่อนเปลี่ยนเป็น true
    [SerializeField] float exitDelay   = 0.10f;   // หน่วงก่อนเปลี่ยนเป็น false

    float _move01;
    float _aboveT, _belowT;

    public bool agentismoving;

    void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        UpdateAIMoveAnimFromNavMesh(agent);
    }

    void UpdateAIMoveAnimFromNavMesh(NavMeshAgent agent)
    {
        if (!agent || !agent.enabled || !agent.isOnNavMesh)
        {
            agentismoving = false;
            _move01 = 0f;
            stateHub?.SetMoveSpeed01(0f);
            return;
        }

        float dt = Time.deltaTime;

        // --- 1) velocity บนพื้น ---
        Vector3 vel = agent.velocity; vel.y = 0f;
        Vector3 desired = agent.desiredVelocity; desired.y = 0f;

        // --- 2) arrived ---
        bool arrived = !agent.pathPending &&
                       (agent.remainingDistance <= agent.stoppingDistance + 0.02f) &&
                       (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f);

        Vector3 moveWorldDir;
        float raw01;

        if (arrived)
        {
            moveWorldDir = Vector3.zero;
            raw01 = 0f;
        }
        else
        {
            // ทิศทาง: vel ก่อน ถ้า 0 ค่อยใช้ desired
            Vector3 dirSrc = (vel.sqrMagnitude > 0.0001f) ? vel : desired;
            moveWorldDir = (dirSrc.sqrMagnitude > 0.0001f) ? dirSrc.normalized : Vector3.zero;

            // ความเร็ว 0..1: ใช้ vel ถ้ามี ไม่งั้นใช้ desired (กันเฟรมเริ่มเดิน vel ยัง 0)
            float speedNow = (vel.sqrMagnitude > 0.0001f) ? vel.magnitude : desired.magnitude;
            float denom = Mathf.Max(agent.speed, 0.001f);
            raw01 = Mathf.Clamp01(speedNow / denom);
        }

        // --- 3) ramp _move01 ---
        float upSpeed   = (rampUpTime   <= 0.0001f) ? 999f : 1f / rampUpTime;
        float downSpeed = (rampDownTime <= 0.0001f) ? 999f : 1f / rampDownTime;

        float target01 = raw01;
        float s = (target01 > _move01) ? upSpeed : downSpeed;
        _move01 = Mathf.MoveTowards(_move01, target01, s * dt);

        stateHub?.SetMoveSpeed01(_move01);

        // --- 4) agentismoving (นิ่ง ไม่สลับรัว) ---
        bool wantMove = !arrived && _move01 > move01Start && agent.hasPath && !agent.isStopped && !agent.pathPending;
        bool wantStop = arrived || _move01 < move01Stop || agent.isStopped || (!agent.hasPath && !agent.pathPending);

        if (!agentismoving)
        {
            _aboveT = wantMove ? (_aboveT + dt) : 0f;
            if (_aboveT >= enterDelay) { agentismoving = true; _aboveT = 0f; }
        }
        else
        {
            _belowT = wantStop ? (_belowT + dt) : 0f;
            if (_belowT >= exitDelay) { agentismoving = false; _belowT = 0f; }
        }

        // --- 5) ส่ง dir เข้า AnimBrain ---
        if (ctx != null && ctx.AnimBrain != null)
        {
            if (moveWorldDir.sqrMagnitude < 0.0001f)
            {
                ctx.AnimBrain.MoveDirLocal = Vector2.zero;
            }
            else
            {
                Vector3 moveLocal3 = transform.InverseTransformDirection(moveWorldDir);
                Vector2 dirLocal = new Vector2(moveLocal3.x, moveLocal3.z);
                if (dirLocal.sqrMagnitude > 1.0001f) dirLocal.Normalize();
                ctx.AnimBrain.MoveDirLocal = dirLocal;
            }
        }
    }
}