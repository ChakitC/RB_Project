using System;
using System.Collections.Generic;
using UnityEngine;

public static class SaveDataMigration
{
    const string PlayerOwnerId = "player";
    const string HelperOwnerId = "helper";
    const string AllyOwnerPrefix = "ally:";

    public static GameSaveData LoadAndMigrateGameSave(string json, PartyData partyOverride, out bool migrated)
    {
        migrated = false;
        if (string.IsNullOrWhiteSpace(json))
            return null;

        GameSaveData data = JsonUtility.FromJson<GameSaveData>(json) ?? new GameSaveData();
        LegacyGameSaveData legacy = JsonUtility.FromJson<LegacyGameSaveData>(json);

        if (legacy == null || legacy.saveVersion != GameSaveData.CurrentVersion)
            migrated = true;

        PartyData migrationParty = HasPartyIds(data.party) ? data.party : partyOverride;
        if (!HasPartyIds(data.party) && HasPartyIds(partyOverride))
        {
            data.party = CloneParty(partyOverride);
            migrationParty = data.party;
            migrated = true;
        }

        if (NormalizeGameSave(data, migrationParty, legacy?.weapon?.equippedWeaponInstanceId))
            migrated = true;

        return data;
    }

    public static void NormalizeGameSaveForWrite(GameSaveData data)
    {
        NormalizeGameSave(data, data?.party, null);
    }

    public static bool ShouldPersistCharacterProgress(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return false;

        string trimmed = characterId.Trim();
        if (trimmed.StartsWith("ID.", StringComparison.Ordinal))
            return true;

        CharacterDatabase[] databases = Resources.FindObjectsOfTypeAll<CharacterDatabase>();
        for (int i = 0; i < databases.Length; i++)
        {
            CharacterDatabase database = databases[i];
            if (database != null && database.GetById(trimmed) != null)
                return true;
        }

        return false;
    }

    public static bool RemoveNonPersistentCharacterProgressEntries(CharacterProgressSaveFile file)
    {
        if (file?.entries == null)
            return false;

        bool changed = false;
        for (int i = file.entries.Count - 1; i >= 0; i--)
        {
            CharacterProgressEntry entry = file.entries[i];
            if (entry == null || !ShouldPersistCharacterProgress(entry.characterId))
            {
                file.entries.RemoveAt(i);
                changed = true;
            }
        }

        return changed;
    }

    static bool NormalizeGameSave(GameSaveData data, PartyData party, string legacyEquippedWeaponId)
    {
        if (data == null)
            return false;

        bool changed = false;
        if (data.saveVersion != GameSaveData.CurrentVersion)
        {
            data.saveVersion = GameSaveData.CurrentVersion;
            changed = true;
        }

        if (MigrateEquipmentOwners(data, party, legacyEquippedWeaponId))
            changed = true;

        if (MigrateAccessoryOwners(data, party))
            changed = true;

        if (MigrateWeaponAffixRuntimeState(data.inventory))
            changed = true;

        return changed;
    }

    static bool MigrateWeaponAffixRuntimeState(PlayerInventoryData inventory)
    {
        if (inventory?.slots == null)
            return false;

        bool changed = false;
        for (int i = 0; i < inventory.slots.Count; i++)
        {
            WeaponInstanceData weapon = inventory.slots[i]?.weaponInstance;
            if (weapon == null || weapon.shotCounter <= 0 || weapon.mainAffix == null)
                continue;

            string affixId = weapon.mainAffix.affixId;
            if (!string.Equals(affixId, "weapon.main.echo_chamber.v1", StringComparison.Ordinal) &&
                !string.Equals(affixId, "weapon.main.breach_chamber.v1", StringComparison.Ordinal))
                continue;

            WeaponAffixRuntimeStateData state = weapon.GetOrCreateAffixState(affixId);
            WeaponAffixRuntimeStateEntry progress = null;
            for (int entryIndex = 0; entryIndex < state.entries.Count; entryIndex++)
            {
                if (state.entries[entryIndex] != null && state.entries[entryIndex].key == "progress")
                {
                    progress = state.entries[entryIndex];
                    break;
                }
            }

            if (progress == null)
            {
                progress = new WeaponAffixRuntimeStateEntry { key = "progress" };
                state.entries.Add(progress);
            }

            progress.intValue = Mathf.Max(progress.intValue, weapon.shotCounter);
            weapon.shotCounter = 0;
            changed = true;
        }

        return changed;
    }

