using UnityEngine;

public class RootMotionCCDriver : MonoBehaviour
{
    [SerializeField] private CharacterAnimBrain brain;
    [SerializeField] private bool zeroY = false;
    [SerializeField] private bool applyRootRotation = false;

    [SerializeField] private CharacterController cc;
    [SerializeField] private Animator animator;

    void Awake()
    {
        ResolveReferences();

        if (animator)
            animator.applyRootMotion = false;
    }

    public void Configure(CharacterAnimBrain animBrain, CharacterController characterController, Animator sourceAnimator, bool suppressY = false)
    {
        brain = animBrain;
        cc = characterController;
        animator = sourceAnimator;
        zeroY = suppressY;

        if (animator)
            animator.applyRootMotion = false;
    }

    void ResolveReferences()
    {
        if (!cc)
            cc = GetComponent<CharacterController>();
        if (!cc)
            cc = GetComponentInParent<CharacterController>();
        if (!cc)
            cc = GetComponentInChildren<CharacterController>(true);

        if (!animator)
            animator = GetComponent<Animator>();
        if (!animator)
            animator = GetComponentInParent<Animator>();
        if (!animator)
            animator = GetComponentInChildren<Animator>(true);

        if (!brain)
            brain = GetComponent<CharacterAnimBrain>();
        if (!brain)
            brain = GetComponentInParent<CharacterAnimBrain>();
        if (!brain)
            brain = GetComponentInChildren<CharacterAnimBrain>(true);
    }

    void OnAnimatorMove()
    {
        if (!brain || !brain.RootMotionActive) return;
        if (!cc || !animator) return;

        Vector3 delta = animator.deltaPosition;
        if (zeroY || brain.RootMotionPlanarOnly) delta.y = 0f;

        cc.Move(delta);

        if (applyRootRotation || brain.RootMotionYawActive)
        {
            float yawDelta = Mathf.DeltaAngle(0f, animator.deltaRotation.eulerAngles.y);
            Transform actorRoot = cc.transform;
            actorRoot.rotation *= Quaternion.AngleAxis(yawDelta, Vector3.up);
        }
    }
}
