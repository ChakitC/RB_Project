using System;
using UnityEngine;
using Object = UnityEngine.Object;

public class PartySlotVisualPreview : MonoBehaviour
{
    [Header("Root Model")]
    [SerializeField] private Transform modelRoot;
    [SerializeField] private WeaponDatabase weaponDatabase;

    [Header("Weapon Preview")]
    [SerializeField] private string rightHandName = "Weapon.R";
    [SerializeField] private string leftHandName = "Weapon.L";

    [SerializeField] private Vector3 rightLocalPos;
    [SerializeField] private Vector3 rightLocalRotEuler;
    [SerializeField] private Vector3 rightLocalScale = Vector3.one;

    [SerializeField] private Vector3 leftLocalPos;
    [SerializeField] private Vector3 leftLocalRotEuler;
    [SerializeField] private Vector3 leftLocalScale = Vector3.one;

    [Header("Runtime")]
    [SerializeField] private Animator animator;

    private GameObject currentModel;
    private GameObject rightWeaponObj;
    private GameObject leftWeaponObj;
    private int currentSlot;
    private int partyIndex;

    public Animator Animator => animator;

    public void SetPartyContext(int saveSlot, int partyMemberIndex)
    {
        currentSlot = saveSlot;
        partyIndex = partyMemberIndex;
    }

    public void BuildCharacter(CharacterStats selected, Transform slotRoot)
    {
        ClearTemporarySelectObjects(slotRoot);

        if (modelRoot == null)
        {
            Debug.LogError("[PartySlotVisualPreview] modelRoot is null", this);
            return;
        }

        ClearModelPreview();
        animator = null;

        if (selected == null)
        {
            Debug.LogWarning("[PartySlotVisualPreview] selected is null -> skip model build", this);
            return;
        }

        if (selected.CharacterPrefabBasement == null)
        {
            Debug.LogError($"[PartySlotVisualPreview] CharacterPrefabBasement is null (selected: {selected.name})", this);
            return;
        }

        currentModel = Instantiate(selected.CharacterPrefabBasement, modelRoot, false);
        currentModel.name = $"{selected.name}_Preview";
        DisablePreviewEquipmentSaveParticipation(currentModel);

        var t = currentModel.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;

        animator = currentModel.GetComponentInChildren<Animator>(true);
        if (!animator)
            animator = currentModel.GetComponent<Animator>();

        if (!animator)
        {
            Debug.LogError("[PartySlotVisualPreview] new model has no Animator", currentModel);
            SafeDestroy(currentModel);
            currentModel = null;
            return;
        }

        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);
        ResetPickedState(animator);

