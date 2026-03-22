using System;
using UnityEngine;

[Serializable]
public class InventorySlotData
{
    public ItemDefinition item;
    public int quantity;
    public WeaponInstanceData weaponInstance;

    public bool HasWeaponInstance => weaponInstance != null;
    public bool IsEmpty => item == null || quantity <= 0;
    public bool IsStackable => !HasWeaponInstance && item != null && item.stackable;
    public int MaxStack => HasWeaponInstance ? 1 : item != null ? Mathf.Max(1, item.maxStack) : 0;

    public void SetItem(ItemDefinition value, int stackAmount)
    {
        if (value == null || stackAmount <= 0)
        {
            Clear();
            return;
        }

        item = value;
        weaponInstance = null;
        quantity = value.stackable ? Mathf.Clamp(stackAmount, 1, Mathf.Max(1, value.maxStack)) : 1;
    }

    public void SetWeaponInstance(GunConfig baseWeapon, WeaponInstanceData instance)
    {
        if (!baseWeapon || instance == null)
        {
            Clear();
            return;
        }

        item = baseWeapon;
        quantity = 1;
        weaponInstance = instance.DeepClone();
    }

    public void CopyFrom(InventorySlotData other)
    {
        if (other == null || other.IsEmpty)
        {
            Clear();
            return;
        }

        if (other.HasWeaponInstance)
        {
            SetWeaponInstance(other.item as GunConfig, other.weaponInstance);
            return;
        }

        SetItem(other.item, other.quantity);
    }

    public InventorySlotData DeepClone()
    {
        var clone = new InventorySlotData();
        clone.CopyFrom(this);
        return clone;
    }

    public bool CanStackWith(InventorySlotData other)
    {
        return other != null &&
               !IsEmpty &&
               !other.IsEmpty &&
               !HasWeaponInstance &&
               !other.HasWeaponInstance &&
               item == other.item &&
               item != null &&
               item.stackable;
    }

    public void Clear()
    {
        item = null;
        quantity = 0;
        weaponInstance = null;
    }
}
