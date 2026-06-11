using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour, IGameSaveAble, ISaveOrder
{
    [Header("Runtime")]
    public List<InventorySlotData> slots = new();

    [Header("Refs")]
    public ItemDatabase itemDatabase;
    [SerializeField] private WeaponDatabase weaponDatabase;
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private CharacterEquipment equipment;
    [SerializeField] private AccessoryLoadout accessoryLoadout;
    [SerializeField] private WeaponSystem weaponSystem;

    [Header("Currency")]
    [SerializeField] private int gold;
    [SerializeField] private int scrap;
    public int Gold => gold;
    public int Scrap => scrap;

    [Header("Weapon Instance")]
    [SerializeField] private string equippedWeaponInstanceId;

    InventorySystem inventorySystem;

    public int LoadOrder => 0;
    public int ConfiguredCapacity => InventorySettings.ResolveDefaultSlotCount();
    public int Capacity => inventorySystem != null ? inventorySystem.Capacity : Mathf.Max(ConfiguredCapacity, slots?.Count ?? 0);
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
    public event Action<int> OnScrapChanged;
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

        if (!accessoryLoadout && ctx != null && ctx.AccessoryLoadout != null)
            accessoryLoadout = ctx.AccessoryLoadout;

        if (!accessoryLoadout)
            accessoryLoadout = GetComponent<AccessoryLoadout>();

        if (!accessoryLoadout && ctx != null)
            accessoryLoadout = ctx.GetComponentInChildren<AccessoryLoadout>(true);

        if (!accessoryLoadout && ctx != null)
            accessoryLoadout = ctx.gameObject.AddComponent<AccessoryLoadout>();

        if (ctx != null && accessoryLoadout != null && ctx.AccessoryLoadout != accessoryLoadout)
            ctx.AccessoryLoadout = accessoryLoadout;

        if (!weaponSystem && ctx != null && ctx.WeaponSystem != null)
            weaponSystem = ctx.WeaponSystem;

        if (!weaponSystem)
            weaponSystem = GetComponentInChildren<WeaponSystem>(true);

        if (!weaponSystem && ctx != null)
            weaponSystem = ctx.GetComponentInChildren<WeaponSystem>(true);

        if (ctx != null && weaponSystem != null && ctx.WeaponSystem != weaponSystem)
            ctx.WeaponSystem = weaponSystem;

        equipment?.ResolveReferences();
        accessoryLoadout?.ResolveReferences();
    }

    void Start()
    {
        if (SaveManager.Instance == null)
        {
            Debug.Log("Save Manager Missing");
            EnsureDefaultWeaponInstance();
            ApplyEquippedWeaponIfPossible();
            ForceRefreshCurrencyUI();
            return;
        }

        SaveManager.Instance.Load();
        if (!HasCurrentSlotSaveData())
        {
            EnsureDefaultWeaponInstance();
            ApplyEquippedWeaponIfPossible();
        }
        ForceRefreshCurrencyUI();
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

    public void AddScrap(int amount)
    {
        if (amount <= 0)
            return;

        scrap += amount;
        OnScrapChanged?.Invoke(scrap);
    }

    public bool SpendScrap(int amount)
    {
        if (amount <= 0 || scrap < amount)
            return false;

        scrap -= amount;
        OnScrapChanged?.Invoke(scrap);
        return true;
    }

    public void ForceRefreshCurrencyUI()
    {
        ForceRefreshGoldUI();
        ForceRefreshScrapUI();
    }

    public void ForceRefreshGoldUI()
    {
        OnGoldChanged?.Invoke(gold);
    }

    public void ForceRefreshScrapUI()
    {
        OnScrapChanged?.Invoke(scrap);
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

        if (itemDatabase != null && itemDatabase.scrapItem != null && item == itemDatabase.scrapItem)
        {
            AddScrap(amount);
            return true;
        }

        if (item is GunConfig gun && !item.stackable)
        {
            if (!inventorySystem.CanAddWeaponInstances(gun, amount))
                return false;

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

        if (item is AccessoryDefinition accessory && !item.stackable)
        {
            if (!inventorySystem.CanAddAccessoryInstances(accessory, amount))
                return false;

            bool allAdded = true;
            for (int i = 0; i < amount; i++)
            {
                var instance = AccessoryInstanceFactory.CreateInstance(accessory);
                if (!AddAccessoryInstance(instance))
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

        if (itemDatabase != null && itemDatabase.scrapItem != null && item == itemDatabase.scrapItem)
            return true;

        if (item is GunConfig gun && !item.stackable)
            return inventorySystem.CanAddWeaponInstances(gun, amount);

        if (item is AccessoryDefinition accessory && !item.stackable)
            return inventorySystem.CanAddAccessoryInstances(accessory, amount);

        return inventorySystem.CanAddItem(item, amount);
    }

    public bool CanAddWeaponInstance(GunConfig weapon, int amount = 1)
    {
        InitializeInventorySystem();
        return inventorySystem.CanAddWeaponInstances(weapon, amount);
    }

    public bool CanAddAccessoryInstance(AccessoryDefinition accessory, int amount = 1)
    {
        InitializeInventorySystem();
        return inventorySystem.CanAddAccessoryInstances(accessory, amount);
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

    public bool AddAccessoryInstance(AccessoryInstanceData accessoryInstance)
    {
        InitializeInventorySystem();

        if (accessoryInstance == null)
            return false;

        var instanceCopy = accessoryInstance.DeepClone();
        if (string.IsNullOrWhiteSpace(instanceCopy.instanceId))
            instanceCopy.instanceId = AccessoryInstanceFactory.CreateInstanceId();

        var accessoryDef = ResolveAccessoryDefinition(instanceCopy.accessoryId);
        if (accessoryDef == null)
        {
            Debug.LogWarning($"[PlayerInventory] Could not resolve accessory definition: {instanceCopy.accessoryId}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(instanceCopy.accessoryId))
            instanceCopy.accessoryId = AccessoryInstanceFactory.ResolveAccessoryId(accessoryDef);

        if (!inventorySystem.AddAccessoryInstance(accessoryDef, instanceCopy))
        {
            Debug.Log("Inventory full!");
            return false;
        }

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
            inventorySystem.NormalizeConfiguredSlotCount();
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

    public bool RemoveAccessoryInstance(string instanceId)
    {
        InitializeInventorySystem();

        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || !slot.HasAccessoryInstance || slot.accessoryInstance == null)
                continue;

            if (!string.Equals(slot.accessoryInstance.instanceId, instanceId, StringComparison.Ordinal))
                continue;

            slot.Clear();
            inventorySystem.NotifySlotChanged(i);
            inventorySystem.NormalizeConfiguredSlotCount();
            AccessoryLoadout.ReleaseRemovedInventoryAccessory(instanceId);
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

        bool saved = CharacterEquipment.SaveEquipmentAssignment(equipmentOwnerId, instanceId, this);
        if (saved && IsCurrentPartyLeaderOwner(equipmentOwnerId))
            SetEquippedWeaponInstanceId(instanceId);

        return saved;
    }

    public bool UnequipWeapon()
    {
        ResolveReferences();

        string ownerId = equipment != null ? equipment.OwnerId : null;
        bool hadWeapon = equipment != null
            ? equipment.CurrentWeapon != null || !string.IsNullOrWhiteSpace(equipment.EquippedWeaponInstanceId)
            : !string.IsNullOrWhiteSpace(equippedWeaponInstanceId);

        ClearEquippedWeaponRuntime();
        bool idChanged = SetEquippedWeaponInstanceId(null);
        bool saved = !string.IsNullOrWhiteSpace(ownerId) && CharacterEquipment.ClearEquipmentAssignment(ownerId);

        if ((hadWeapon || idChanged || saved) && SaveManager.Instance != null)
            SaveManager.Instance.Save();

        return hadWeapon || idChanged || saved;
    }

    public bool UnequipWeaponForOwner(string equipmentOwnerId)
    {
        if (string.IsNullOrWhiteSpace(equipmentOwnerId))
            return UnequipWeapon();

        ResolveReferences();

        bool hadWeapon = false;
        if (CharacterEquipment.TryFindSceneEquipmentByOwner(equipmentOwnerId, out CharacterEquipment targetEquipment))
        {
            hadWeapon = targetEquipment.CurrentWeapon != null ||
                        !string.IsNullOrWhiteSpace(targetEquipment.EquippedWeaponInstanceId);
            targetEquipment.ClearEquipment();
        }

        bool idChanged = false;
        if ((targetEquipment != null && targetEquipment.IsPlayerEquipment) ||
            IsCurrentPartyLeaderOwner(equipmentOwnerId))
        {
            idChanged = SetEquippedWeaponInstanceId(null);
        }

        bool saved = CharacterEquipment.ClearEquipmentAssignment(equipmentOwnerId);
        if ((hadWeapon || idChanged || saved) && SaveManager.Instance != null)
            SaveManager.Instance.Save();

        return hadWeapon || idChanged || saved;
    }

    public bool EquipAccessoryInstance(string instanceId, int slotIndex = -1)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        ResolveReferences();

        if (accessoryLoadout == null)
            return false;

        string requesterOwnerId = accessoryLoadout.OwnerId;
        if (AccessoryLoadout.IsAccessoryInstanceUnavailable(instanceId, requesterOwnerId, accessoryLoadout))
            return false;

        if (!TryGetAccessoryInstanceWithDefinition(instanceId, out _, out _))
            return false;

        bool equipped = accessoryLoadout.TryEquipFromInventory(this, instanceId, slotIndex);
        if (equipped && SaveManager.Instance != null)
            SaveManager.Instance.Save();

        return equipped;
    }

    public bool EquipAccessoryInstanceForOwner(string equipmentOwnerId, string instanceId, int slotIndex = -1)
    {
        if (string.IsNullOrWhiteSpace(equipmentOwnerId) || string.IsNullOrWhiteSpace(instanceId))
            return false;

        if (!TryGetAccessoryInstanceWithDefinition(instanceId, out _, out _))
            return false;

        AccessoryLoadout targetLoadout = FindSceneAccessoryLoadoutByOwner(equipmentOwnerId);
        if (AccessoryLoadout.IsAccessoryInstanceUnavailable(instanceId, equipmentOwnerId, targetLoadout))
            return false;

        if (targetLoadout != null)
        {
            bool equipped = targetLoadout.TryEquipFromInventory(this, instanceId, slotIndex);
            if (equipped && SaveManager.Instance != null)
                SaveManager.Instance.Save();

            return equipped;
        }

        return AccessoryLoadout.SaveLoadoutAssignment(equipmentOwnerId, instanceId, slotIndex, this);
    }

    public bool UnequipAccessorySlot(int slotIndex)
    {
        ResolveReferences();

        if (accessoryLoadout == null)
            return false;

        bool changed = accessoryLoadout.UnequipSlot(slotIndex);
        if (changed && SaveManager.Instance != null)
            SaveManager.Instance.Save();

        return changed;
    }

    public bool UnequipAccessorySlotForOwner(string equipmentOwnerId, int slotIndex)
    {
        if (string.IsNullOrWhiteSpace(equipmentOwnerId))
            return UnequipAccessorySlot(slotIndex);

        if (slotIndex < 0)
            return false;

        AccessoryLoadout targetLoadout = FindSceneAccessoryLoadoutByOwner(equipmentOwnerId);
        bool changed = targetLoadout != null && targetLoadout.UnequipSlot(slotIndex);
        bool saved = AccessoryLoadout.ClearLoadoutAssignment(equipmentOwnerId, slotIndex);

        if ((changed || saved) && SaveManager.Instance != null)
            SaveManager.Instance.Save();

        return changed || saved;
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

    public AccessoryInstanceData GetAccessoryInstance(string instanceId)
    {
        InitializeInventorySystem();

        if (string.IsNullOrWhiteSpace(instanceId))
            return null;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || !slot.HasAccessoryInstance || slot.accessoryInstance == null)
                continue;

            if (string.Equals(slot.accessoryInstance.instanceId, instanceId, StringComparison.Ordinal))
                return slot.accessoryInstance;
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

    public bool NotifyAccessoryInstanceChanged(string instanceId)
    {
        InitializeInventorySystem();

        int slotIndex = FindAccessoryInstanceSlotIndex(instanceId);
        if (slotIndex < 0)
            return false;

        inventorySystem.NotifySlotChanged(slotIndex);

        ResolveReferences();
        accessoryLoadout?.ResolveReferences();
        ctx?.StatsHub?.MarkDirty();
        ctx?.PassiveController?.RefreshPassiveLoadout();

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

    public int FindAccessoryInstanceSlotIndex(string instanceId)
    {
        InitializeInventorySystem();

        if (string.IsNullOrWhiteSpace(instanceId))
            return -1;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || !slot.HasAccessoryInstance || slot.accessoryInstance == null)
                continue;

            if (string.Equals(slot.accessoryInstance.instanceId, instanceId, StringComparison.Ordinal))
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

    public bool TryGetAccessoryInstanceWithDefinition(
        string instanceId,
        out AccessoryDefinition accessoryDefinition,
        out AccessoryInstanceData accessoryInstance)
    {
        accessoryDefinition = null;
        accessoryInstance = null;

        accessoryInstance = GetAccessoryInstance(instanceId);
        if (accessoryInstance == null)
            return false;

        accessoryDefinition = ResolveAccessoryDefinition(accessoryInstance.accessoryId);
        return accessoryDefinition != null;
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
            scrap = scrap
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

            if (slot.HasAccessoryInstance && slot.accessoryInstance != null)
            {
                data.slots.Add(new InventorySlotSaveData
                {
                    itemId = slot.item != null ? slot.item.itemId : slot.accessoryInstance.accessoryId,
                    amount = 1,
                    accessoryInstance = slot.accessoryInstance.DeepClone()
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
        scrap = Mathf.Max(0, data.scrap);
        OnGoldChanged?.Invoke(gold);
        OnScrapChanged?.Invoke(scrap);
        
        int incomingCount = data.slots != null ? data.slots.Count : 0;
        inventorySystem.EnsureSlotCount(Mathf.Max(ConfiguredCapacity, incomingCount));

        for (int i = 0; i < slots.Count; i++)
            slots[i].Clear();

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

            if (HasSavedWeaponInstance(slotData.weaponInstance))
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

            if (HasSavedAccessoryInstance(slotData.accessoryInstance, slotData.itemId))
            {
                var instance = slotData.accessoryInstance.DeepClone();
                if (string.IsNullOrWhiteSpace(instance.accessoryId))
                    instance.accessoryId = slotData.itemId;

                var accessoryDef = ResolveAccessoryDefinition(instance.accessoryId);
                if (accessoryDef == null)
                {
                    Debug.LogWarning($"Accessory with id {instance.accessoryId} not found in database");
                    slot.Clear();
                    continue;
                }

                slot.SetAccessoryInstance(accessoryDef, instance);
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

        inventorySystem.NormalizeConfiguredSlotCount(notifyReset: false);
        inventorySystem.NotifyInventoryReset();
    }

    static bool HasSavedWeaponInstance(WeaponInstanceData instance)
    {
        return instance != null && !instance.IsEmpty;
    }

    bool HasSavedAccessoryInstance(AccessoryInstanceData instance, string fallbackItemId)
    {
        if (instance == null)
            return false;

        if (!instance.IsEmpty)
            return true;

        return ResolveAccessoryDefinition(fallbackItemId) != null;
    }

    public void OnSave(GameSaveData data)
    {
        if (data == null)
            return;

        SyncEquippedWeaponIdFromEquipment();

        data.inventory = ToData();
        CharacterEquipment.WriteSceneEquipmentToSave(data, this);
        AccessoryLoadout.WriteSceneLoadoutsToSave(data, this);
    }

    public void OnLoad(GameSaveData data)
    {
        InitializeInventorySystem();

        if (data == null)
        {
            Debug.Log("[PlayerInventory] No save data");
            EnsureDefaultWeaponInstance();
            ApplyEquippedWeaponIfPossible();
            ForceRefreshCurrencyUI();
            return;
        }

        if (data.inventory != null)
            FromData(data.inventory);
        else
            EnsureSlotCount();

        ForceRefreshCurrencyUI();

        if (CharacterEquipment.HasSavedEquipmentEntries(data.equipment))
        {
            string equipmentPlayerWeaponId = CharacterEquipment.ApplySceneEquipmentFromInventory(data, this);
            SetEquippedWeaponInstanceId(equipmentPlayerWeaponId);
        }
        else
        {
            SetEquippedWeaponInstanceId(ResolveSavedPlayerWeaponInstanceId(data));
            EnsureDefaultWeaponInstance();
            ApplyEquippedWeaponIfPossible();
        }

        AccessoryLoadout.ApplySceneLoadoutsFromInventory(data, this);
    }

    string ResolveSavedPlayerWeaponInstanceId(GameSaveData data)
    {
        string partyLeaderOwnerId = ResolvePartyLeaderOwnerId(data);
        if (CharacterEquipment.TryFindEquipmentEntry(data?.equipment, partyLeaderOwnerId, out string savedInstanceId))
        {
            return savedInstanceId;
        }

        ResolveReferences();

        string equipmentOwnerId = equipment != null ? equipment.OwnerId : null;
        if (!string.Equals(equipmentOwnerId, partyLeaderOwnerId, StringComparison.Ordinal) &&
            CharacterEquipment.TryFindEquipmentEntry(data?.equipment, equipmentOwnerId, out savedInstanceId))
        {
            return savedInstanceId;
        }

        if (CharacterEquipment.HasSavedEquipmentEntries(data?.equipment))
            return null;

        return equippedWeaponInstanceId;
    }

    static string ResolvePartyLeaderOwnerId(GameSaveData data)
    {
        if (data?.party?.partyIds == null || data.party.partyIds.Count == 0)
            return null;

        return CharacterEquipment.BuildCharacterOwnerId(data.party.partyIds[0]);
    }

    static bool HasCurrentSlotSaveData()
    {
        if (SaveManager.Instance == null)
            return false;

        int saveSlot = SaveManager.Instance.currentSlot;
        return SaveSystem.LoadGame(saveSlot) != null || SaveSystem.LoadPartyOnly(saveSlot) != null;
    }

    static bool IsCurrentPartyLeaderOwner(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return false;

        int saveSlot = SaveManager.Instance != null ? SaveManager.Instance.currentSlot : 0;
        string leaderId = SaveSystem.GetPartyMemberId(saveSlot, 0);
        if (string.IsNullOrWhiteSpace(leaderId))
        {
            GameSaveData data = SaveSystem.LoadGame(saveSlot);
            if (data?.party?.partyIds != null && data.party.partyIds.Count > 0)
                leaderId = data.party.partyIds[0];
        }

        string leaderOwnerId = CharacterEquipment.BuildCharacterOwnerId(leaderId);
        return string.Equals(ownerId, leaderOwnerId, StringComparison.Ordinal);
    }

    void InitializeInventorySystem()
    {
        if (slots == null)
            slots = new List<InventorySlotData>();

        int configuredCapacity = ConfiguredCapacity;
        inventorySystem ??= new InventorySystem(slots, configuredCapacity);
        inventorySystem.SetConfiguredSlotCount(configuredCapacity);
    }

    void EnsureSlotCount()
    {
        InitializeInventorySystem();
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

        if (!TryGetWeaponInstanceWithDefinition(equipment.EquippedWeaponInstanceId, out _, out _))
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

    AccessoryDefinition ResolveAccessoryDefinition(string accessoryId)
    {
        if (string.IsNullOrWhiteSpace(accessoryId))
            return null;

        if (itemDatabase != null)
        {
            var fromItemDb = itemDatabase.GetItemById(accessoryId) as AccessoryDefinition;
            if (fromItemDb != null)
                return fromItemDb;
        }

        AccessoryDefinition[] definitions = Resources.FindObjectsOfTypeAll<AccessoryDefinition>();
        for (int i = 0; i < definitions.Length; i++)
        {
            AccessoryDefinition definition = definitions[i];
            if (definition == null)
                continue;

            if (string.Equals(definition.RuntimeId, accessoryId, StringComparison.Ordinal))
                return definition;
        }

        return null;
    }

    AccessoryLoadout FindSceneAccessoryLoadoutByOwner(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return null;

        AccessoryLoadout[] loadouts = FindObjectsByType<AccessoryLoadout>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < loadouts.Length; i++)
        {
            AccessoryLoadout candidate = loadouts[i];
            if (candidate == null || !candidate.UsesPlayerInventorySave)
                continue;

            if (string.Equals(candidate.OwnerId, ownerId, StringComparison.Ordinal))
                return candidate;
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
