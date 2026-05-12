using UnityEngine;
using Object = UnityEngine.Object;

public class CharacterDragWeaponPreview : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
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

    GameObject rightWeaponObj;
    GameObject leftWeaponObj;
    CharacterStats cachedSelected;
    GunConfig cachedWeapon;
    bool cachedUseRightHand;
    bool cachedUseLeftHand;

    public void SetAnimator(Animator targetAnimator)
    {
        animator = targetAnimator;
    }

    public void Build(CharacterStats selected, int currentSlot = 0, int partyIndex = -1)
    {
        if (selected == null)
        {
            Clear();
            return;
        }

        ResolveAnimator();
        if (!animator)
        {
            Clear();
            return;
        }

        bool useRightHand = ShouldUseRightHand(selected);
        bool useLeftHand = ShouldUseLeftHand(selected);
        if (!useRightHand && !useLeftHand)
        {
            Clear();
            return;
        }

        if (!CharacterWeaponPreviewResolver.TryResolveSelectedCharacterWeapon(
                selected,
                currentSlot,
                partyIndex,
                weaponDatabase,
                out var weapon) ||
            weapon == null)
        {
            Clear();
            return;
        }

        if (CanReuse(selected, weapon, useRightHand, useLeftHand))
        {
            RefreshWeaponObjects(weapon, useRightHand, useLeftHand);
            SetWeaponObjectsActive(true);
            return;
        }

        ClearWeaponObjects();
        cachedSelected = selected;
        cachedWeapon = weapon;
        cachedUseRightHand = useRightHand;
        cachedUseLeftHand = useLeftHand;

        if (useRightHand)
            rightWeaponObj = SpawnWeaponOnHand(weapon, rightHand: true);

        if (useLeftHand)
            leftWeaponObj = SpawnWeaponOnHand(weapon, rightHand: false);
    }

    public void BuildFromHolder(int currentSlot = 0, int partyIndex = -1)
    {
        var holder = GetComponentInChildren<CharacterDefHolder>(true);
        Build(holder != null ? holder.def : null, currentSlot, partyIndex);
    }

    public void Clear()
    {
        ClearWeaponObjects();
        cachedSelected = null;
        cachedWeapon = null;
        cachedUseRightHand = false;
        cachedUseLeftHand = false;
    }

    void ClearWeaponObjects()
    {
        if (rightWeaponObj)
            SafeDestroy(rightWeaponObj);

        if (leftWeaponObj)
            SafeDestroy(leftWeaponObj);

        rightWeaponObj = null;
        leftWeaponObj = null;
    }

    bool CanReuse(CharacterStats selected, GunConfig weapon, bool useRightHand, bool useLeftHand)
    {
        if (cachedSelected != selected || cachedWeapon != weapon)
            return false;

        if (cachedUseRightHand != useRightHand || cachedUseLeftHand != useLeftHand)
            return false;

        if (useRightHand && !rightWeaponObj)
            return false;

        if (useLeftHand && !leftWeaponObj)
            return false;

        return true;
    }

    void SetWeaponObjectsActive(bool active)
    {
        if (rightWeaponObj)
            rightWeaponObj.SetActive(active);

        if (leftWeaponObj)
            leftWeaponObj.SetActive(active);
    }

    void RefreshWeaponObjects(GunConfig weapon, bool useRightHand, bool useLeftHand)
    {
        if (useRightHand)
            RefreshWeaponObject(rightWeaponObj, weapon, rightHand: true);

        if (useLeftHand)
            RefreshWeaponObject(leftWeaponObj, weapon, rightHand: false);
    }

    void RefreshWeaponObject(GameObject weaponObj, GunConfig weapon, bool rightHand)
    {
        if (!weaponObj || !weapon)
            return;

        var hand = GetHandTransform(animator, rightHand);
        if (!hand)
            return;

        weaponObj.transform.SetParent(hand, false);
        WeaponModelMountUtility.ApplyOffset(
            weaponObj.transform,
            WeaponModelMountUtility.ResolveOffset(
                weapon,
                rightHand,
                CreateRightFallbackOffset(),
                CreateLeftFallbackOffset()));
    }

    void OnDestroy()
    {
        Clear();
    }

    void ResolveAnimator()
    {
        if (animator && animator.transform.IsChildOf(transform))
            return;

        animator = GetComponentInChildren<Animator>(true);
        if (!animator)
            animator = GetComponent<Animator>();
    }

    GameObject SpawnWeaponOnHand(GunConfig weapon, bool rightHand)
    {
        var hand = GetHandTransform(animator, rightHand);
        if (!hand)
        {
            Debug.LogWarning($"[CharacterDragWeaponPreview] {(rightHand ? "Right" : "Left")} hand not found for weapon preview.", this);
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
            weaponObj.name = $"{weaponObj.name}_DragPreview";

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
