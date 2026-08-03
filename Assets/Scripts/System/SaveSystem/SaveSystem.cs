using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    // ---------- paths ----------
    static string Dir
    {
        get
        {
#if UNITY_EDITOR
            var projectRoot = Directory.GetParent(Application.dataPath);
            var baseDir = projectRoot != null ? projectRoot.FullName : Application.dataPath;
            return Path.Combine(baseDir, "Savedata");
#else
            return Application.persistentDataPath;
#endif
        }
    }

    public static string CurrentDirectory => Dir;

    static string GamePath(int slot)  => Path.Combine(Dir, $"slot_{slot}_game.json");
    static string InventoryPath(int slot) => Path.Combine(Dir, $"slot_{slot}_inventory.json");
    static string EquipmentPath(int slot) => Path.Combine(Dir, $"slot_{slot}_equipment.json");
    static string AccessoriesPath(int slot) => Path.Combine(Dir, $"slot_{slot}_accessories.json");
    static string PartyPath(int slot) => Path.Combine(Dir, $"slot_{slot}_party.json");
    static string StageProgressPath(int slot) => Path.Combine(Dir, $"slot_{slot}_stage_progress.json");
    
    static string CharacterPath(int slot) => Path.Combine(Dir, $"slot_{slot}_character.json");

    static IEnumerable<string> GetSlotPaths(int slot)
    {
        yield return GamePath(slot);
        yield return InventoryPath(slot);
        yield return EquipmentPath(slot);
        yield return AccessoriesPath(slot);
        yield return PartyPath(slot);
        yield return CharacterPath(slot);
        yield return StageProgressPath(slot);
    }

    public static bool HasAnyData(int slot)
    {
        if (slot < 0) slot = 0;

        foreach (string path in GetSlotPaths(slot))
        {
            if (File.Exists(path))
                return true;

#if UNITY_EDITOR
            string legacyPath = Path.Combine(Application.persistentDataPath, Path.GetFileName(path));
            if (File.Exists(legacyPath))
                return true;
#endif
        }

        return false;
    }

    public static bool DeleteSlot(int slot)
    {
        if (slot < 0) slot = 0;
        bool succeeded = true;

        foreach (string path in GetSlotPaths(slot))
        {
            succeeded &= TryDelete(path);
            succeeded &= TryDelete(path + ".tmp");

#if UNITY_EDITOR
            string legacyPath = Path.Combine(Application.persistentDataPath, Path.GetFileName(path));
            succeeded &= TryDelete(legacyPath);
            succeeded &= TryDelete(legacyPath + ".tmp");
#endif
        }

        return succeeded;
    }

    static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);

            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveSystem] Failed to delete save file '{path}': {ex.Message}");
            return false;
        }
    }
    

    // ---------- GAME ----------
    public static void SaveGame(GameSaveData data, int slot)
    {
        if (data == null) return;
        if (slot < 0) slot = 0;

        SaveDataMigration.NormalizeGameSaveForWrite(data);
        UpdatePartyOnly(data.party, slot);
        if (data.inventory != null)
            WriteJson(InventoryPath(slot), data.inventory);
        if (data.equipment != null)
            WriteJson(EquipmentPath(slot), data.equipment);
        if (data.accessories != null)
            WriteJson(AccessoriesPath(slot), data.accessories);
    }

    public static GameSaveData LoadGame(int slot)
    {
        if (slot < 0) slot = 0;

        string inventoryPath = ResolveReadPath(InventoryPath(slot));
        string equipmentPath = ResolveReadPath(EquipmentPath(slot));
        string accessoriesPath = ResolveReadPath(AccessoriesPath(slot));
        bool hasInventory = File.Exists(inventoryPath);
        bool hasEquipment = File.Exists(equipmentPath);
        bool hasAccessories = File.Exists(accessoriesPath);

        PartyData party = LoadPartyOnly(slot);
        GameSaveData legacy = null;
        if (!hasInventory || !hasEquipment || !hasAccessories)
            legacy = LoadLegacyGame(slot, party);

        if (!hasInventory && !hasEquipment && !hasAccessories && legacy == null)
            return null;

        var data = new GameSaveData
        {
            inventory = hasInventory ? ReadJson<PlayerInventoryData>(inventoryPath) : legacy?.inventory,
            equipment = hasEquipment ? ReadJson<EquipmentSaveData>(equipmentPath) : legacy?.equipment,
            accessories = hasAccessories ? ReadJson<AccessoryLoadoutSaveData>(accessoriesPath) : legacy?.accessories,
            party = party ?? legacy?.party ?? new PartyData()
        };

        SaveDataMigration.NormalizeGameSaveForWrite(data);

        if (!hasInventory && data.inventory != null)
            SaveInventory(data.inventory, slot);
        if (!hasEquipment && data.equipment != null)
            SaveEquipment(data.equipment, slot);
        if (!hasAccessories && data.accessories != null)
            SaveAccessories(data.accessories, slot);
        if (party == null && data.party != null)
            UpdatePartyOnly(data.party, slot);

        return data;
    }

    public static void SaveInventory(PlayerInventoryData data, int slot)
    {
        if (data == null) return;
        if (slot < 0) slot = 0;
        WriteJson(InventoryPath(slot), data);
    }

    public static PlayerInventoryData LoadInventory(int slot)
    {
        if (slot < 0) slot = 0;
        string path = ResolveReadPath(InventoryPath(slot));
        if (File.Exists(path))
            return ReadJson<PlayerInventoryData>(path);

        GameSaveData legacy = LoadLegacyGame(slot, LoadPartyOnly(slot));
        if (legacy?.inventory != null)
            SaveInventory(legacy.inventory, slot);
        return legacy?.inventory;
    }

    public static void SaveEquipment(EquipmentSaveData data, int slot)
    {
        if (data == null) return;
        if (slot < 0) slot = 0;

        var aggregate = new GameSaveData
        {
            equipment = data,
            party = LoadPartyOnly(slot) ?? new PartyData()
        };
        SaveDataMigration.NormalizeGameSaveForWrite(aggregate);
        WriteJson(EquipmentPath(slot), aggregate.equipment);
    }

    public static EquipmentSaveData LoadEquipment(int slot)
    {
        if (slot < 0) slot = 0;
        string path = ResolveReadPath(EquipmentPath(slot));
        if (File.Exists(path))
            return ReadJson<EquipmentSaveData>(path);

        PartyData party = LoadPartyOnly(slot);
        GameSaveData legacy = LoadLegacyGame(slot, party);
        if (legacy?.equipment != null)
            SaveEquipment(legacy.equipment, slot);
        return legacy?.equipment;
    }

    public static void SaveAccessories(AccessoryLoadoutSaveData data, int slot)
    {
        if (data == null) return;
        if (slot < 0) slot = 0;

        var aggregate = new GameSaveData
        {
            accessories = data,
            party = LoadPartyOnly(slot) ?? new PartyData()
        };
        SaveDataMigration.NormalizeGameSaveForWrite(aggregate);
        WriteJson(AccessoriesPath(slot), aggregate.accessories);
    }

    public static AccessoryLoadoutSaveData LoadAccessories(int slot)
    {
        if (slot < 0) slot = 0;
        string path = ResolveReadPath(AccessoriesPath(slot));
        if (File.Exists(path))
            return ReadJson<AccessoryLoadoutSaveData>(path);

        PartyData party = LoadPartyOnly(slot);
        GameSaveData legacy = LoadLegacyGame(slot, party);
        if (legacy?.accessories != null)
            SaveAccessories(legacy.accessories, slot);
        return legacy?.accessories;
    }

    public static bool HasGameData(int slot)
    {
        if (slot < 0) slot = 0;
        return File.Exists(ResolveReadPath(InventoryPath(slot))) ||
               File.Exists(ResolveReadPath(EquipmentPath(slot))) ||
               File.Exists(ResolveReadPath(AccessoriesPath(slot))) ||
               File.Exists(ResolveReadPath(GamePath(slot)));
    }

    static GameSaveData LoadLegacyGame(int slot, PartyData partyOnly)
    {
        var path = ResolveReadPath(GamePath(slot));
        if (!File.Exists(path)) return null;

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return SaveDataMigration.LoadAndMigrateGameSave(json, partyOnly, out _);
    }

    static void WriteJson<T>(string path, T data)
    {
        var json = JsonUtility.ToJson(data, true);
        WriteAtomic(path, json);
    }

    static T ReadJson<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<T>(json);
    }

    // ---------- PARTY ONLY ----------
    public static void UpdatePartyOnly(PartyData party, int slot)
    {
        if (party == null) return;
        var json = JsonUtility.ToJson(party, true);
        WriteAtomic(PartyPath(slot), json);
    }

    public static PartyData LoadPartyOnly(int slot)
    {
        var path = ResolveReadPath(PartyPath(slot));
        if (!File.Exists(path)) return null;

        var json = File.ReadAllText(path);
        return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<PartyData>(json);
        
    }
    public static string GetPartyMemberId(int slot, int index)
    {
        PartyData party = LoadPartyOnly(slot);
        if (party?.partyIds == null || index < 0 || index >= party.partyIds.Count)
            return null;

        return party.partyIds[index];
    }

    public static int LoadStageProgress(int slot, string stageId)
    {
        if (slot < 0) slot = 0;
        string path = ResolveReadPath(StageProgressPath(slot));
        StageProgressSaveFile file = ReadJson<StageProgressSaveFile>(path) ?? new StageProgressSaveFile();
        return file.GetProgress(stageId);
    }

    public static void SaveStageProgress(int slot, string stageId, int progressCount)
    {
        if (string.IsNullOrWhiteSpace(stageId)) return;
        if (slot < 0) slot = 0;

        string path = StageProgressPath(slot);
        StageProgressSaveFile file = ReadJson<StageProgressSaveFile>(ResolveReadPath(path)) ?? new StageProgressSaveFile();
        file.SetProgress(stageId, progressCount);
        WriteJson(path, file);
    }

    public static void ResetStageProgress(int slot)
    {
        if (slot < 0) slot = 0;
        WriteJson(StageProgressPath(slot), new StageProgressSaveFile());
    }


    // ---------- atomic write ----------
    static void WriteAtomic(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS
        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
#else
        // fallback
        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
#endif
    }

    #region CharacterProgressData

    //---------- CharacterProgressData Save -------------------//


    public static void SaveCharacterProgress(int slot, string characterId, CharacterProgressData data)
    {
        if (string.IsNullOrEmpty(characterId) || data == null) return;
        if (!SaveDataMigration.ShouldPersistCharacterProgress(characterId)) return;
        if (slot < 0) slot = 0;

        var file = LoadCharacterProgressFile(slot);
        if (file.entries == null) file.entries = new List<CharacterProgressEntry>();
        SaveDataMigration.RemoveNonPersistentCharacterProgressEntries(file);

        // หา entry เดิม
        var entry = file.entries.Find(e => e != null && e.characterId == characterId);

        if (entry == null)
        {
            entry = new CharacterProgressEntry { characterId = characterId, progress = data };
            file.entries.Add(entry);
        }
        else
        {
            entry.progress = data; // หรือคัดลอกทีละ field ก็ได้
        }

        var json = JsonUtility.ToJson(file, true);
        WriteAtomic(CharacterPath(slot), json);
    }

