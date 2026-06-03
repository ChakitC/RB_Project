using System;
using UnityEngine;

public enum EquipmentItemKind
{
    Weapon,
    Accessory
}

public static class EquipmentAssignmentService
{
    public static bool IsEquipmentSlot(InventorySlotData slotData, EquipmentItemKind itemKind)
    {
        if (slotData == null || slotData.IsEmpty)
            return false;

        switch (itemKind)
        {
            case EquipmentItemKind.Weapon:
                return slotData.HasWeaponInstance ||
                       (slotData.item != null && slotData.item.itemType == ItemType.Weapon);

            case EquipmentItemKind.Accessory:
                return slotData.HasAccessoryInstance ||
                       (slotData.item != null && slotData.item.itemType == ItemType.Accessory);

            default:
                return false;
        }
    }

    public static bool TryGetInstanceId(InventorySlotData slotData, EquipmentItemKind itemKind, out string instanceId)
    {
        instanceId = null;

        if (slotData == null || slotData.IsEmpty)
            return false;

        switch (itemKind)
        {
            case EquipmentItemKind.Weapon:
                if (!slotData.HasWeaponInstance || slotData.weaponInstance == null)
                    return false;

                instanceId = slotData.weaponInstance.instanceId;
                break;

            case EquipmentItemKind.Accessory:
                if (!slotData.HasAccessoryInstance || slotData.accessoryInstance == null)
                    return false;

                instanceId = slotData.accessoryInstance.instanceId;
                break;
        }

        return !string.IsNullOrWhiteSpace(instanceId);
    }

