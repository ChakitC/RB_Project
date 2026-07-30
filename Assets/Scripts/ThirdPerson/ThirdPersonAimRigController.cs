using UnityEngine;

[DefaultExecutionOrder(10100)]
[DisallowMultipleComponent]
public sealed class ThirdPersonAimRigController : MonoBehaviour
{
    const string FirePointAimPivotName = "Upper-body Aim Fire Point Pivot";

    [SerializeField] private CharacteContext characterContext;
    [SerializeField, Min(0f)] private float blendSpeed = 12f;

    Animator animator;
    Transform spine;
    Transform chest;
    Transform upperChest;
    Transform firePoint;
    Transform firePointSourceParent;
    Transform firePointAimPivot;
    AIAimTargetDriver aiAimTargetDriver;
    readonly ThirdPersonCharacterProfile fallbackProfile =
        ThirdPersonCharacterProfile.CreateDefault();
    float currentWeight;
    float currentPitch;
    bool aimDriverResolved;
    bool missingBoneMapWarningIssued;

    void Awake()
    {
        ResolveReferences();
    }

    void OnDisable()
    {
        ResetFirePointAimPivot();
    }

    void LateUpdate()
    {
        ResolveReferences();
        if (animator == null || !HasAimBones())
        {
            ResetFirePointAimPivot();
            return;
        }

        ThirdPersonCharacterProfile profile = characterContext != null &&
                                             characterContext.baseStats != null &&
                                             characterContext.baseStats.thirdPersonProfile != null
            ? characterContext.baseStats.thirdPersonProfile
            : fallbackProfile;

        float pitch = 0f;
        bool active = CanApplyVisualAim() &&
                      TryResolveAimPoint(out Vector3 aimPoint) &&
                      TryResolvePitch(aimPoint, profile, out pitch);
        if (active)
            currentPitch = pitch;

        float targetWeight = active ? 1f : 0f;
        currentWeight = Mathf.MoveTowards(
            currentWeight,
            targetWeight,
            blendSpeed * Time.deltaTime);
        if (currentWeight <= 0.001f)
        {
            ResetFirePointAimPivot();
            return;
        }

        float totalBoneWeight =
            GetActiveBoneWeight(spine, profile.spineAimWeight) +
            GetActiveBoneWeight(chest, profile.chestAimWeight) +
            GetActiveBoneWeight(upperChest, profile.upperChestAimWeight);
        if (totalBoneWeight <= 0.001f)
        {
            ResetFirePointAimPivot();
            return;
        }

        float normalization = 1f / totalBoneWeight;
        Transform reference =
            characterContext != null ? characterContext.transform : transform;
        ApplyBonePitch(
            spine,
            currentPitch,
            profile.spineAimWeight * normalization * currentWeight,
            reference);
        ApplyBonePitch(
            chest,
            currentPitch,
            profile.chestAimWeight * normalization * currentWeight,
            reference);
        ApplyBonePitch(
            upperChest,
            currentPitch,
            profile.upperChestAimWeight * normalization * currentWeight,
            reference);
        ApplyFirePointPitch(reference);
    }

    bool CanApplyVisualAim()
    {
        if (characterContext == null)
            return false;

        StateHub stateHub = characterContext.stateHub;
        if (stateHub != null &&
            (!stateHub.IsAlive || stateHub.Isdown || !stateHub.CanRotate()))
        {
            return false;
        }

        CharacterAnimBrain animBrain = characterContext.AnimBrain;
        return animBrain == null || !animBrain.IsExclusiveLocomotionActive;
    }

    bool TryResolvePitch(
        Vector3 aimPoint,
        ThirdPersonCharacterProfile profile,
        out float pitch)
    {
        Transform reference =
            characterContext != null ? characterContext.transform : transform;
        Transform originBone = chest != null
            ? chest
            : upperChest != null
                ? upperChest
                : spine;
        Vector3 origin = originBone != null
            ? originBone.position
            : reference.position + Vector3.up;
        Vector3 direction = aimPoint - origin;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            pitch = 0f;
            return false;
        }