// โหลด progress ของ “ตัวเดียว”
    public static CharacterProgressData LoadCharacterProgress(int slot, string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return null;
        if (!SaveDataMigration.ShouldPersistCharacterProgress(characterId)) return null;
        if (slot < 0) slot = 0;

        var file = LoadCharacterProgressFile(slot);
        if (file?.entries == null) return null;

        var entry = file.entries.Find(e => e != null && e.characterId == characterId);
        return entry?.progress;
    }

// โหลดทั้งไฟล์ (เผื่อคุณอยากทำ cache)
    static CharacterProgressSaveFile LoadCharacterProgressFile(int slot)
    {
        var path = ResolveReadPath(CharacterPath(slot));
        if (!File.Exists(path)) return new CharacterProgressSaveFile();

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json)) return new CharacterProgressSaveFile();

        var file = JsonUtility.FromJson<CharacterProgressSaveFile>(json) ?? new CharacterProgressSaveFile();
        if (SaveDataMigration.RemoveNonPersistentCharacterProgressEntries(file))
        {
            var migratedJson = JsonUtility.ToJson(file, true);
            WriteAtomic(CharacterPath(slot), migratedJson);
        }

        return file;
    }

    static string ResolveReadPath(string preferredPath)
    {
        if (File.Exists(preferredPath))
            return preferredPath;

#if UNITY_EDITOR
        var legacyPath = Path.Combine(Application.persistentDataPath, Path.GetFileName(preferredPath));
        if (!File.Exists(legacyPath))
            return preferredPath;

        TryMigrateLegacyFile(legacyPath, preferredPath);

        return File.Exists(preferredPath) ? preferredPath : legacyPath;
#else
        return preferredPath;
#endif
    }

#if UNITY_EDITOR
    static void TryMigrateLegacyFile(string legacyPath, string preferredPath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(preferredPath));
            File.Copy(legacyPath, preferredPath, overwrite: false);
            Debug.Log($"[SaveSystem] Migrated save file to {preferredPath}");
        }
        catch (IOException)
        {
            // Leave the legacy file in place if another process already created the new file.
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SaveSystem] Failed to migrate save file from {legacyPath} to {preferredPath}: {ex.Message}");
        }
    }
#endif

    #endregion

    
    
    
    
    
}
