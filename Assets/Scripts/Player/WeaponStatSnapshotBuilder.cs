using UnityEngine;

public static class WeaponStatSnapshotBuilder
{
    const float BaseCritMultiplier = 1f;

    public static WeaponStatSnapshot Build(GunConfig weapon, StatsHub statsHub, CharacteContext ctx)
    {
        if (!weapon)
            return default;

        int maxMagazine = statsHub ? statsHub.GetMaxMagazine(weapon) : weapon.maxMagazine;

        bool infiniteReserveAmmo = weapon.infiniteReserveAmmo || (ctx != null && ctx.ForceInfiniteReserveAmmo);
        int maxReserveAmmo = infiniteReserveAmmo
            ? 0
            : statsHub
                ? statsHub.GetMaxReserveAmmo(weapon)
                : WeaponInstanceFactory.ResolveMaxReserveAmmo(weapon, maxMagazine);

        return new WeaponStatSnapshot(
            weapon,
            weapon.WeaponType,
            weapon.firingModes,
            ResolveDamage(weapon, statsHub, ctx),
            ResolveFireInterval(weapon, statsHub),
            maxMagazine,
            maxReserveAmmo,
            infiniteReserveAmmo,
            ResolveReloadTime(weapon, statsHub),
            ResolveCritRate(weapon, statsHub, ctx),
            ResolveCritMultiplier(weapon, statsHub, ctx),
            ResolveStability(weapon, statsHub),
            ResolveBulletSpeed(weapon, statsHub),
            Mathf.Max(0f, weapon.staggerPower),
            weapon.magazineReload,
            weapon.autoloader,
            weapon.reloadPerBullet,
            ResolveReloadDuration(weapon.startInsertDelay, statsHub, weapon),
            ResolveReloadDuration(weapon.perBulletInsertTime, statsHub, weapon),
            ResolveReloadDuration(weapon.endInsertDelay, statsHub, weapon),
            weapon.shootInterruptsReload,
            weapon.burstCount,
            weapon.burstInterval);
    }

    static float ResolveDamage(GunConfig weapon, StatsHub statsHub, CharacteContext ctx)
    {
        return statsHub ? statsHub.GetDamage(weapon) : weapon.damage + (ctx ? ctx.baseDamage : 0f);
    }

    static float ResolveFireInterval(GunConfig weapon, StatsHub statsHub)
    {
        return statsHub ? statsHub.GetFireInterval(weapon) : weapon.fireRate;
    }

    static float ResolveReloadTime(GunConfig weapon, StatsHub statsHub)
    {
        return statsHub ? statsHub.GetReloadTime(weapon) : weapon.reloadTime;
    }

    static float ResolveReloadDuration(float duration, StatsHub statsHub, GunConfig weapon)
    {
        return statsHub ? statsHub.GetReloadDuration(duration, weapon) : Mathf.Max(0f, duration);
    }

    static float ResolveCritRate(GunConfig weapon, StatsHub statsHub, CharacteContext ctx)
    {
        return statsHub ? statsHub.GetCritRatePercent(weapon) : weapon.critRate + (ctx ? ctx.basecritRate : 0f);
    }

    static float ResolveCritMultiplier(GunConfig weapon, StatsHub statsHub, CharacteContext ctx)
    {
        if (statsHub)
            return statsHub.GetCritMultiplier(weapon);

        float weaponMultiplier = Mathf.Max(BaseCritMultiplier, weapon.critMultiplier);
        float characterBonus = ctx ? Mathf.Max(BaseCritMultiplier, ctx.basecritMultiplier) - BaseCritMultiplier : 0f;
        return weaponMultiplier + characterBonus;
    }

    static float ResolveStability(GunConfig weapon, StatsHub statsHub)
    {
        return statsHub ? statsHub.GetStability(weapon) : weapon.stability;
    }

    static float ResolveBulletSpeed(GunConfig weapon, StatsHub statsHub)
    {
        return statsHub ? statsHub.GetBulletSpeed(weapon) : weapon.BulletSpeed;
    }
}
