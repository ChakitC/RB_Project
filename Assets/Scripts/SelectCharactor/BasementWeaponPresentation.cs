using UnityEngine;

/// <summary>Owns mounted preview objects; equipment selection stays with the caller.</summary>
[DisallowMultipleComponent]
public sealed class BasementWeaponPresentation : MonoBehaviour
{
    [SerializeField] private string rightHandName = "Weapon.R";
    [SerializeField] private string leftHandName = "Weapon.L";
    [SerializeField] private WeaponModelMountOffset rightOffset = WeaponModelMountOffset.Identity;
    [SerializeField] private WeaponModelMountOffset leftOffset = WeaponModelMountOffset.Identity;

    Animator boundAnimator;
    GunConfig currentWeapon;
    GameObject rightWeapon;
    GameObject leftWeapon;

    public void Show(Animator animator, GunConfig weapon, CharacterWeaponHandMode hands)
    {
        if (!animator || !weapon)
        {
            Clear();
            return;
        }

        if (boundAnimator != animator || currentWeapon != weapon)
            Clear();
        boundAnimator = animator;
        currentWeapon = weapon;
        UpdateHand(ref rightWeapon, hands == CharacterWeaponHandMode.RightHand || hands == CharacterWeaponHandMode.BothHands, true);
        UpdateHand(ref leftWeapon, hands == CharacterWeaponHandMode.LeftHand || hands == CharacterWeaponHandMode.BothHands, false);
    }

    public void Clear()
    {
        DestroyWeapon(ref rightWeapon);
        DestroyWeapon(ref leftWeapon);
        boundAnimator = null;
        currentWeapon = null;
    }

    void UpdateHand(ref GameObject instance, bool useHand, bool rightHand)
    {
        if (!useHand)
        {
            DestroyWeapon(ref instance);
            return;
        }

        var hand = FindHand(rightHand);
        if (!hand)
        {
            DestroyWeapon(ref instance);
            Debug.LogWarning($"[Basement Weapon] {(rightHand ? "Right" : "Left")} hand not found for weapon preview.", this);
            return;
        }

        if (!instance)
            instance = WeaponModelMountUtility.SpawnWeapon(currentWeapon, hand, rightHand, rightOffset, leftOffset, this);
        else
        {
            instance.transform.SetParent(hand, false);
            WeaponModelMountUtility.ApplyOffset(instance.transform,
                WeaponModelMountUtility.ResolveOffset(currentWeapon, rightHand, rightOffset, leftOffset));
            instance.SetActive(true);
        }
    }

    Transform FindHand(bool rightHand)
    {
        var root = boundAnimator.transform;
        var hand = FindChild(root, rightHand ? rightHandName : leftHandName);
        if (!hand) hand = FindChild(root, rightHand ? "Weapon.R" : "Weapon.L");
        if (!hand) hand = FindChild(root, rightHand ? "hand.r" : "hand.l");
        if (!hand && boundAnimator.isHuman)
            hand = boundAnimator.GetBoneTransform(rightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        return hand;
    }

    static Transform FindChild(Transform root, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChild(root.GetChild(i), name);
            if (found) return found;
        }
        return null;
    }

    static void DestroyWeapon(ref GameObject instance)
    {
        if (instance)
        {
            instance.SetActive(false);
            if (Application.isPlaying) Destroy(instance);
            else DestroyImmediate(instance);
        }
        instance = null;
    }
}
