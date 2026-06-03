using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CharacterWeaponPreviewResolver
{
    public static bool TryResolveSelectedCharacterWeapon(
        CharacterStats selected,
        int currentSlot,
        int partyIndex,
        WeaponDatabase weaponDatabase,
        out GunConfig weapon)
    {
        return TryResolveSelectedCharacterWeapon(
            selected,
            currentSlot,
            partyIndex,
            weaponDatabase,
            null,
            null,
            out weapon);
    }

    public static bool TryResolveSelectedCharacterWeapon(
        CharacterStats selected,
        int currentSlot,
        int partyIndex,
        WeaponDatabase weaponDatabase,
        PlayerInventory preferredInventory,
        string preferredEquippedInstanceId,
        out GunConfig weapon)
    {
        weapon = null;

        if (selected == null)
            return false;

        string ownerId = CharacterEquipment.BuildCharacterOwnerId(selected.characterId);
        if (string.IsNullOrWhiteSpace(ownerId))
            return false;

        var data = LoadCurrentGameData(currentSlot);
        string equippedId = preferredEquippedInstanceId;
        if (string.IsNullOrWhiteSpace(equippedId))
            CharacterEquipment.TryFindEquipmentEntry(data?.equipment, ownerId, out equippedId);

        if (string.IsNullOrWhiteSpace(equippedId) &&
            ShouldUsePlayerInventoryFallback(data, selected, partyIndex))
        {
            equippedId = ResolveInventoryEquippedWeaponInstanceId(preferredInventory);
        }

        if (!string.IsNullOrWhiteSpace(equippedId))
        {
            if (TryResolveWeaponFromRuntimeInventory(equippedId, weaponDatabase, preferredInventory, out weapon))
                return true;

            if (TryResolveWeaponFromSave(data, equippedId, weaponDatabase, out weapon))
                return true;
        }

        if (ShouldUsePlayerInventoryFallback(data, selected, partyIndex) &&
            TryResolveFirstSavedWeapon(data, weaponDatabase, out weapon))
        {
            return true;
        }

        return TryResolveCharacterWeaponFromSceneEquipment(ownerId, weaponDatabase, preferredInventory, out weapon);
    }

    static bool TryResolveCharacterWeaponFromSceneEquipment(
        string ownerId,
        WeaponDatabase weaponDatabase,
        PlayerInventory preferredInventory,
        out GunConfig weapon)
    {
        weapon = null;

        if (!CharacterEquipment.TryFindSceneEquipmentByOwner(ownerId, out var equipment) || equipment == null)
            return false;

        if (equipment.CurrentWeapon != null)
        {
            weapon = equipment.CurrentWeapon;
            return true;
        }

        string equippedId = equipment.EquippedWeaponInstanceId;
        if (string.IsNullOrWhiteSpace(equippedId))
            return false;

        return TryResolveWeaponFromRuntimeInventory(equippedId, weaponDatabase, preferredInventory, out weapon);
    }

    static bool TryResolveWeaponFromRuntimeInventory(
        string equippedId,
        WeaponDatabase weaponDatabase,
        PlayerInventory preferredInventory,
        out GunConfig weapon)
    {
        weapon = null;

        if (string.IsNullOrWhiteSpace(equippedId))
            return false;

        if (TryResolveWeaponFromInventory(preferredInventory, equippedId, weaponDatabase, out weapon))
            return true;

        var inventories = Object.FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < inventories.Length; i++)
        {
            var inventory = inventories[i];
            if (!inventory || inventory == preferredInventory)
                continue;

            if (TryResolveWeaponFromInventory(inventory, equippedId, weaponDatabase, out weapon))
                return true;
        }

        return false;
    }

    static bool TryResolveWeaponFromInventory(
        PlayerInventory inventory,
        string equippedId,
        WeaponDatabase weaponDatabase,
        out GunConfig weapon)
    {
        weapon = null;

        if (!inventory)
            return false;

        string baseWeaponId = FindBaseWeaponId(inventory.Slots, equippedId);
        return TryResolveWeaponDefinition(baseWeaponId, inventory, weaponDatabase, out weapon);
    }

    static bool TryResolveWeaponFromSave(GameSaveData data, string equippedId, WeaponDatabase weaponDatabase, out GunConfig weapon)
    {
        weapon = null;

        if (data == null || string.IsNullOrWhiteSpace(equippedId))
            return false;

        string baseWeaponId = FindBaseWeaponId(data.inventory?.slots, equippedId);
        return TryResolveWeaponDefinition(baseWeaponId, null, weaponDatabase, out weapon);
    }

    static GameSaveData LoadCurrentGameData(int currentSlot)
    {
        int saveSlot = SaveManager.Instance != null ? SaveManager.Instance.currentSlot : currentSlot;
        GameSaveData data = SaveSystem.LoadGame(saveSlot);
        PartyData party = SaveSystem.LoadPartyOnly(saveSlot);
        if (party != null)
        {
            data ??= new GameSaveData();
            data.party = party;
        }

        return data;
    }

    static bool ShouldUsePlayerInventoryFallback(GameSaveData data, CharacterStats selected, int partyIndex)
    {
        if (partyIndex == 0)
            return true;

        if (data?.party?.partyIds == null || data.party.partyIds.Count == 0 || selected == null)
            return false;

        return string.Equals(data.party.partyIds[0], selected.characterId, StringComparison.Ordinal);
    }

    static string ResolveInventoryEquippedWeaponInstanceId(PlayerInventory inventory)
    {
        if (!inventory)
            return null;

        if (!string.IsNullOrWhiteSpace(inventory.EquippedWeaponInstanceId))
            return inventory.EquippedWeaponInstanceId;

        var slots = inventory.Slots;
        if (slots == null)
            return null;

        for (int i = 0; i < slots.Count; i++)
        {
            var instance = slots[i]?.weaponInstance;
            if (instance != null && !string.IsNullOrWhiteSpace(instance.instanceId))
                return instance.instanceId;
        }

        return null;
    }

    static bool TryResolveFirstSavedWeapon(GameSaveData data, WeaponDatabase weaponDatabase, out GunConfig weapon)
    {
        weapon = null;

        var slots = data?.inventory?.slots;
        if (slots == null)
            return false;

        for (int i = 0; i < slots.Count; i++)
        {
            string baseWeaponId = slots[i]?.weaponInstance?.baseWeaponId;
            if (TryResolveWeaponDefinition(baseWeaponId, null, weaponDatabase, out weapon))
                return true;
        }

        return false;
    }

    static bool TryResolveWeaponDefinition(
        string baseWeaponId,
        PlayerInventory sourceInventory,
        WeaponDatabase weaponDatabase,
        out GunConfig weapon)
    {
        weapon = null;

        if (string.IsNullOrWhiteSpace(baseWeaponId))
            return false;

        if (weaponDatabase != null)
        {
            weapon = weaponDatabase.GetById(baseWeaponId);
            if (weapon != null)
                return true;
        }

        if (sourceInventory != null && sourceInventory.itemDatabase != null)
        {
            weapon = sourceInventory.itemDatabase.GetItemById(baseWeaponId) as GunConfig;
            if (weapon != null)
                return true;
        }

        var loadedWeaponDatabases = Resources.FindObjectsOfTypeAll<WeaponDatabase>();
        for (int i = 0; i < loadedWeaponDatabases.Length; i++)
        {
            var loadedDb = loadedWeaponDatabases[i];
            if (!loadedDb)
                continue;

            weapon = loadedDb.GetById(baseWeaponId);
            if (weapon != null)
                return true;
        }

        var loadedItemDatabases = Resources.FindObjectsOfTypeAll<ItemDatabase>();
        for (int i = 0; i < loadedItemDatabases.Length; i++)
        {
            var loadedDb = loadedItemDatabases[i];
            if (!loadedDb)
                continue;

            weapon = loadedDb.GetItemById(baseWeaponId) as GunConfig;
            if (weapon != null)
                return true;
        }

        var loadedWeapons = Resources.FindObjectsOfTypeAll<GunConfig>();
        for (int i = 0; i < loadedWeapons.Length; i++)
        {
            var candidate = loadedWeapons[i];
            if (!candidate)
                continue;

            string candidateId = WeaponInstanceFactory.ResolveBaseWeaponId(candidate);
            if (!string.Equals(candidateId, baseWeaponId, StringComparison.Ordinal))
                continue;

            weapon = candidate;
            return true;
        }

        return false;
    }

    static string FindBaseWeaponId(IReadOnlyList<InventorySlotData> slots, string equippedInstanceId)
    {
        if (slots == null || string.IsNullOrWhiteSpace(equippedInstanceId))
            return null;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var instance = slot?.weaponInstance;
            if (instance == null)
                continue;

            if (string.Equals(instance.instanceId, equippedInstanceId, StringComparison.Ordinal))
                return instance.baseWeaponId;
        }

        return null;
    }

    static string FindBaseWeaponId(IReadOnlyList<InventorySlotSaveData> slots, string equippedInstanceId)
    {
        if (slots == null || string.IsNullOrWhiteSpace(equippedInstanceId))
            return null;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var instance = slot?.weaponInstance;
            if (instance == null)
                continue;

            if (string.Equals(instance.instanceId, equippedInstanceId, StringComparison.Ordinal))
                return instance.baseWeaponId;
        }

        return null;
    }
}
