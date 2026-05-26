using System;
using UnityEngine;

[Serializable]
public class InventorySlotData
{
    public ItemDefinition item;
    public int quantity;
    public WeaponInstanceData weaponInstance;
    public AccessoryInstanceData accessoryInstance;

    public bool HasWeaponInstance => weaponInstance != null;
    public bool HasAccessoryInstance => accessoryInstance != null;
    public bool HasUniqueInstance => HasWeaponInstance || HasAccessoryInstance;
    public bool IsEmpty => item == null || quantity <= 0;
    public bool IsStackable => !HasUniqueInstance && item != null && item.stackable;
    public int MaxStack => HasUniqueInstance ? 1 : item != null ? Mathf.Max(1, item.maxStack) : 0;

    public void SetItem(ItemDefinition value, int stackAmount)
    {
        if (value == null || stackAmount <= 0)
        {
            Clear();
            return;
        }

        item = value;
        weaponInstance = null;
        accessoryInstance = null;
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
        accessoryInstance = null;
    }

    public void SetAccessoryInstance(AccessoryDefinition accessory, AccessoryInstanceData instance)
    {
        if (!accessory || instance == null)
        {
            Clear();
            return;
        }

        item = accessory;
        quantity = 1;
        weaponInstance = null;
        accessoryInstance = instance.DeepClone();
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

        if (other.HasAccessoryInstance)
        {
            SetAccessoryInstance(other.item as AccessoryDefinition, other.accessoryInstance);
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
               !HasUniqueInstance &&
               !other.HasUniqueInstance &&
               item == other.item &&
               item != null &&
               item.stackable;
    }

    public void Clear()
    {
        item = null;
        quantity = 0;
        weaponInstance = null;
        accessoryInstance = null;
    }
}
