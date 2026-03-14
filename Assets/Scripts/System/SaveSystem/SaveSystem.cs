using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    // ---------- paths ----------
    static string Dir => Application.persistentDataPath;

    static string GamePath(int slot)  => Path.Combine(Dir, $"slot_{slot}_game.json");
    static string PartyPath(int slot) => Path.Combine(Dir, $"slot_{slot}_party.json");
    
    static string CharacterPath(int slot) => Path.Combine(Dir, $"slot_{slot}_character.json");
    

    // ---------- GAME ----------
    public static void SaveGame(GameSaveData data, int slot)
    {
        if (data == null) return;
        var json = JsonUtility.ToJson(data, true);
        WriteAtomic(GamePath(slot), json);
    }

    public static GameSaveData LoadGame(int slot)
    {
        var path = GamePath(slot);
        if (!File.Exists(path)) return null;

        var json = File.ReadAllText(path);
        return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<GameSaveData>(json);
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
        var path = PartyPath(slot);
        if (!File.Exists(path)) return null;

        var json = File.ReadAllText(path);
        return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<PartyData>(json);
        
    }
    public static string GetPartyMemberId(int slot, int index)
    {
        
        var path = PartyPath(slot);
        Debug.Log($"[GetPartyMemberId] path={path}");

        var json = File.Exists(path) ? File.ReadAllText(path) : "(no file)";
        Debug.Log($"[GetPartyMemberId] json=\n{json}");

        var party = JsonUtility.FromJson<PartyData>(json);
        Debug.Log($"count={(party?.partyIds==null ? -1 : party.partyIds.Count)}");

        if (party?.partyIds != null)
        {
            for (int i = 0; i < party.partyIds.Count; i++)
            {
                var v = party.partyIds[i];
                Debug.Log($"partyIds[{i}] = {(v==null ? "NULL" : $"'{v}' len={v.Length}")}");
            }
        }
        
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
        if (slot < 0) slot = 0;

        var file = LoadCharacterProgressFile(slot);
        if (file.entries == null) file.entries = new List<CharacterProgressEntry>();

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
        if (slot < 0) slot = 0;

        var file = LoadCharacterProgressFile(slot);
        if (file?.entries == null) return null;

        var entry = file.entries.Find(e => e != null && e.characterId == characterId);
        return entry?.progress;
    }

// โหลดทั้งไฟล์ (เผื่อคุณอยากทำ cache)
    static CharacterProgressSaveFile LoadCharacterProgressFile(int slot)
    {
        var path = CharacterPath(slot);
        if (!File.Exists(path)) return new CharacterProgressSaveFile();

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json)) return new CharacterProgressSaveFile();

        var file = JsonUtility.FromJson<CharacterProgressSaveFile>(json);
        return file ?? new CharacterProgressSaveFile();
    }

    #endregion

    
    
    
    
    
}