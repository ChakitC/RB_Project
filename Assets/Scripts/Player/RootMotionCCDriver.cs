using UnityEngine;

public class RootMotionCCDriver : MonoBehaviour
{
    [SerializeField] private CharacterAnimBrain brain; // หรือใส่ ref อื่นที่บอกว่า RootMotionActive
    [SerializeField] private bool zeroY = true;
    [SerializeField] private bool applyRootRotation = false;

    private CharacterController cc;
    private Animator animator;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        if (!brain) brain = GetComponent<CharacterAnimBrain>();

        // ให้ Animator เรียก OnAnimatorMove และเรารับผิดชอบการขยับเอง
        animator.applyRootMotion = true;
    }

    void OnAnimatorMove()
    {
        if (!brain || !brain.RootMotionActive) return;

        Vector3 delta = animator.deltaPosition;
        if (zeroY) delta.y = 0f;

        cc.Move(delta);

        if (applyRootRotation)
            transform.rotation *= animator.deltaRotation;
    }
}