    static bool MigrateEquipmentOwners(GameSaveData data, PartyData party, string legacyEquippedWeaponId)
    {
        bool changed = false;
        data.equipment ??= new EquipmentSaveData();
        data.equipment.entries ??= new List<CharacterEquipmentSaveData>();

        var migratedEntries = new List<CharacterEquipmentSaveData>();
        for (int i = 0; i < data.equipment.entries.Count; i++)
        {
            CharacterEquipmentSaveData entry = data.equipment.entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.ownerId))
            {
                changed = true;
                continue;
            }

            string ownerId = ResolveModernOwnerId(entry.ownerId, party);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                changed = true;
                continue;
            }

            if (!string.Equals(ownerId, entry.ownerId, StringComparison.Ordinal))
                changed = true;

            UpsertEquipmentEntry(migratedEntries, ownerId, entry.equippedWeaponInstanceId);
        }

        string leaderOwnerId = ResolvePartyOwnerId(party, 0);
        if (!string.IsNullOrWhiteSpace(leaderOwnerId) &&
            !string.IsNullOrWhiteSpace(legacyEquippedWeaponId) &&
            !HasNonEmptyEquipmentEntry(migratedEntries, leaderOwnerId))
        {
            UpsertEquipmentEntry(migratedEntries, leaderOwnerId, legacyEquippedWeaponId);
            changed = true;
        }

        if (!AreEquipmentEntriesEquivalent(data.equipment.entries, migratedEntries))
            changed = true;

        data.equipment.entries = migratedEntries;
        return changed;
    }

    static bool MigrateAccessoryOwners(GameSaveData data, PartyData party)
    {
        if (data.accessories?.entries == null)
            return false;

        bool changed = false;
        var migratedEntries = new List<CharacterAccessoryLoadoutSaveData>();
        for (int i = 0; i < data.accessories.entries.Count; i++)
        {
            CharacterAccessoryLoadoutSaveData entry = data.accessories.entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.ownerId))
            {
                changed = true;
                continue;
            }

            string ownerId = ResolveModernOwnerId(entry.ownerId, party);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                changed = true;
                continue;
            }

            if (!string.Equals(ownerId, entry.ownerId, StringComparison.Ordinal))
                changed = true;

            MergeAccessoryEntry(migratedEntries, ownerId, entry);
        }

        if (!AreAccessoryEntriesEquivalent(data.accessories.entries, migratedEntries))
            changed = true;

        data.accessories.entries = migratedEntries;
        return changed;
    }

    static string ResolveModernOwnerId(string ownerId, PartyData party)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return null;

        if (CharacterEquipment.TryParseCharacterOwnerId(ownerId, out _))
            return ownerId;

        if (string.Equals(ownerId, PlayerOwnerId, StringComparison.Ordinal))
            return ResolvePartyOwnerId(party, 0);

        if (string.Equals(ownerId, HelperOwnerId, StringComparison.Ordinal))
            return ResolvePartyOwnerId(party, 3);

        if (!ownerId.StartsWith(AllyOwnerPrefix, StringComparison.Ordinal))
            return null;

        string role = ownerId.Substring(AllyOwnerPrefix.Length);
        if (string.Equals(role, "Player", StringComparison.Ordinal))
            return ResolvePartyOwnerId(party, 0);
        if (string.Equals(role, "PartySlot1", StringComparison.Ordinal))
            return ResolvePartyOwnerId(party, 1);
        if (string.Equals(role, "PartySlot2", StringComparison.Ordinal))
            return ResolvePartyOwnerId(party, 2);
        if (string.Equals(role, "Helper", StringComparison.Ordinal))
            return ResolvePartyOwnerId(party, 3);

        return null;
    }

    static string ResolvePartyOwnerId(PartyData party, int partyIndex)
    {
        if (party?.partyIds == null || partyIndex < 0 || partyIndex >= party.partyIds.Count)
            return null;

        string characterId = party.partyIds[partyIndex];
        return CharacterEquipment.BuildCharacterOwnerId(characterId);
    }

    static void UpsertEquipmentEntry(List<CharacterEquipmentSaveData> entries, string ownerId, string instanceId)
    {
        if (entries == null || string.IsNullOrWhiteSpace(ownerId))
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            CharacterEquipmentSaveData entry = entries[i];
            if (entry == null || !string.Equals(entry.ownerId, ownerId, StringComparison.Ordinal))
                continue;

            if (string.IsNullOrWhiteSpace(entry.equippedWeaponInstanceId) && !string.IsNullOrWhiteSpace(instanceId))
                entry.equippedWeaponInstanceId = instanceId;
            return;
        }

        entries.Add(new CharacterEquipmentSaveData
        {
            ownerId = ownerId,
            equippedWeaponInstanceId = instanceId
        });
    }

    static bool HasNonEmptyEquipmentEntry(List<CharacterEquipmentSaveData> entries, string ownerId)
    {
        if (entries == null || string.IsNullOrWhiteSpace(ownerId))
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            CharacterEquipmentSaveData entry = entries[i];
            if (entry != null &&
                string.Equals(entry.ownerId, ownerId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(entry.equippedWeaponInstanceId))
            {
                return true;
            }
        }

        return false;
    }

    static void MergeAccessoryEntry(
        List<CharacterAccessoryLoadoutSaveData> entries,
        string ownerId,
        CharacterAccessoryLoadoutSaveData source)
    {
        if (entries == null || string.IsNullOrWhiteSpace(ownerId) || source == null)
            return;

        CharacterAccessoryLoadoutSaveData target = null;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && string.Equals(entries[i].ownerId, ownerId, StringComparison.Ordinal))
            {
                target = entries[i];
                break;
            }
        }

        if (target == null)
        {
            target = new CharacterAccessoryLoadoutSaveData
            {
                ownerId = ownerId,
                slotCount = source.slotCount,
                equippedAccessories = new List<AccessoryInstanceData>()
            };
            entries.Add(target);
        }

        target.slotCount = Mathf.Max(target.slotCount, source.slotCount);
        target.equippedAccessories ??= new List<AccessoryInstanceData>();
        if (source.equippedAccessories == null)
            return;

        while (target.equippedAccessories.Count < source.equippedAccessories.Count)
            target.equippedAccessories.Add(null);

        for (int i = 0; i < source.equippedAccessories.Count; i++)
        {
            AccessoryInstanceData sourceInstance = source.equippedAccessories[i];
            if (sourceInstance == null || sourceInstance.IsEmpty)
                continue;

            AccessoryInstanceData targetInstance = target.equippedAccessories[i];
            if (targetInstance == null || targetInstance.IsEmpty)
                target.equippedAccessories[i] = sourceInstance.DeepClone();
        }
    }

    static bool AreEquipmentEntriesEquivalent(
        List<CharacterEquipmentSaveData> left,
        List<CharacterEquipmentSaveData> right)
    {
        if ((left?.Count ?? 0) != (right?.Count ?? 0))
            return false;

        if (left == null)
            return true;

        for (int i = 0; i < left.Count; i++)
        {
            CharacterEquipmentSaveData a = left[i];
            CharacterEquipmentSaveData b = right[i];
            if (a == null || b == null)
                return a == b;

            if (!string.Equals(a.ownerId, b.ownerId, StringComparison.Ordinal) ||
                !string.Equals(a.equippedWeaponInstanceId, b.equippedWeaponInstanceId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    static bool AreAccessoryEntriesEquivalent(
        List<CharacterAccessoryLoadoutSaveData> left,
        List<CharacterAccessoryLoadoutSaveData> right)
    {
        if ((left?.Count ?? 0) != (right?.Count ?? 0))
            return false;

        if (left == null)
            return true;

        for (int i = 0; i < left.Count; i++)
        {
            CharacterAccessoryLoadoutSaveData a = left[i];
            CharacterAccessoryLoadoutSaveData b = right[i];
            if (a == null || b == null)
                return a == b;

            if (!string.Equals(a.ownerId, b.ownerId, StringComparison.Ordinal) ||
                a.slotCount != b.slotCount ||
                !AreAccessoryListsEquivalent(a.equippedAccessories, b.equippedAccessories))
            {
                return false;
            }
        }

        return true;
    }

    static bool AreAccessoryListsEquivalent(List<AccessoryInstanceData> left, List<AccessoryInstanceData> right)
    {
        if ((left?.Count ?? 0) != (right?.Count ?? 0))
            return false;

        if (left == null)
            return true;

        for (int i = 0; i < left.Count; i++)
        {
            string leftId = left[i] != null ? left[i].instanceId : null;
            string rightId = right[i] != null ? right[i].instanceId : null;
            if (!string.Equals(leftId, rightId, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    static bool HasPartyIds(PartyData party)
    {
        if (party?.partyIds == null)
            return false;

        for (int i = 0; i < party.partyIds.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(party.partyIds[i]))
                return true;
        }

        return false;
    }

    static PartyData CloneParty(PartyData source)
    {
        var clone = new PartyData();
        if (source?.partyIds != null)
            clone.partyIds.AddRange(source.partyIds);
        return clone;
    }

    [Serializable]
    sealed class LegacyGameSaveData
    {
        public int saveVersion;
        public LegacyPlayerWeaponData weapon;
    }

    [Serializable]
    sealed class LegacyPlayerWeaponData
    {
        public string equippedWeaponInstanceId;
    }
}
