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
    static string PartyPath(int slot) => Path.Combine(Dir, $"slot_{slot}_party.json");
    
    static string CharacterPath(int slot) => Path.Combine(Dir, $"slot_{slot}_character.json");
    

    // ---------- GAME ----------
    public static void SaveGame(GameSaveData data, int slot)
    {
        if (data == null) return;
        SaveDataMigration.NormalizeGameSaveForWrite(data);
        var json = JsonUtility.ToJson(data, true);
        WriteAtomic(GamePath(slot), json);
    }

    public static GameSaveData LoadGame(int slot)
    {
        var path = ResolveReadPath(GamePath(slot));
        if (!File.Exists(path)) return null;

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        PartyData partyOnly = LoadPartyOnly(slot);
        GameSaveData data = SaveDataMigration.LoadAndMigrateGameSave(json, partyOnly, out bool migrated);
        if (data != null && migrated)
            SaveGame(data, slot);

        return data;
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
