public enum WeaponFireStartAction
{
    None,
    Semi,
    Auto,
    Burst
}

public sealed class WeaponFireController
{
    public bool CanStartFiring(bool hasWeapon, bool canShootNow)
    {
        return hasWeapon && canShootNow;
    }

    public bool ShouldCancelReload(bool isReloading, bool shootInterruptsReload)
    {
        return isReloading && shootInterruptsReload;
    }

    public WeaponFireStartAction ResolveStartAction(
        FiringMode firingMode,
        bool isReloading,
        bool isBursting,
        bool hasBurstRoutine)
    {
        switch (firingMode)
        {
            case FiringMode.Burst:
                return isReloading || isBursting || hasBurstRoutine
                    ? WeaponFireStartAction.None
                    : WeaponFireStartAction.Burst;

            case FiringMode.Semi:
                return WeaponFireStartAction.Semi;

            case FiringMode.Auto:
                return WeaponFireStartAction.Auto;

            default:
                return WeaponFireStartAction.None;
        }
    }
}
