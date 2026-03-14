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

    void Awake()
    {
        if (!brain) brain = GetComponent<CharacterAnimBrain>();
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponent<Animator>();

        // ปกติให้ agent ขยับ (in-place)
        animator.applyRootMotion = false;
        _prevRM = brain && brain.RootMotionActive;
    }

    void Update()
    {
        bool rm = brain && brain.RootMotionActive;

        // ทำเฉพาะตอน "เปลี่ยนโหมด" (ไม่ต้อง set ซ้ำทุกเฟรม)
        if (rm != _prevRM)
        {
            if (rm) EnterRootMotion();
            else ExitRootMotion();
            _prevRM = rm;
        }

        // สำคัญ: ระหว่าง RM ต้อง sync nextPosition กัน agent ดึงวาป
        if (rm && agent && agent.enabled)
            agent.nextPosition = transform.position;
    }

    private void EnterRootMotion()
    {
        if (!agent) return;

        agent.isStopped = true;
        agent.updatePosition = false;
        agent.updateRotation = false;

        // sync ทันที กัน snap เฟรมแรก
        agent.nextPosition = transform.position;

        animator.applyRootMotion = true;
    }

    private void ExitRootMotion()
    {
        if (!agent) return;

        animator.applyRootMotion = false;

        // sync ก่อนเปิด updatePosition กลับ
        agent.nextPosition = transform.position;

        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.isStopped = false;

        // ไม่แนะนำให้ Warp ทุกครั้ง—ใช้เฉพาะกรณีหลุดไกลจริง ๆ (ค่อยเปิด)
        // ResyncAgent(0.5f);
    }

    void OnAnimatorMove()
    {
        if (!brain || !brain.RootMotionActive) return;
        if (!agent || !agent.enabled) return;

        Vector3 delta = animator.deltaPosition;
        if (zeroY) delta.y = 0f;

        // ✅ ขยับตัวละครจริง ๆ ด้วย root motion
        transform.position += delta;

        // ✅ กันวาป / sync agent ให้ตาม transform
        agent.nextPosition = transform.position;

        if (applyRootRotation)
            transform.rotation *= animator.deltaRotation;
    }

    // เรียกเฉพาะจำเป็นจริง ๆ
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
