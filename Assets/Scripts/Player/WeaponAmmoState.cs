using UnityEngine;

public sealed class WeaponAmmoState
{
    public int Magazine { get; private set; }
    public int ReserveAmmo { get; private set; }
    public int MaxMagazine { get; private set; }
    public int MaxReserveAmmo { get; private set; }
    public bool HasInfiniteReserveAmmo { get; private set; }

    public bool HasReserveAmmo => HasInfiniteReserveAmmo || ReserveAmmo > 0;
    public bool IsMagazineEmpty => Magazine <= 0;
    public bool IsOutOfAmmo => Magazine <= 0 && !HasReserveAmmo;
    public bool IsFreeAmmoActive => Time.unscaledTime < freeAmmoUntilUnscaledTime;
    public float FreeAmmoRemaining => Mathf.Max(0f, freeAmmoUntilUnscaledTime - Time.unscaledTime);

    float freeAmmoUntilUnscaledTime;

    public void Equip(
        GunConfig weapon,
        WeaponInstanceData instance,
        int maxMagazine,
        int maxReserveAmmo,
        bool infiniteReserveAmmo)
    {
        MaxMagazine = Mathf.Max(0, maxMagazine);
        MaxReserveAmmo = Mathf.Max(0, maxReserveAmmo);
        HasInfiniteReserveAmmo = infiniteReserveAmmo;

        Magazine = ResolveStartingMagazine(weapon, instance);
        ReserveAmmo = ResolveStartingReserveAmmo(weapon, instance);
        ClampMagazineToMax();
        ClampReserveAmmoToMax(resetInfiniteReserve: true);
        SyncInstance(instance);
    }

    public void ClearAmmo()
    {
        Magazine = 0;
        ReserveAmmo = 0;
        MaxMagazine = 0;
        MaxReserveAmmo = 0;
        HasInfiniteReserveAmmo = false;
    }

    public void ClearFreeAmmo()
    {
        freeAmmoUntilUnscaledTime = 0f;
    }

    public void SetCurrent(int magazine, int reserveAmmo)
    {
        Magazine = magazine;
        ReserveAmmo = reserveAmmo;
    }

    public void RefreshLimits(
        int maxMagazine,
        int maxReserveAmmo,
        bool infiniteReserveAmmo,
        bool clampMagazine,
        bool resetInfiniteReserve)
    {
        MaxMagazine = Mathf.Max(0, maxMagazine);
        MaxReserveAmmo = Mathf.Max(0, maxReserveAmmo);
        HasInfiniteReserveAmmo = infiniteReserveAmmo;

        if (clampMagazine)
            ClampMagazineToMax();

        ClampReserveAmmoToMax(resetInfiniteReserve);
    }

    public void SyncInstance(WeaponInstanceData instance)
    {
        if (instance == null)
            return;

        instance.currentMagazine = Mathf.Max(0, Magazine);
        instance.currentReserveAmmo = HasInfiniteReserveAmmo ? -1 : Mathf.Max(0, ReserveAmmo);
        instance.reserveAmmoInitialized = true;
    }

    public bool CanRestoreMagazine(int amount, bool fillToMax)
    {
        if (MaxMagazine <= 0)
            return false;

        if (fillToMax)
            return Magazine < MaxMagazine;

        return amount > 0 && Magazine < MaxMagazine;
    }

    public bool RestoreMagazine(int amount, bool fillToMax)
    {
        if (!CanRestoreMagazine(amount, fillToMax))
            return false;

        int previous = Magazine;
        int amountToRestore = fillToMax ? MaxMagazine - Magazine : Mathf.Max(0, amount);
        Magazine = Mathf.Clamp(Magazine + amountToRestore, 0, MaxMagazine);
        return Magazine > previous;
    }

    public bool CanRestoreReserveAmmo(int amount, bool fillToMax)
    {
        if (HasInfiniteReserveAmmo || MaxReserveAmmo <= 0)
            return false;

        if (fillToMax)
            return ReserveAmmo < MaxReserveAmmo;

        return amount > 0 && ReserveAmmo < MaxReserveAmmo;
    }

    public bool RestoreReserveAmmo(int amount, bool fillToMax)
    {
        if (!CanRestoreReserveAmmo(amount, fillToMax))
            return false;

        int previous = ReserveAmmo;
        int amountToRestore = fillToMax ? MaxReserveAmmo - ReserveAmmo : Mathf.Max(0, amount);
        ReserveAmmo = Mathf.Clamp(ReserveAmmo + amountToRestore, 0, MaxReserveAmmo);
        return ReserveAmmo > previous;
    }

    public bool ConsumeMagazineAmmo(int amount = 1)
    {
        int requested = Mathf.Max(0, amount);
        if (requested <= 0 || Magazine <= 0)
            return false;

        int consumed = Mathf.Min(requested, Magazine);
        Magazine -= consumed;
        return consumed > 0;
    }

    public int ConsumeReserveAmmo(int amount)
    {
        int requested = Mathf.Max(0, amount);
        if (requested <= 0)
            return 0;

        if (HasInfiniteReserveAmmo)
            return requested;

        int consumed = Mathf.Min(requested, Mathf.Max(0, ReserveAmmo));
        ReserveAmmo -= consumed;
        return consumed;
    }

    public void AddMagazineAmmo(int amount)
    {
        if (amount <= 0)
            return;

        Magazine = Mathf.Clamp(Magazine + amount, 0, MaxMagazine);
    }

    public void GrantFreeAmmo(float duration)
    {
        if (duration <= 0f)
            return;

        freeAmmoUntilUnscaledTime = Mathf.Max(
            freeAmmoUntilUnscaledTime,
            Time.unscaledTime + duration);
    }

    int ResolveStartingMagazine(GunConfig weapon, WeaponInstanceData instance)
    {
        if (!weapon)
            return 0;

        if (instance != null)
        {
            int defaultMagazine = WeaponInstanceFactory.ResolveDefaultMagazine(weapon);
            int desiredMagazine = instance.currentMagazine;
            if (desiredMagazine < 0)
                desiredMagazine = defaultMagazine;

            return Mathf.Clamp(desiredMagazine, 0, Mathf.Max(weapon.maxMagazine, defaultMagazine));
        }

        return WeaponInstanceFactory.ResolveDefaultMagazine(weapon);
    }

    int ResolveStartingReserveAmmo(GunConfig weapon, WeaponInstanceData instance)
    {
        if (!WeaponInstanceFactory.UsesFiniteReserveAmmo(weapon))
            return 0;

        if (instance != null)
        {
            int defaultReserveAmmo = WeaponInstanceFactory.ResolveDefaultReserveAmmo(weapon, MaxMagazine);
            int desiredReserveAmmo = instance.currentReserveAmmo;
            if (!instance.reserveAmmoInitialized || desiredReserveAmmo < 0)
                desiredReserveAmmo = defaultReserveAmmo;

            return Mathf.Clamp(desiredReserveAmmo, 0, MaxReserveAmmo);
        }

        return Mathf.Max(0, WeaponInstanceFactory.ResolveDefaultReserveAmmo(weapon, MaxMagazine));
    }

    void ClampMagazineToMax()
    {
        Magazine = Mathf.Clamp(Magazine, 0, MaxMagazine);
    }

    void ClampReserveAmmoToMax(bool resetInfiniteReserve)
    {
        if (HasInfiniteReserveAmmo)
        {
            if (resetInfiniteReserve)
                ReserveAmmo = 0;

            return;
        }

        ReserveAmmo = Mathf.Clamp(ReserveAmmo, 0, MaxReserveAmmo);
    }
}
