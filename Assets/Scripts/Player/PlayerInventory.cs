using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour, IGameSaveAble, ISaveOrder
{
    [Header("Settings")]
    public int slotCount = 0;

    [Header("Runtime")]
    public List<InventorySlotData> slots = new();

    [Header("Refs")]
    public ItemDatabase itemDatabase;
    [SerializeField] private WeaponDatabase weaponDatabase;
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private CharacterEquipment equipment;
    [SerializeField] private WeaponSystem weaponSystem;

    [Header("Currency")]
    [SerializeField] private int gold;
    public int Gold => gold;

    [Header("Weapon Instance")]
    [SerializeField] private string equippedWeaponInstanceId;

    InventorySystem inventorySystem;

    public int LoadOrder => 0;
    public string EquippedWeaponInstanceId => equippedWeaponInstanceId;
    public InventorySystem InventorySystem => inventorySystem;
    public IReadOnlyList<InventorySlotData> Slots => inventorySystem != null ? inventorySystem.Slots : slots;
    public Func<int, InventorySlotData, bool> PlacementValidator
    {
        get
        {
            InitializeInventorySystem();
            return inventorySystem.PlacementValidator;
        }
        set
        {
            InitializeInventorySystem();
            inventorySystem.PlacementValidator = value;
        }
    }

    public event Action<int> OnGoldChanged;
    public event Action<string> OnEquippedWeaponChanged;

    void Awake()
    {
        ResolveReferences();

        InitializeInventorySystem();
        EnsureSlotCount();
    }

    void ResolveReferences()
    {
        if (!ctx)
            TryGetComponent(out ctx);

        if (!ctx)
            ctx = GetComponentInParent<CharacteContext>();

        ctx?.ResolveReferences();

        if (ctx is PlayerContext playerContext && playerContext.inventory != this)
            playerContext.inventory = this;

        if (!equipment && ctx != null && ctx.Equipment != null)
            equipment = ctx.Equipment;

        if (!equipment)
            equipment = GetComponent<CharacterEquipment>();

        if (!equipment && ctx != null)
            equipment = ctx.GetComponentInChildren<CharacterEquipment>(true);

        if (!equipment && ctx != null)
            equipment = ctx.gameObject.AddComponent<CharacterEquipment>();

        if (ctx != null && equipment != null && ctx.Equipment != equipment)
            ctx.Equipment = equipment;

        if (!weaponSystem && ctx != null && ctx.WeaponSystem != null)
            weaponSystem = ctx.WeaponSystem;

        if (!weaponSystem)
            weaponSystem = GetComponentInChildren<WeaponSystem>(true);

        if (!weaponSystem && ctx != null)
            weaponSystem = ctx.GetComponentInChildren<WeaponSystem>(true);

        if (ctx != null && weaponSystem != null && ctx.WeaponSystem != weaponSystem)
            ctx.WeaponSystem = weaponSystem;

        equipment?.ResolveReferences();
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
        InitializeInventorySystem();

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

        bool added = inventorySystem.AddItem(item, amount);
        if (!added)
            Debug.Log("Inventory full!");

        return added;
    }

    public bool CanAddItem(ItemDefinition item, int amount = 1)
    {
        InitializeInventorySystem();

        if (item == null || amount <= 0)
            return false;

        if (itemDatabase != null && itemDatabase.goldItem != null && item == itemDatabase.goldItem)
            return true;

        if (item is GunConfig gun && !item.stackable)
            return inventorySystem.CanAddWeaponInstances(gun, amount);

        return inventorySystem.CanAddItem(item, amount);
    }

    public bool AddWeaponInstance(WeaponInstanceData weaponInstance)
    {
        InitializeInventorySystem();

        if (weaponInstance == null)
            return false;

        var instanceCopy = weaponInstance.DeepClone();
        var weaponDef = ResolveWeaponDefinition(instanceCopy.baseWeaponId);
        if (weaponDef == null)
        {
            Debug.LogWarning($"[PlayerInventory] Could not resolve weapon definition: {instanceCopy.baseWeaponId}");
            return false;
        }

        if (!inventorySystem.AddWeaponInstance(weaponDef, instanceCopy))
        {
            Debug.Log("Inventory full!");
            return false;
        }

        if (string.IsNullOrWhiteSpace(equippedWeaponInstanceId))
            SetEquippedWeaponInstanceId(instanceCopy.instanceId);

        return true;
    }

    public bool RemoveWeaponInstance(string instanceId)
    {
        InitializeInventorySystem();

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
            inventorySystem.NotifySlotChanged(i);
            CharacterEquipment.ReleaseRemovedInventoryWeapon(instanceId);

            if (string.Equals(equippedWeaponInstanceId, instanceId, StringComparison.Ordinal))
            {
                SetEquippedWeaponInstanceId(null);
                if (TryAssignFirstWeaponInstance())
                    ApplyEquippedWeaponIfPossible();
                else
                    ClearEquippedWeaponRuntime();
            }

            return true;
        }

        return false;
    }

    public bool EquipWeaponInstance(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        ResolveReferences();

        string requesterOwnerId = equipment != null ? equipment.OwnerId : null;
        if (CharacterEquipment.IsWeaponInstanceUnavailable(instanceId, requesterOwnerId, equipment))
            return false;

        if (GetWeaponInstance(instanceId) == null)
            return false;

        bool changed = SetEquippedWeaponInstanceId(instanceId);
        bool equipped = ApplyEquippedWeaponIfPossible();

        if (equipped && changed && SaveManager.Instance != null)
            SaveManager.Instance.Save();

        return equipped;
    }

    public bool EquipWeaponInstanceForOwner(string equipmentOwnerId, string instanceId)
    {
        if (string.IsNullOrWhiteSpace(equipmentOwnerId) || string.IsNullOrWhiteSpace(instanceId))
            return false;

        if (!TryGetWeaponInstanceWithDefinition(instanceId, out _, out _))
            return false;

        if (CharacterEquipment.IsWeaponInstanceUnavailable(instanceId, equipmentOwnerId, null))
            return false;

        if (CharacterEquipment.TryFindSceneEquipmentByOwner(equipmentOwnerId, out CharacterEquipment targetEquipment))
        {
            bool equipped = targetEquipment.EquipFromInventory(this, instanceId);
            if (equipped && targetEquipment.IsPlayerEquipment)
                SetEquippedWeaponInstanceId(instanceId);

            if (equipped && SaveManager.Instance != null)
                SaveManager.Instance.Save();

            return equipped;
        }

        if (CharacterEquipment.IsWeaponInstanceEquippedByOther(instanceId, null))
            return false;

        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();

        return CharacterEquipment.SaveEquipmentAssignment(equipmentOwnerId, instanceId, this);
    }

    public WeaponInstanceData GetWeaponInstance(string instanceId)
    {
        InitializeInventorySystem();

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

    public bool NotifyWeaponInstanceChanged(string instanceId)
    {
        InitializeInventorySystem();

        int slotIndex = FindWeaponInstanceSlotIndex(instanceId);
        if (slotIndex < 0)
            return false;

        inventorySystem.NotifySlotChanged(slotIndex);

        if (string.Equals(equippedWeaponInstanceId, instanceId, StringComparison.Ordinal))
        {
            ResolveReferences();
            ctx?.StatsHub?.MarkDirty();
            weaponSystem?.NotifyWeaponInstanceChanged();
        }

        return true;
    }

    public int FindWeaponInstanceSlotIndex(string instanceId)
    {
        InitializeInventorySystem();

        if (string.IsNullOrWhiteSpace(instanceId))
            return -1;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || !slot.HasWeaponInstance || slot.weaponInstance == null)
                continue;

            if (string.Equals(slot.weaponInstance.instanceId, instanceId, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    public bool TryGetWeaponInstanceWithDefinition(
        string instanceId,
        out GunConfig weaponDefinition,
        out WeaponInstanceData weaponInstance)
    {
        weaponDefinition = null;
        weaponInstance = null;

        weaponInstance = GetWeaponInstance(instanceId);
        if (weaponInstance == null)
            return false;

        weaponDefinition = ResolveWeaponDefinition(weaponInstance.baseWeaponId);
        return weaponDefinition != null;
    }

    public bool HasItem(ItemDefinition item, int amount = 1)
    {
        InitializeInventorySystem();
        return inventorySystem.HasItem(item, amount);
    }

    public bool RemoveItem(ItemDefinition item, int amount = 1)
    {
        InitializeInventorySystem();
        return inventorySystem.RemoveItem(item, amount);
    }

    public bool MoveOrSwap(int fromIndex, int toIndex)
    {
        InitializeInventorySystem();
        return inventorySystem.MoveOrSwap(fromIndex, toIndex);
    }

    public bool TryMerge(int fromIndex, int toIndex)
    {
        InitializeInventorySystem();
        return inventorySystem.TryMerge(fromIndex, toIndex);
    }

    public bool SplitStack(int fromIndex, int toIndex, int amount)
    {
        InitializeInventorySystem();
        return inventorySystem.SplitStack(fromIndex, toIndex, amount);
    }

    public bool CanPlaceItem(int slotIndex, ItemDefinition item)
    {
        InitializeInventorySystem();
        return inventorySystem.CanPlaceItem(slotIndex, item);
    }

    public bool CanPlaceItem(int slotIndex, InventorySlotData slotData)
    {
        InitializeInventorySystem();
        return inventorySystem.CanPlaceItem(slotIndex, slotData);
    }

    public PlayerInventoryData ToData()
    {
        InitializeInventorySystem();

        var data = new PlayerInventoryData
        {
            gold = gold,
            maxSlotCount = slotCount
        };

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.IsEmpty)
            {
                data.slots.Add(new InventorySlotSaveData());
                continue;
            }

            if (slot.HasWeaponInstance && slot.weaponInstance != null)
            {
                data.slots.Add(new InventorySlotSaveData
                {
                    itemId = slot.item != null ? slot.item.itemId : slot.weaponInstance.baseWeaponId,
                    amount = 1,
                    weaponInstance = slot.weaponInstance.DeepClone()
                });
                continue;
            }

            data.slots.Add(new InventorySlotSaveData
            {
                itemId = slot.item != null ? slot.item.itemId : null,
                amount = slot.quantity
            });
        }

        return data;
    }

    public void FromData(PlayerInventoryData data)
    {
        InitializeInventorySystem();

        if (data == null)
        {
            Debug.LogWarning("PlayerInventory.FromData: data is null");
            return;
        }

        gold = Mathf.Max(0, data.gold);
        OnGoldChanged?.Invoke(gold);
        
        if (data.maxSlotCount > 0)
            slotCount = data.maxSlotCount;

        EnsureSlotCount();

        for (int i = 0; i < slots.Count; i++)
            slots[i].Clear();

        int incomingCount = data.slots != null ? data.slots.Count : 0;
        int count = Mathf.Min(slots.Count, incomingCount);

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
                {
                    Debug.LogWarning($"Weapon with id {instance.baseWeaponId} not found in database");
                    slot.Clear();
                    continue;
                }

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

        inventorySystem.NotifyInventoryReset();
    }

    public void OnSave(GameSaveData data)
    {
        if (data == null)
            return;

        SyncEquippedWeaponIdFromEquipment();

        data.inventory = ToData();
        if (data.weapon == null)
            data.weapon = new PlayerWeaponData();

        data.weapon.equippedWeaponInstanceId = equippedWeaponInstanceId;
        CharacterEquipment.WriteSceneEquipmentToSave(data, this);
    }

    public void OnLoad(GameSaveData data)
    {
        InitializeInventorySystem();

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

        string legacyEquippedWeaponId = data.weapon != null ? data.weapon.equippedWeaponInstanceId : equippedWeaponInstanceId;
        SetEquippedWeaponInstanceId(legacyEquippedWeaponId);

        ForceRefreshGoldUI();
        EnsureDefaultWeaponInstance();
        ApplyEquippedWeaponIfPossible();

        string equipmentPlayerWeaponId = CharacterEquipment.ApplySceneEquipmentFromInventory(data, this, equippedWeaponInstanceId);
        if (!string.IsNullOrWhiteSpace(equipmentPlayerWeaponId))
            SetEquippedWeaponInstanceId(equipmentPlayerWeaponId);
    }

    void InitializeInventorySystem()
    {
        if (slots == null)
            slots = new List<InventorySlotData>();

        inventorySystem ??= new InventorySystem(slots, slotCount);
        inventorySystem.EnsureSlotCount(slotCount);
    }

    void EnsureSlotCount()
    {
        InitializeInventorySystem();
        inventorySystem.EnsureSlotCount(slotCount);
    }

    void EnsureDefaultWeaponInstance()
    {
        if (GetWeaponInstance(equippedWeaponInstanceId) != null)
            return;

        if (TryAssignFirstWeaponInstance())
            return;

        GunConfig defaultWeapon = ResolveDefaultWeaponDefinition();
        if (defaultWeapon == null)
            return;

        var defaultInstance = WeaponInstanceFactory.CreatePlainInstance(defaultWeapon);
        if (!AddWeaponInstance(defaultInstance))
            return;

        SetEquippedWeaponInstanceId(defaultInstance.instanceId);
    }

    bool TryAssignFirstWeaponInstance()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || !slot.HasWeaponInstance || slot.weaponInstance == null)
                continue;

            SetEquippedWeaponInstanceId(slot.weaponInstance.instanceId);
            return true;
        }

        return false;
    }

    bool ApplyEquippedWeaponIfPossible()
    {
        ResolveReferences();

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

        if (equipment != null)
            return equipment.Equip(weaponDef, instance);

        if (ctx != null)
            ctx.currentWeapon = weaponDef;

        if (weaponSystem != null)
            weaponSystem.Equip(weaponDef, instance);

        return true;
    }

    void ClearEquippedWeaponRuntime()
    {
        ResolveReferences();

        if (equipment != null)
        {
            equipment.ClearEquipment();
            return;
        }

        if (ctx != null)
            ctx.currentWeapon = null;

        if (weaponSystem != null)
            weaponSystem.Equip(null, null);
    }

    void SyncEquippedWeaponIdFromEquipment()
    {
        ResolveReferences();

        if (equipment == null || string.IsNullOrWhiteSpace(equipment.EquippedWeaponInstanceId))
            return;

        SetEquippedWeaponInstanceId(equipment.EquippedWeaponInstanceId);
    }

    GunConfig ResolveDefaultWeaponDefinition()
    {
        ResolveReferences();

        if (equipment != null && equipment.DefaultWeapon != null)
            return equipment.DefaultWeapon;

        if (ctx != null && ctx.currentWeapon != null)
            return ctx.currentWeapon;

        if (equipment != null && equipment.CurrentWeapon != null)
            return equipment.CurrentWeapon;

        if (weaponSystem != null && weaponSystem.CurrentWeapon != null)
            return weaponSystem.CurrentWeapon;

        return null;
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

        if (equipment != null && equipment.CurrentWeapon != null)
        {
            var currentId = WeaponInstanceFactory.ResolveBaseWeaponId(equipment.CurrentWeapon);
            if (string.Equals(currentId, baseWeaponId, StringComparison.Ordinal))
                return equipment.CurrentWeapon;
        }

        if (equipment != null && equipment.DefaultWeapon != null)
        {
            var defaultId = WeaponInstanceFactory.ResolveBaseWeaponId(equipment.DefaultWeapon);
            if (string.Equals(defaultId, baseWeaponId, StringComparison.Ordinal))
                return equipment.DefaultWeapon;
        }

        if (ctx != null && ctx.currentWeapon != null)
        {
            var currentId = WeaponInstanceFactory.ResolveBaseWeaponId(ctx.currentWeapon);
            if (string.Equals(currentId, baseWeaponId, StringComparison.Ordinal))
                return ctx.currentWeapon;
        }

        return null;
    }

    bool SetEquippedWeaponInstanceId(string instanceId)
    {
        if (string.Equals(equippedWeaponInstanceId, instanceId, StringComparison.Ordinal))
            return false;

        equippedWeaponInstanceId = instanceId;
        OnEquippedWeaponChanged?.Invoke(equippedWeaponInstanceId);
        return true;
    }
}
