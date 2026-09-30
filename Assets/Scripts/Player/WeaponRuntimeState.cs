using System;

public readonly struct WeaponRuntimeState : IEquatable<WeaponRuntimeState>
{
    public readonly GunConfig Weapon;
    public readonly bool Reloading, FiringHeld, FiringActivity, CanInterruptReload;
    public readonly FiringMode FiringMode;
    public readonly int Magazine, ReserveAmmo;
    public WeaponRuntimeState(GunConfig weapon, bool reloading, bool firingHeld, bool firingActivity, int magazine, int reserveAmmo, bool canInterruptReload, FiringMode firingMode)
    {
        Weapon = weapon;
        Reloading = reloading;
        FiringHeld = firingHeld;
        FiringActivity = firingActivity;
        Magazine = magazine;
        ReserveAmmo = reserveAmmo;
        CanInterruptReload = canInterruptReload;
        FiringMode = firingMode;
    }
    public bool Equals(WeaponRuntimeState other) => Weapon == other.Weapon && Reloading == other.Reloading &&
        FiringHeld == other.FiringHeld && FiringActivity == other.FiringActivity &&
        Magazine == other.Magazine && ReserveAmmo == other.ReserveAmmo &&
        CanInterruptReload == other.CanInterruptReload && FiringMode == other.FiringMode;
    public override bool Equals(object obj) => obj is WeaponRuntimeState other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Weapon, Reloading, FiringHeld, FiringActivity, Magazine, ReserveAmmo, CanInterruptReload, FiringMode);
}