        direction.Normalize();
        pitch = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) *
                Mathf.Rad2Deg;
        pitch = Mathf.Clamp(
            pitch,
            -profile.maximumUpperBodyPitch,
            profile.maximumUpperBodyPitch);
        return true;
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
            ResetFirePointAimPivot();
            animator = nextAnimator;
            firePoint = null;
            firePointSourceParent = null;
            firePointAimPivot = null;
            currentWeight = 0f;
            currentPitch = 0f;
            missingBoneMapWarningIssued = false;
            ResolveAimBones();
        }

        ResolveFirePointAimPivot();

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

    void ResolveFirePointAimPivot()
    {
        Transform nextFirePoint = characterContext != null &&
                                  characterContext.WeaponSystem != null
            ? characterContext.WeaponSystem.FirePoint
            : null;
        if (nextFirePoint == null)
        {
            ResetFirePointAimPivot();
            firePoint = null;
            firePointSourceParent = null;
            firePointAimPivot = null;
            return;
        }

        if (nextFirePoint == firePoint &&
            firePointAimPivot != null &&
            nextFirePoint.parent == firePointAimPivot)
        {
            return;
        }

        ResetFirePointAimPivot();
        firePoint = nextFirePoint;

        Transform parent = firePoint.parent;
        if (parent == null || IsUnderAimBones(parent))
        {
            firePointSourceParent = null;
            firePointAimPivot = null;
            return;
        }

        if (parent.name == FirePointAimPivotName && parent.parent != null)
        {
            firePointAimPivot = parent;
            firePointSourceParent = parent.parent;
            return;
        }

        var pivotObject = new GameObject(FirePointAimPivotName);
        firePointAimPivot = pivotObject.transform;
        firePointAimPivot.SetParent(parent, false);
        firePointSourceParent = parent;
        firePoint.SetParent(firePointAimPivot, false);
    }

    bool IsUnderAimBones(Transform candidate)
    {
        return IsChildOf(candidate, spine) ||
               IsChildOf(candidate, chest) ||
               IsChildOf(candidate, upperChest);
    }

    static bool IsChildOf(Transform candidate, Transform ancestor)
    {
        return candidate != null &&
               ancestor != null &&
               (candidate == ancestor || candidate.IsChildOf(ancestor));
    }

    void ApplyFirePointPitch(Transform reference)
    {
        if (firePointAimPivot == null || firePointSourceParent == null)
            return;

        Transform originBone = chest != null
            ? chest
            : upperChest != null
                ? upperChest
                : spine;
        Vector3 pivot = originBone != null
            ? originBone.position
            : reference.position + Vector3.up;
        Quaternion pitchRotation = Quaternion.AngleAxis(
            -currentPitch * currentWeight,
            reference.right);

        firePointAimPivot.position =
            pivot +
            pitchRotation * (firePointSourceParent.position - pivot);
        firePointAimPivot.rotation =
            pitchRotation * firePointSourceParent.rotation;
    }

    void ResetFirePointAimPivot()
    {
        if (firePointAimPivot == null)
            return;

        firePointAimPivot.localPosition = Vector3.zero;
        firePointAimPivot.localRotation = Quaternion.identity;
        firePointAimPivot.localScale = Vector3.one;
    }

    void ResolveAimBones()
    {
        spine = null;
        chest = null;
        upperChest = null;
        if (animator == null)
            return;

        ThirdPersonAimBoneMap boneMap =
            animator.GetComponent<ThirdPersonAimBoneMap>();
        if (boneMap != null && boneMap.HasAnyBone)
        {
            spine = boneMap.Spine;
            chest = boneMap.Chest;
            upperChest = boneMap.UpperChest;
            return;
        }

        if (animator.isHuman)
        {
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            upperChest =
                animator.GetBoneTransform(HumanBodyBones.UpperChest);
            return;
        }

        if (!missingBoneMapWarningIssued)
        {
            Debug.LogWarning(
                $"[{nameof(ThirdPersonAimRigController)}] Generic Animator " +
                $"'{animator.name}' requires {nameof(ThirdPersonAimBoneMap)} " +
                "on the Animator GameObject. Visual upper-body aim is disabled.",
                animator);
            missingBoneMapWarningIssued = true;
        }
    }

    bool HasAimBones()
    {
        return spine != null || chest != null || upperChest != null;
    }

    static float GetActiveBoneWeight(Transform bone, float weight)
    {
        return bone != null ? Mathf.Max(0f, weight) : 0f;
    }

    static void ApplyBonePitch(
        Transform bone,
        float pitch,
        float weight,
        Transform reference)
    {
        if (bone == null || weight <= 0.001f)
            return;

        Quaternion pitchRotation = Quaternion.AngleAxis(
            -pitch * weight,
            reference.right);
        bone.rotation = pitchRotation * bone.rotation;
    }
}
