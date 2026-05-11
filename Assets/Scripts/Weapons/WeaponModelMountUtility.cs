using System;
using UnityEngine;
using Object = UnityEngine.Object;

[Serializable]
public struct WeaponModelMountOffset
{
    public Vector3 localPosition;
    public Vector3 localRotationEuler;
    public Vector3 localScale;

    public static WeaponModelMountOffset Identity => new WeaponModelMountOffset
    {
        localPosition = Vector3.zero,
        localRotationEuler = Vector3.zero,
        localScale = Vector3.one
    };

    public Vector3 SafeLocalScale => localScale == Vector3.zero ? Vector3.one : localScale;
}

public static class WeaponModelMountUtility
{
    public static GameObject SpawnWeapon(
        GunConfig weapon,
        Transform hand,
        bool rightHand,
        WeaponModelMountOffset fallbackRight,
        WeaponModelMountOffset fallbackLeft,
        Object context = null)
    {
        if (!weapon || !hand)
            return null;

        var weaponPrefab = weapon.WeaponPrefab;
        if (!weaponPrefab)
        {
            Debug.LogWarning($"[WeaponModelMountUtility] WeaponPrefab missing: {weapon.name}", context);
            return null;
        }

        var weaponObj = Object.Instantiate(weaponPrefab, hand, false);
        weaponObj.name = $"{weaponPrefab.name}_Mounted";
        ApplyOffset(weaponObj.transform, ResolveOffset(weapon, rightHand, fallbackRight, fallbackLeft));
        return weaponObj;
    }

    public static WeaponModelMountOffset ResolveOffset(
        GunConfig weapon,
        bool rightHand,
        WeaponModelMountOffset fallbackRight,
        WeaponModelMountOffset fallbackLeft)
    {
        if (weapon && weapon.overrideWeaponModelMountOffset)
            return rightHand ? weapon.rightHandWeaponModelMount : weapon.leftHandWeaponModelMount;

        return rightHand ? fallbackRight : fallbackLeft;
    }

    static void ApplyOffset(Transform target, WeaponModelMountOffset offset)
    {
        if (!target)
            return;

        target.localPosition = offset.localPosition;
        target.localRotation = Quaternion.Euler(offset.localRotationEuler);
        target.localScale = offset.SafeLocalScale;
    }
}
