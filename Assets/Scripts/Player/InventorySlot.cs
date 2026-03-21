using System;
using UnityEngine;

[Serializable]
public class InventorySlot
{
    public ItemDefinition item;
    public int amount;
    public WeaponInstanceData weaponInstance;

    public bool HasWeaponInstance => weaponInstance != null;
    public bool IsEmpty => !HasWeaponInstance && (item == null || amount <= 0);

    public void SetItem(ItemDefinition value, int stackAmount)
    {
        item = value;
        amount = stackAmount;
        weaponInstance = null;
    }

    public void SetWeaponInstance(ItemDefinition baseWeapon, WeaponInstanceData instance)
    {
        item = baseWeapon;
        amount = instance != null ? 1 : 0;
        weaponInstance = instance;
    }

    public void Clear()
    {
        item = null;
        amount = 0;
        weaponInstance = null;
    }
}
