using System;
using System.Collections.Generic;
using UnityEngine;

public class InventorySystem
{
    readonly List<InventorySlotData> slots;

    public Func<int, InventorySlotData, bool> PlacementValidator { get; set; }
    public IReadOnlyList<InventorySlotData> Slots => slots;

    public event Action<int> OnSlotChanged;
    public event Action OnInventoryReset;

    public InventorySystem(List<InventorySlotData> backingSlots, int slotCount)
    {
        slots = backingSlots ?? new List<InventorySlotData>();
        EnsureSlotCount(slotCount);
    }

    public void EnsureSlotCount(int slotCount)
    {
        if (slotCount < 0)
            slotCount = 0;

        while (slots.Count < slotCount)
            slots.Add(new InventorySlotData());

        for (int i = 0; i < slots.Count; i++)
            EnsureSlotObject(i);
    }

    public void ClearAll()
    {
        for (int i = 0; i < slots.Count; i++)
            EnsureSlotObject(i).Clear();

        NotifyInventoryReset();
    }

    public InventorySlotData GetSlot(int index)
    {
        return IsValidSlotIndex(index) ? EnsureSlotObject(index) : null;
    }

    public int FindFirstEmptySlotIndex(ItemDefinition item = null)
    {
        InventorySlotData probe = null;

        if (item != null)
        {
            probe = new InventorySlotData();
            probe.SetItem(item, 1);
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty)
                continue;

            if (probe != null && !CanPlaceItem(i, probe))
                continue;

            return i;
        }

