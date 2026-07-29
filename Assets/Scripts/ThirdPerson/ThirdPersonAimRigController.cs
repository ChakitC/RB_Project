using UnityEngine;

[DefaultExecutionOrder(150)]
[DisallowMultipleComponent]
public sealed class ThirdPersonAimRigController : MonoBehaviour
{
    [SerializeField] private CharacteContext characterContext;
    [SerializeField, Min(0f)] private float blendSpeed = 12f;

    Animator animator;
    Transform spine;
    Transform chest;
    Transform upperChest;
    AIAimTargetDriver aiAimTargetDriver;
    readonly ThirdPersonCharacterProfile fallbackProfile =
        ThirdPersonCharacterProfile.CreateDefault();
    float currentWeight;
    bool aimDriverResolved;

    void Awake()
    {
        ResolveReferences();
    }

    void LateUpdate()
    {
        ResolveReferences();
        if (animator == null || !animator.isHuman)
            return;

        ThirdPersonCharacterProfile profile = characterContext != null &&
                                             characterContext.baseStats != null &&
                                             characterContext.baseStats.thirdPersonProfile != null
            ? characterContext.baseStats.thirdPersonProfile
            : fallbackProfile;

        bool active = TryResolveAimPoint(out Vector3 aimPoint);
        float targetWeight = active ? 1f : 0f;
        currentWeight = Mathf.MoveTowards(
            currentWeight,
            targetWeight,
            blendSpeed * Time.deltaTime);
        if (currentWeight <= 0.001f)
            return;

        Transform reference = characterContext != null ? characterContext.transform : transform;
        Vector3 origin = chest != null ? chest.position : reference.position + Vector3.up;
        Vector3 direction = aimPoint - origin;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        direction.Normalize();
        Vector3 planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (planarDirection.sqrMagnitude <= 0.0001f)
            planarDirection = reference.forward;

        float yaw = Vector3.SignedAngle(reference.forward, planarDirection, Vector3.up);
        float pitch = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
        yaw = Mathf.Clamp(yaw, -profile.maximumUpperBodyYaw, profile.maximumUpperBodyYaw);
        pitch = Mathf.Clamp(pitch, -profile.maximumUpperBodyPitch, profile.maximumUpperBodyPitch);

        float totalBoneWeight = profile.spineAimWeight +
                                profile.chestAimWeight +
                                profile.upperChestAimWeight;
        float normalization = totalBoneWeight > 1f
            ? 1f / totalBoneWeight
            : 1f;

        ApplyBoneAim(
            spine,
            yaw,
            pitch,
            profile.spineAimWeight * normalization * currentWeight,
            reference);
        ApplyBoneAim(
            chest,
            yaw,
            pitch,
            profile.chestAimWeight * normalization * currentWeight,
            reference);
        ApplyBoneAim(
            upperChest,
            yaw,
            pitch,
            profile.upperChestAimWeight * normalization * currentWeight,
            reference);
    }

    bool TryResolveAimPoint(out Vector3 aimPoint)
    {
        aimPoint = Vector3.zero;
        if (characterContext == null)
            return false;

        if (characterContext is PlayerContext player)
        {
            ThirdPersonAimController aimController = player.thirdPersonAim;
            bool active = GameplayCameraController.Instance != null &&
                          GameplayCameraController.Instance.HasCombatAlignment;
            if (aimController == null || !active)
                return false;

            aimPoint = aimController.AimPoint;
            return true;
        }

        WeaponSystem weapon = characterContext.WeaponSystem;
        bool weaponActive = weapon != null &&
                            (weapon.IsAiming || weapon.IsFiringActivity || weapon.IsFiringHeld);
        if (!weaponActive)
            return false;

        if (aiAimTargetDriver != null && aiAimTargetDriver.AimTarget != null)
        {
            aimPoint = aiAimTargetDriver.AimTarget.position;
            return true;
        }

        if (characterContext.EnegySystem != null &&
            characterContext.EnegySystem.AimTransform != null)
        {
            aimPoint = characterContext.EnegySystem.AimTransform.position;
            return true;
        }

        return false;
    }

    void ResolveReferences()
    {
        if (characterContext == null)
            characterContext = GetComponent<CharacteContext>();
        if (characterContext == null)
            characterContext = GetComponentInParent<CharacteContext>();

        Animator nextAnimator = characterContext != null &&
                                characterContext.Visual != null
            ? characterContext.Visual.animator
            : null;
        if (nextAnimator == null)
            nextAnimator = GetComponentInChildren<Animator>(true);

        if (animator != nextAnimator)
        {
            animator = nextAnimator;
            spine = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Spine)
                : null;
            chest = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Chest)
                : null;
            upperChest = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.UpperChest)
                : null;
        }

        if (!aimDriverResolved && characterContext != null)
        {
            aiAimTargetDriver = GetComponent<AIAimTargetDriver>();
            if (aiAimTargetDriver == null)
            {
                aiAimTargetDriver =
                    GetComponentInChildren<AIAimTargetDriver>(true);
            }

            aimDriverResolved = true;
        }
    }

    static void ApplyBoneAim(
        Transform bone,
        float yaw,
        float pitch,
        float weight,
        Transform reference)
    {
        if (bone == null || weight <= 0.001f)
            return;

        Quaternion yawRotation = Quaternion.AngleAxis(yaw * weight, Vector3.up);
        Quaternion pitchRotation = Quaternion.AngleAxis(
            -pitch * weight,
            reference.right);
        bone.rotation = yawRotation * pitchRotation * bone.rotation;
    }
}