        RefreshWeapon(selected);
    }

    public void RefreshWeapon(
        CharacterStats selected,
        string preferredEquippedInstanceId = null,
        PlayerInventory preferredInventory = null)
    {
        ClearWeaponPreview();

        if (!animator || selected == null)
            return;

        bool useRightHand = ShouldUseRightHand(selected);
        bool useLeftHand = ShouldUseLeftHand(selected);

        if (!useRightHand && !useLeftHand)
            return;

        if (!CharacterWeaponPreviewResolver.TryResolveSelectedCharacterWeapon(
                selected,
                currentSlot,
                partyIndex,
                weaponDatabase,
                preferredInventory,
                preferredEquippedInstanceId,
                out var weapon) ||
            weapon == null)
        {
            return;
        }

        if (useRightHand)
            rightWeaponObj = SpawnWeaponOnHand(weapon, rightHand: true);

        if (useLeftHand)
            leftWeaponObj = SpawnWeaponOnHand(weapon, rightHand: false);
    }

    public void ClearPreview()
    {
        ClearModelPreview();
    }

    public void ClearTemporarySelectObjects(Transform slotRoot)
    {
        ClearTemporarySelectObjectsUnder(slotRoot);

        if (modelRoot != null && modelRoot != slotRoot)
            ClearTemporarySelectObjectsUnder(modelRoot);
    }

    void ClearWeaponPreview()
    {
        if (rightWeaponObj)
            SafeDestroy(rightWeaponObj);

        if (leftWeaponObj)
            SafeDestroy(leftWeaponObj);

        rightWeaponObj = null;
        leftWeaponObj = null;
    }

    void ClearModelPreview()
    {
        ClearWeaponPreview();

        if (currentModel)
        {
            SafeDestroy(currentModel);
            currentModel = null;
        }

        if (modelRoot == null)
            return;

        for (int i = modelRoot.childCount - 1; i >= 0; i--)
        {
            var child = modelRoot.GetChild(i);
            if (!IsGeneratedPreviewRoot(child))
                continue;

            child.gameObject.SetActive(false);
            SafeDestroy(child.gameObject);
        }
    }

    bool IsGeneratedPreviewRoot(Transform child)
    {
        if (!child)
            return false;

        if (currentModel && child == currentModel.transform)
            return true;

        if (!child.name.EndsWith("_Preview", StringComparison.Ordinal))
            return false;

        return child.GetComponentInChildren<CharacterSelectable>(true) == null;
    }

    void ClearTemporarySelectObjectsUnder(Transform root)
    {
        if (!root)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (!IsTemporarySelectRoot(child))
                continue;

            child.gameObject.SetActive(false);
            SafeDestroy(child.gameObject);
        }
    }

    bool IsTemporarySelectRoot(Transform child)
    {
        if (!child)
            return false;

        if (currentModel && child == currentModel.transform)
            return false;

        if (child.name.EndsWith("_Preview", StringComparison.Ordinal))
            return false;

        if (child.GetComponentInChildren<CharacterSelectable>(true) != null)
            return true;

        if (child.GetComponentInChildren<CharacterDefHolder>(true) != null)
            return true;

        return child.name.IndexOf("Select", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void DisablePreviewEquipmentSaveParticipation(GameObject root)
    {
        if (!root)
            return;

        var equipments = root.GetComponentsInChildren<CharacterEquipment>(true);
        for (int i = 0; i < equipments.Length; i++)
            equipments[i].SetPlayerInventorySaveParticipation(false);
    }

    GameObject SpawnWeaponOnHand(GunConfig weapon, bool rightHand)
    {
        var hand = GetHandTransform(animator, rightHand);
        if (!hand)
        {
            Debug.LogWarning($"[PartySlotVisualPreview] {(rightHand ? "Right" : "Left")} hand not found for weapon preview.", this);
            return null;
        }

        var weaponObj = WeaponModelMountUtility.SpawnWeapon(
            weapon,
            hand,
            rightHand,
            CreateRightFallbackOffset(),
            CreateLeftFallbackOffset(),
            this);

        if (weaponObj)
            weaponObj.name = $"{weaponObj.name}_Preview";

        return weaponObj;
    }

    WeaponModelMountOffset CreateRightFallbackOffset()
    {
        return new WeaponModelMountOffset
        {
            localPosition = rightLocalPos,
            localRotationEuler = rightLocalRotEuler,
            localScale = rightLocalScale
        };
    }

    WeaponModelMountOffset CreateLeftFallbackOffset()
    {
        return new WeaponModelMountOffset
        {
            localPosition = leftLocalPos,
            localRotationEuler = leftLocalRotEuler,
            localScale = leftLocalScale
        };
    }

    static bool ShouldUseRightHand(CharacterStats stats)
    {
        if (!stats)
            return true;

        return stats.weaponHandMode == CharacterWeaponHandMode.RightHand
            || stats.weaponHandMode == CharacterWeaponHandMode.BothHands;
    }

    static bool ShouldUseLeftHand(CharacterStats stats)
    {
        if (!stats)
            return false;

        return stats.weaponHandMode == CharacterWeaponHandMode.LeftHand
            || stats.weaponHandMode == CharacterWeaponHandMode.BothHands;
    }

    Transform GetHandTransform(Animator anim, bool rightHand)
    {
        if (!anim)
            return null;

        var namedMount = FindChildByName(anim.transform, rightHand ? rightHandName : leftHandName);
        if (namedMount)
            return namedMount;

        namedMount = FindChildByName(anim.transform, rightHand ? "Weapon.R" : "Weapon.L");
        if (namedMount)
            return namedMount;

        namedMount = FindChildByName(anim.transform, rightHand ? "hand.r" : "hand.l");
        if (namedMount)
            return namedMount;

        if (anim.isHuman)
            return anim.GetBoneTransform(rightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);

        return null;
    }

    static Transform FindChildByName(Transform root, string targetName)
    {
        if (!root || string.IsNullOrWhiteSpace(targetName))
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildByName(root.GetChild(i), targetName);
            if (found)
                return found;
        }

        return null;
    }

    static void ResetPickedState(Animator targetAnimator)
    {
        if (!targetAnimator)
            return;

        foreach (var parameter in targetAnimator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Bool)
                continue;

            if (parameter.name != "IsPicked")
                continue;

            targetAnimator.SetBool(parameter.name, false);
            return;
        }
    }

    static void SafeDestroy(GameObject go)
    {
        if (go == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Object.DestroyImmediate(go);
        else
            Object.Destroy(go);
#else
        Object.Destroy(go);
#endif
    }
}