        return -1;
    }

    public bool AddItem(ItemDefinition item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return false;

        if (item.stackable)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.IsEmpty || slot.HasUniqueInstance || slot.item != item || slot.quantity >= slot.MaxStack)
                    continue;

                int canAdd = slot.MaxStack - slot.quantity;
                int toAdd = Mathf.Min(canAdd, amount);
                slot.quantity += toAdd;
                amount -= toAdd;
                NotifySlotChanged(i);

                if (amount <= 0)
                    return true;
            }
        }

        while (amount > 0)
        {
            int emptyIndex = FindFirstEmptySlotIndex(item);
            if (emptyIndex == -1)
                return false;

            int toAdd = item.stackable ? Mathf.Min(Mathf.Max(1, item.maxStack), amount) : 1;
            slots[emptyIndex].SetItem(item, toAdd);
            amount -= toAdd;
            NotifySlotChanged(emptyIndex);
        }

        return true;
    }

    public bool CanAddItem(ItemDefinition item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return false;

        if (item.stackable)
            return CanAddStackableItem(item, amount);

        var payload = new InventorySlotData();
        payload.SetItem(item, 1);
        return HasEmptySlotsForPayload(payload, amount);
    }

    public bool AddWeaponInstance(GunConfig baseWeapon, WeaponInstanceData instance)
    {
        if (!baseWeapon || instance == null)
            return false;

        var payload = new InventorySlotData();
        payload.SetWeaponInstance(baseWeapon, instance);

        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty)
                continue;

            if (!CanPlaceItem(i, payload))
                continue;

            slots[i].CopyFrom(payload);
            NotifySlotChanged(i);
            return true;
        }

        return false;
    }

    public bool AddAccessoryInstance(AccessoryDefinition accessory, AccessoryInstanceData instance)
    {
        if (!accessory || instance == null)
            return false;

        var payload = new InventorySlotData();
        payload.SetAccessoryInstance(accessory, instance);

        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty)
                continue;

            if (!CanPlaceItem(i, payload))
                continue;

            slots[i].CopyFrom(payload);
            NotifySlotChanged(i);
            return true;
        }

        return false;
    }

    public bool CanAddWeaponInstances(GunConfig baseWeapon, int amount = 1)
    {
        if (!baseWeapon || amount <= 0)
            return false;

        var payload = new InventorySlotData();
        payload.SetWeaponInstance(baseWeapon, new WeaponInstanceData
        {
            instanceId = "__shop_probe__",
            baseWeaponId = WeaponInstanceFactory.ResolveBaseWeaponId(baseWeapon)
        });

        return HasEmptySlotsForPayload(payload, amount);
    }

    public bool CanAddAccessoryInstances(AccessoryDefinition accessory, int amount = 1)
    {
        if (!accessory || amount <= 0)
            return false;

        var payload = new InventorySlotData();
        payload.SetAccessoryInstance(accessory, new AccessoryInstanceData
        {
            instanceId = "__accessory_probe__",
            accessoryId = AccessoryInstanceFactory.ResolveAccessoryId(accessory)
        });

        return HasEmptySlotsForPayload(payload, amount);
    }

    public bool HasItem(ItemDefinition item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return false;

        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.IsEmpty || slot.HasUniqueInstance || slot.item != item)
                continue;

            total += slot.quantity;
            if (total >= amount)
                return true;
        }

        return false;
    }

    public bool RemoveItem(ItemDefinition item, int amount = 1)
    {
        if (!HasItem(item, amount))
            return false;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.IsEmpty || slot.HasUniqueInstance || slot.item != item)
                continue;

            int toRemove = Mathf.Min(slot.quantity, amount);
            slot.quantity -= toRemove;
            amount -= toRemove;

            if (slot.quantity <= 0)
                slot.Clear();

            NotifySlotChanged(i);

            if (amount <= 0)
                return true;
        }

        return true;
    }

    public bool MoveOrSwap(int fromIndex, int toIndex)
    {
        if (!IsValidSlotIndex(fromIndex) || !IsValidSlotIndex(toIndex) || fromIndex == toIndex)
            return false;

        var source = slots[fromIndex];
        var target = slots[toIndex];

        if (source.IsEmpty)
            return false;

        if (target.IsEmpty)
        {
            if (!CanPlaceItem(toIndex, source))
                return false;

            target.CopyFrom(source);
            source.Clear();
            NotifySlotsChanged(fromIndex, toIndex);
            return true;
        }

        if (source.CanStackWith(target))
            return TryMerge(fromIndex, toIndex);

        if (!CanPlaceItem(toIndex, source) || !CanPlaceItem(fromIndex, target))
            return false;

        var sourceCopy = source.DeepClone();
        source.CopyFrom(target);
        target.CopyFrom(sourceCopy);
        NotifySlotsChanged(fromIndex, toIndex);
        return true;
    }

    public bool TryMerge(int fromIndex, int toIndex)
    {
        if (!IsValidSlotIndex(fromIndex) || !IsValidSlotIndex(toIndex) || fromIndex == toIndex)
            return false;

        var source = slots[fromIndex];
        var target = slots[toIndex];

        if (!source.CanStackWith(target) || !CanPlaceItem(toIndex, source))
            return false;

        int spaceLeft = target.MaxStack - target.quantity;
        if (spaceLeft <= 0)
            return false;

        int movedAmount = Mathf.Min(spaceLeft, source.quantity);
        target.quantity += movedAmount;
        source.quantity -= movedAmount;

        if (source.quantity <= 0)
            source.Clear();

        NotifySlotsChanged(fromIndex, toIndex);
        return true;
    }

    public bool SplitStack(int fromIndex, int toIndex, int amount)
    {
        if (!IsValidSlotIndex(fromIndex) || !IsValidSlotIndex(toIndex) || fromIndex == toIndex || amount <= 0)
            return false;

        var source = slots[fromIndex];
        var target = slots[toIndex];

        if (source.IsEmpty || source.HasUniqueInstance || !source.IsStackable)
            return false;

        if (!target.IsEmpty)
            return false;

        if (source.quantity <= amount)
            return false;

        if (!CanPlaceItem(toIndex, source))
            return false;

        target.SetItem(source.item, amount);
        source.quantity -= amount;
        NotifySlotsChanged(fromIndex, toIndex);
        return true;
    }

    public bool CanPlaceItem(int slotIndex, ItemDefinition item)
    {
        if (item == null)
            return false;

        var probe = new InventorySlotData();
        probe.SetItem(item, 1);
        return CanPlaceItem(slotIndex, probe);
    }

    public bool CanPlaceItem(int slotIndex, InventorySlotData slotData)
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        if (slotData == null || slotData.IsEmpty)
            return true;

        return PlacementValidator?.Invoke(slotIndex, slotData) ?? true;
    }

    public void NotifyInventoryReset()
    {
        OnInventoryReset?.Invoke();
    }

    public void NotifySlotChanged(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return;

        OnSlotChanged?.Invoke(slotIndex);
    }

    bool IsValidSlotIndex(int index)
    {
        return index >= 0 && index < slots.Count;
    }

    InventorySlotData EnsureSlotObject(int index)
    {
        if (slots[index] == null)
            slots[index] = new InventorySlotData();

        return slots[index];
    }

    void NotifySlotsChanged(int firstIndex, int secondIndex)
    {
        NotifySlotChanged(firstIndex);

        if (secondIndex != firstIndex)
            NotifySlotChanged(secondIndex);
    }

    bool CanAddStackableItem(ItemDefinition item, int amount)
    {
        int remaining = amount;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.IsEmpty || slot.HasUniqueInstance || slot.item != item || slot.quantity >= slot.MaxStack)
                continue;

            remaining -= Mathf.Min(slot.MaxStack - slot.quantity, remaining);
            if (remaining <= 0)
                return true;
        }

        var payload = new InventorySlotData();
        payload.SetItem(item, 1);
        int stackSize = Mathf.Max(1, item.maxStack);

        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty || !CanPlaceItem(i, payload))
                continue;

            remaining -= Mathf.Min(stackSize, remaining);
            if (remaining <= 0)
                return true;
        }

        return false;
    }

    bool HasEmptySlotsForPayload(InventorySlotData payload, int amount)
    {
        if (payload == null || payload.IsEmpty || amount <= 0)
            return false;

        int remaining = amount;

        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty || !CanPlaceItem(i, payload))
                continue;

            remaining--;
            if (remaining <= 0)
                return true;
        }

        return false;
    }
}