    public static bool TryEquip(
        PlayerInventory inventory,
        EquipmentItemKind itemKind,
        string ownerId,
        string instanceId,
        int equippedSlotIndex = 0)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(instanceId))
            return false;

        switch (itemKind)
        {
            case EquipmentItemKind.Weapon:
                return string.IsNullOrWhiteSpace(ownerId)
                    ? inventory.EquipWeaponInstance(instanceId)
                    : inventory.EquipWeaponInstanceForOwner(ownerId, instanceId);

            case EquipmentItemKind.Accessory:
                int accessorySlotIndex = Mathf.Max(0, equippedSlotIndex);
                return string.IsNullOrWhiteSpace(ownerId)
                    ? inventory.EquipAccessoryInstance(instanceId, accessorySlotIndex)
                    : inventory.EquipAccessoryInstanceForOwner(ownerId, instanceId, accessorySlotIndex);

            default:
                return false;
        }
    }

    public static InventorySlotData GetEquippedSlotData(
        PlayerInventory inventory,
        EquipmentItemKind itemKind,
        string ownerId,
        int equippedSlotIndex = 0)
    {
        switch (itemKind)
        {
            case EquipmentItemKind.Weapon:
                return GetEquippedWeaponSlotData(inventory, ownerId);

            case EquipmentItemKind.Accessory:
                return GetEquippedAccessorySlotData(inventory, ownerId, Mathf.Max(0, equippedSlotIndex));

            default:
                return new InventorySlotData();
        }
    }

    public static string FindAssignedOwnerId(
        PlayerInventory inventory,
        EquipmentItemKind itemKind,
        InventorySlotData slotData)
    {
        return TryGetInstanceId(slotData, itemKind, out string instanceId)
            ? FindAssignedOwnerId(inventory, itemKind, instanceId)
            : null;
    }

    public static string FindAssignedOwnerId(
        PlayerInventory inventory,
        EquipmentItemKind itemKind,
        string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return null;

        switch (itemKind)
        {
            case EquipmentItemKind.Weapon:
                return FindAssignedWeaponOwnerId(inventory, instanceId);

            case EquipmentItemKind.Accessory:
                return FindAssignedAccessoryOwnerId(instanceId);

            default:
                return null;
        }
    }

    static InventorySlotData GetEquippedWeaponSlotData(PlayerInventory inventory, string ownerId)
    {
        string equippedInstanceId = GetEquippedWeaponInstanceId(inventory, ownerId);
        if (inventory == null || string.IsNullOrWhiteSpace(equippedInstanceId))
            return new InventorySlotData();

        InventorySlotData slotData = FindInventorySlotByInstanceId(inventory, EquipmentItemKind.Weapon, equippedInstanceId);
        return slotData ?? new InventorySlotData();
    }

    static InventorySlotData GetEquippedAccessorySlotData(PlayerInventory inventory, string ownerId, int slotIndex)
    {
        string resolvedOwnerId = string.IsNullOrWhiteSpace(ownerId)
            ? ResolveDefaultOwnerId(inventory)
            : ownerId;
        if (string.IsNullOrWhiteSpace(resolvedOwnerId))
            return new InventorySlotData();

        if (AccessoryLoadout.TryFindSceneLoadoutByOwner(resolvedOwnerId, out AccessoryLoadout loadout) &&
            loadout != null)
        {
            InventorySlotData sceneSlotData = loadout.GetSlotData(slotIndex);
            if (sceneSlotData != null && !sceneSlotData.IsEmpty)
                return sceneSlotData;
        }

        CharacterAccessoryLoadoutSaveData entry = GetSavedAccessoryEntry(resolvedOwnerId);
        AccessoryInstanceData savedInstance = GetSavedAccessoryInstance(entry, slotIndex);
        if (savedInstance == null || savedInstance.IsEmpty)
            return new InventorySlotData();

        if (inventory != null &&
            inventory.TryGetAccessoryInstanceWithDefinition(
                savedInstance.instanceId,
                out AccessoryDefinition inventoryDefinition,
                out AccessoryInstanceData inventoryInstance))
        {
            var inventorySlotData = new InventorySlotData();
            inventorySlotData.SetAccessoryInstance(inventoryDefinition, inventoryInstance);
            return inventorySlotData;
        }

        AccessoryDefinition savedDefinition = ResolveAccessoryDefinition(inventory, savedInstance.accessoryId);
        if (savedDefinition == null)
            return new InventorySlotData();

        var slotData = new InventorySlotData();
        slotData.SetAccessoryInstance(savedDefinition, savedInstance);
        return slotData;
    }

    static string GetEquippedWeaponInstanceId(PlayerInventory inventory, string ownerId)
    {
        if (inventory == null)
            return null;

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            ownerId = ResolveDefaultOwnerId(inventory);
            if (string.IsNullOrWhiteSpace(ownerId))
                return inventory.EquippedWeaponInstanceId;
        }

        if (CharacterEquipment.TryFindSceneEquipmentByOwner(ownerId, out CharacterEquipment equipment) &&
            equipment != null &&
            !string.IsNullOrWhiteSpace(equipment.EquippedWeaponInstanceId))
        {
            return equipment.EquippedWeaponInstanceId;
        }

        GameSaveData data = LoadCurrentGameData();
        if (CharacterEquipment.TryFindEquipmentEntry(data?.equipment, ownerId, out string savedInstanceId) &&
            !string.IsNullOrWhiteSpace(savedInstanceId))
        {
            return savedInstanceId;
        }

        if (string.Equals(ownerId, ResolveDefaultOwnerId(inventory), StringComparison.Ordinal))
            return ResolveInventoryEquippedWeaponInstanceId(inventory);

        return null;
    }

    static string FindAssignedWeaponOwnerId(PlayerInventory inventory, string instanceId)
    {
        if (CharacterEquipment.TryFindSceneEquipmentByWeaponInstance(instanceId, out CharacterEquipment equipment) &&
            equipment != null)
        {
            return equipment.OwnerId;
        }

        GameSaveData data = LoadCurrentGameData();
        string savedOwnerId = FindSavedWeaponOwnerId(data, instanceId);
        if (!string.IsNullOrWhiteSpace(savedOwnerId))
            return savedOwnerId;

        return null;
    }

    static string ResolveDefaultOwnerId(PlayerInventory inventory)
    {
        if (inventory != null)
        {
            CharacteContext context = inventory.GetComponent<CharacteContext>();
            if (context == null)
                context = inventory.GetComponentInParent<CharacteContext>();

            context?.ResolveReferences();
            if (context != null && context.baseStats != null)
                return CharacterEquipment.BuildCharacterOwnerId(context.baseStats.characterId);
        }

        GameSaveData data = LoadCurrentGameData();
        if (data?.party?.partyIds == null || data.party.partyIds.Count == 0)
            return null;

        return CharacterEquipment.BuildCharacterOwnerId(data.party.partyIds[0]);
    }

    static string ResolveInventoryEquippedWeaponInstanceId(PlayerInventory inventory)
    {
        if (inventory == null)
            return null;

        if (!string.IsNullOrWhiteSpace(inventory.EquippedWeaponInstanceId))
            return inventory.EquippedWeaponInstanceId;

        var slots = inventory.Slots;
        if (slots == null)
            return null;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlotData slot = slots[i];
            if (slot?.weaponInstance == null)
                continue;

            if (!string.IsNullOrWhiteSpace(slot.weaponInstance.instanceId))
                return slot.weaponInstance.instanceId;
        }

        return null;
    }

    static string FindAssignedAccessoryOwnerId(string instanceId)
    {
        if (AccessoryLoadout.TryFindSceneLoadoutByAccessoryInstance(instanceId, out AccessoryLoadout loadout) &&
            loadout != null)
        {
            return loadout.OwnerId;
        }

        GameSaveData data = LoadCurrentGameData();
        return FindSavedAccessoryOwnerId(data, instanceId);
    }

    static InventorySlotData FindInventorySlotByInstanceId(
        PlayerInventory inventory,
        EquipmentItemKind itemKind,
        string instanceId)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(instanceId))
            return null;

        var slots = inventory.Slots;
        if (slots == null)
            return null;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlotData slot = slots[i];
            if (!TryGetInstanceId(slot, itemKind, out string slotInstanceId))
                continue;

            if (string.Equals(slotInstanceId, instanceId, StringComparison.Ordinal))
                return slot;
        }

        return null;
    }

    static CharacterAccessoryLoadoutSaveData GetSavedAccessoryEntry(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return null;

        GameSaveData data = LoadCurrentGameData();
        return AccessoryLoadout.FindLoadoutEntry(data?.accessories, ownerId);
    }

    static AccessoryInstanceData GetSavedAccessoryInstance(CharacterAccessoryLoadoutSaveData entry, int slotIndex)
    {
        if (entry?.equippedAccessories == null ||
            slotIndex < 0 ||
            slotIndex >= entry.equippedAccessories.Count)
        {
            return null;
        }

        return entry.equippedAccessories[slotIndex];
    }

    static string FindSavedWeaponOwnerId(GameSaveData data, string instanceId)
    {
        var entries = data?.equipment?.entries;
        if (entries == null)
            return null;

        for (int i = 0; i < entries.Count; i++)
        {
            CharacterEquipmentSaveData entry = entries[i];
            if (entry == null)
                continue;

            if (string.Equals(entry.equippedWeaponInstanceId, instanceId, StringComparison.Ordinal))
                return entry.ownerId;
        }

        return null;
    }

    static string FindSavedAccessoryOwnerId(GameSaveData data, string instanceId)
    {
        var entries = data?.accessories?.entries;
        if (entries == null)
            return null;

        for (int i = 0; i < entries.Count; i++)
        {
            CharacterAccessoryLoadoutSaveData entry = entries[i];
            if (entry?.equippedAccessories == null)
                continue;

            for (int j = 0; j < entry.equippedAccessories.Count; j++)
            {
                AccessoryInstanceData instance = entry.equippedAccessories[j];
                if (instance == null)
                    continue;

                if (string.Equals(instance.instanceId, instanceId, StringComparison.Ordinal))
                    return entry.ownerId;
            }
        }

        return null;
    }

    static AccessoryDefinition ResolveAccessoryDefinition(PlayerInventory inventory, string accessoryId)
    {
        if (string.IsNullOrWhiteSpace(accessoryId))
            return null;

        if (inventory != null && inventory.itemDatabase != null)
        {
            var fromItemDatabase = inventory.itemDatabase.GetItemById(accessoryId) as AccessoryDefinition;
            if (fromItemDatabase != null)
                return fromItemDatabase;
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

    static GameSaveData LoadCurrentGameData()
    {
        int saveSlot = SaveManager.Instance != null ? SaveManager.Instance.currentSlot : 0;
        GameSaveData data = SaveSystem.LoadGame(saveSlot);
        PartyData party = SaveSystem.LoadPartyOnly(saveSlot);
        if (party != null)
        {
            data ??= new GameSaveData();
            data.party = party;
        }

        return data;
    }
}
