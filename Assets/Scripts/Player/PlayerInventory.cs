using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour, IGameSaveAble, ISaveOrder
{
    [Header("Settings")]
    public int slotCount = 0;

    [Header("Runtime")]
    public List<InventorySlot> slots = new();

    [Header("Refs")]
    public ItemDatabase itemDatabase;
    [SerializeField] private WeaponDatabase weaponDatabase;
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private WeaponSystem weaponSystem;

    [Header("Currency")]
    [SerializeField] private int gold = 0;
    public int Gold => gold;

    [Header("Weapon Instance")]
    [SerializeField] private string equippedWeaponInstanceId;

    public int LoadOrder => 0;
    public string EquippedWeaponInstanceId => equippedWeaponInstanceId;

    public event Action<int> OnGoldChanged;

    void Awake()
    {
        if (!ctx) TryGetComponent(out ctx);
        if (!weaponSystem) TryGetComponent(out weaponSystem);
        EnsureSlotCount();
    }

    void Start()
    {
        if (SaveManager.Instance == null)
        {
            Debug.Log("Save Manager Missing");
            EnsureDefaultWeaponInstance();
            ApplyEquippedWeaponIfPossible();
            ForceRefreshGoldUI();
            return;
        }

        SaveManager.Instance.Load();
        EnsureDefaultWeaponInstance();
        ApplyEquippedWeaponIfPossible();
        ForceRefreshGoldUI();
    }

    public void AddGold(int amount)
    {
        if (amount <= 0)
            return;

        gold += amount;
        OnGoldChanged?.Invoke(gold);
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0 || gold < amount)
            return false;

        gold -= amount;
        OnGoldChanged?.Invoke(gold);
        return true;
    }

    public void ForceRefreshGoldUI()
    {
        OnGoldChanged?.Invoke(gold);
    }

    public bool AddItem(ItemDefinition item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return false;

        if (itemDatabase != null && itemDatabase.goldItem != null && item == itemDatabase.goldItem)
        {
            AddGold(amount);
            return true;
        }

        if (item is GunConfig gun && !item.stackable)
        {
            bool allAdded = true;
            for (int i = 0; i < amount; i++)
            {
                var instance = WeaponInstanceFactory.CreatePlainInstance(gun);
                if (!AddWeaponInstance(instance))
                {
                    allAdded = false;
                    break;
                }
            }

            return allAdded;
        }

        if (item.stackable)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.HasWeaponInstance)
                    continue;

                if (slot.item == item && slot.amount < item.maxStack)
                {
                    int canAdd = item.maxStack - slot.amount;
                    int toAdd = Mathf.Min(canAdd, amount);

                    slot.amount += toAdd;
                    amount -= toAdd;

                    if (amount <= 0)
                        return true;
                }
            }
        }

        while (amount > 0)
        {
            int emptyIndex = FindEmptySlotIndex();
            if (emptyIndex == -1)
            {
                Debug.Log("Inventory full!");
                return false;
            }

            var emptySlot = slots[emptyIndex];

            if (item.stackable)
            {
                int toAdd = Mathf.Min(item.maxStack, amount);
                emptySlot.SetItem(item, toAdd);
                amount -= toAdd;
            }
            else
            {
                emptySlot.SetItem(item, 1);
                amount -= 1;
            }
        }

        return true;
    }

    public bool AddWeaponInstance(WeaponInstanceData weaponInstance)
    {
        if (weaponInstance == null)
            return false;

        int emptyIndex = FindEmptySlotIndex();
        if (emptyIndex == -1)
        {
            Debug.Log("Inventory full!");
            return false;
        }

        var instanceCopy = weaponInstance.DeepClone();
        var weaponDef = ResolveWeaponDefinition(instanceCopy.baseWeaponId);
        slots[emptyIndex].SetWeaponInstance(weaponDef, instanceCopy);

        if (string.IsNullOrWhiteSpace(equippedWeaponInstanceId))
            equippedWeaponInstanceId = instanceCopy.instanceId;

        return true;
    }

    public bool RemoveWeaponInstance(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || !slot.HasWeaponInstance || slot.weaponInstance == null)
                continue;

            if (!string.Equals(slot.weaponInstance.instanceId, instanceId, StringComparison.Ordinal))
                continue;

            slot.Clear();

            if (string.Equals(equippedWeaponInstanceId, instanceId, StringComparison.Ordinal))
            {
                equippedWeaponInstanceId = null;
                TryAssignFirstWeaponInstance();
                ApplyEquippedWeaponIfPossible();
            }

            return true;
        }

        return false;
    }

    public bool EquipWeaponInstance(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        if (GetWeaponInstance(instanceId) == null)
            return false;

        equippedWeaponInstanceId = instanceId;
        return ApplyEquippedWeaponIfPossible();
    }

    public WeaponInstanceData GetWeaponInstance(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return null;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || !slot.HasWeaponInstance || slot.weaponInstance == null)
                continue;

            if (string.Equals(slot.weaponInstance.instanceId, instanceId, StringComparison.Ordinal))
                return slot.weaponInstance;
        }

        return null;
    }

    public bool HasItem(ItemDefinition item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return false;

        int total = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.HasWeaponInstance || slot.item != item)
                continue;

            total += slot.amount;
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
            if (slot == null || slot.HasWeaponInstance || slot.item != item)
                continue;

            int toRemove = Mathf.Min(slot.amount, amount);
            slot.amount -= toRemove;
            amount -= toRemove;

            if (slot.amount <= 0)
                slot.Clear();

            if (amount <= 0)
                return true;
        }

        return true;
    }

    public PlayerInventoryData ToData()
    {
        var data = new PlayerInventoryData
        {
            gold = gold
        };

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.IsEmpty)
            {
                data.slots.Add(new InventorySlotData());
                continue;
            }

            if (slot.HasWeaponInstance && slot.weaponInstance != null)
            {
                data.slots.Add(new InventorySlotData
                {
                    amount = 1,
                    weaponInstance = slot.weaponInstance.DeepClone()
                });
                continue;
            }

            data.slots.Add(new InventorySlotData
            {
                itemId = slot.item != null ? slot.item.itemId : null,
                amount = slot.amount
            });
        }

        return data;
    }

    public void FromData(PlayerInventoryData data)
    {
        if (data == null)
        {
            Debug.LogWarning("PlayerInventory.FromData: data is null");
            return;
        }

        gold = Mathf.Max(0, data.gold);
        OnGoldChanged?.Invoke(gold);

        slots.Clear();
        EnsureSlotCount();

        int count = Mathf.Min(slots.Count, data.slots.Count);

        for (int i = 0; i < count; i++)
        {
            var slotData = data.slots[i];
            var slot = slots[i];

            if (slotData == null)
            {
                slot.Clear();
                continue;
            }

            if (slotData.weaponInstance != null)
            {
                var instance = slotData.weaponInstance.DeepClone();
                var weaponDef = ResolveWeaponDefinition(instance.baseWeaponId);
                if (weaponDef == null)
                    Debug.LogWarning($"Weapon with id {instance.baseWeaponId} not found in database");

                slot.SetWeaponInstance(weaponDef, instance);
                continue;
            }

            if (string.IsNullOrEmpty(slotData.itemId) || slotData.amount <= 0)
            {
                slot.Clear();
                continue;
            }

            var itemDef = itemDatabase != null ? itemDatabase.GetItemById(slotData.itemId) : null;
            if (itemDef == null)
            {
                Debug.LogWarning($"Item with id {slotData.itemId} not found in database");
                slot.Clear();
                continue;
            }

            slot.SetItem(itemDef, slotData.amount);
        }
    }

    public void OnSave(GameSaveData data)
    {
        if (data == null)
            return;

        data.inventory = ToData();
        if (data.weapon == null)
            data.weapon = new PlayerWeaponData();
        data.weapon.equippedWeaponInstanceId = equippedWeaponInstanceId;
    }

    public void OnLoad(GameSaveData data)
    {
        if (data == null)
        {
            Debug.Log("[PlayerInventory] No save data");
            EnsureDefaultWeaponInstance();
            ApplyEquippedWeaponIfPossible();
            ForceRefreshGoldUI();
            return;
        }

        if (data.inventory != null)
            FromData(data.inventory);
        else
            EnsureSlotCount();

        equippedWeaponInstanceId = data.weapon != null ? data.weapon.equippedWeaponInstanceId : equippedWeaponInstanceId;

        ForceRefreshGoldUI();
        EnsureDefaultWeaponInstance();
        ApplyEquippedWeaponIfPossible();
    }

    void EnsureSlotCount()
    {
        if (slots == null)
            slots = new List<InventorySlot>();

        while (slots.Count < slotCount)
            slots.Add(new InventorySlot());
    }

    int FindEmptySlotIndex()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null || slots[i].IsEmpty)
                return i;
        }

        return -1;
    }

    void EnsureDefaultWeaponInstance()
    {
        if (GetWeaponInstance(equippedWeaponInstanceId) != null)
            return;

        if (TryAssignFirstWeaponInstance())
            return;

        if (ctx == null || ctx.currentWeapon == null)
            return;

        var defaultInstance = WeaponInstanceFactory.CreatePlainInstance(ctx.currentWeapon);
        if (!AddWeaponInstance(defaultInstance))
            return;

        equippedWeaponInstanceId = defaultInstance.instanceId;
    }

    bool TryAssignFirstWeaponInstance()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || !slot.HasWeaponInstance || slot.weaponInstance == null)
                continue;

            equippedWeaponInstanceId = slot.weaponInstance.instanceId;
            return true;
        }

        return false;
    }

    bool ApplyEquippedWeaponIfPossible()
    {
        var instance = GetWeaponInstance(equippedWeaponInstanceId);
        if (instance == null && !TryAssignFirstWeaponInstance())
            return false;

        instance = GetWeaponInstance(equippedWeaponInstanceId);
        if (instance == null)
            return false;

        var weaponDef = ResolveWeaponDefinition(instance.baseWeaponId);
        if (weaponDef == null)
        {
            Debug.LogWarning($"[PlayerInventory] Could not resolve weapon definition: {instance.baseWeaponId}");
            return false;
        }

        if (ctx != null)
            ctx.currentWeapon = weaponDef;

        if (weaponSystem != null)
            weaponSystem.Equip(weaponDef, instance);

        return true;
    }

    GunConfig ResolveWeaponDefinition(string baseWeaponId)
    {
        if (string.IsNullOrWhiteSpace(baseWeaponId))
            return null;

        if (weaponDatabase != null)
        {
            var fromWeaponDb = weaponDatabase.GetById(baseWeaponId);
            if (fromWeaponDb != null)
                return fromWeaponDb;
        }

        if (itemDatabase != null)
        {
            var fromItemDb = itemDatabase.GetItemById(baseWeaponId) as GunConfig;
            if (fromItemDb != null)
                return fromItemDb;
        }

        if (ctx != null && ctx.currentWeapon != null)
        {
            var currentId = WeaponInstanceFactory.ResolveBaseWeaponId(ctx.currentWeapon);
            if (string.Equals(currentId, baseWeaponId, StringComparison.Ordinal))
                return ctx.currentWeapon;
        }

        return null;
    }
}
