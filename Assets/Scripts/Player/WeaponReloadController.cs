using UnityEngine;

public enum WeaponReloadMode
{
    None,
    PerBullet,
    FullMagazine
}

public sealed class WeaponReloadController
{
    public float GetAnimationDuration(
        bool reloadPerBullet,
        int maxMagazine,
        int magazine,
        bool hasInfiniteReserveAmmo,
        int reserveAmmo,
        float startInsertDelay,
        float perBulletInsertTime,
        float endInsertDelay,
        float reloadTime)
    {
        if (!reloadPerBullet)
            return reloadTime;

        int missing = Mathf.Max(0, maxMagazine - magazine);
        int reloadable = hasInfiniteReserveAmmo ? missing : Mathf.Min(missing, Mathf.Max(0, reserveAmmo));
        return startInsertDelay + (reloadable * perBulletInsertTime) + endInsertDelay;
    }

    public WeaponReloadMode ResolveReloadMode(bool reloadPerBullet, bool magazineReload)
    {
        if (reloadPerBullet)
            return WeaponReloadMode.PerBullet;

        if (magazineReload)
            return WeaponReloadMode.FullMagazine;

        return WeaponReloadMode.None;
    }
}
