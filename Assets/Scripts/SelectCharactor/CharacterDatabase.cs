using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Characters/Database", fileName = "CharacterDatabase")]
public class CharacterDatabase : ScriptableObject
{
    public List<CharacterStats> characters = new();
    [SerializeField] private List<CharacterUnlockEntry> unlockEntries = new();

    Dictionary<string, CharacterStats> _lookup;
    Dictionary<string, CharacterUnlockEntry> _unlockLookup;

    public IReadOnlyList<CharacterUnlockEntry> UnlockEntries => unlockEntries;

    void OnEnable() => BuildLookup();

    void OnValidate() => BuildLookup();

    void BuildLookup()
    {
        _lookup = new Dictionary<string, CharacterStats>();
        _unlockLookup = new Dictionary<string, CharacterUnlockEntry>();

        foreach (var c in characters)
        {
            if (!c) continue;

            if (string.IsNullOrWhiteSpace(c.characterId))
            {
                Debug.LogWarning($"[CharacterDatabase] Missing characterId: {c.name}", this);
                continue;
            }

            if (_lookup.ContainsKey(c.characterId))
            {
                Debug.LogWarning($"[CharacterDatabase] Duplicate id: {c.characterId}", this);
                continue;
            }

            _lookup.Add(c.characterId, c);
        }

        if (unlockEntries == null)
            return;

        for (int i = 0; i < unlockEntries.Count; i++)
        {
            CharacterUnlockEntry entry = unlockEntries[i];
            if (entry == null)
                continue;

            string id = entry.CharacterId;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (_unlockLookup.ContainsKey(id))
            {
                Debug.LogWarning($"[CharacterDatabase] Duplicate unlock id: {id}", this);
                continue;
            }

            _unlockLookup.Add(id, entry);
        }
    }

    public CharacterStats GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (_lookup == null) BuildLookup();
        _lookup.TryGetValue(id, out var def);
        return def;
    }

    public bool TryGetUnlockEntry(string id, out CharacterUnlockEntry entry)
    {
        entry = null;

        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (_unlockLookup == null)
            BuildLookup();

        return _unlockLookup != null && _unlockLookup.TryGetValue(id.Trim(), out entry);
    }
}

[Serializable]
public sealed class CharacterUnlockEntry
{
    [SerializeField] private CharacterStats character;
    [SerializeField] private string characterIdOverride;
    [SerializeField] private bool unlockedByDefault;
    [SerializeField, Min(0)] private int goldCost;
    [SerializeField, TextArea] private string lockedMessage;

    public CharacterStats Character => character;
    public string CharacterId => ResolveCharacterId();
    public bool UnlockedByDefault => unlockedByDefault;
    public int GoldCost => Mathf.Max(0, goldCost);
    public string LockedMessage => lockedMessage;

    public string DisplayName
    {
        get
        {
            if (character != null && !string.IsNullOrWhiteSpace(character.characterName))
                return character.characterName;

            string id = CharacterId;
            return string.IsNullOrWhiteSpace(id) ? "Character" : id;
        }
    }

    string ResolveCharacterId()
    {
        if (!string.IsNullOrWhiteSpace(characterIdOverride))
            return characterIdOverride.Trim();

        return character != null ? character.characterId : string.Empty;
    }
}

public static class CharacterUnlockService
{
    public static event Action<string> CharacterUnlocked;

    public static bool IsUnlockedForSelection(CharacterStats character)
    {
        if (character == null)
            return false;

        return IsUnlockedForSelection(character.characterId);
    }

    public static bool IsUnlockedForSelection(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return false;

        characterId = characterId.Trim();

        if (!TryResolveEntry(characterId, out CharacterUnlockEntry entry))
            return true;

        if (entry.UnlockedByDefault)
            return true;

        return SaveManager.Instance != null && SaveManager.Instance.LoadCharacterUnlockState(characterId);
    }

    public static bool CanUnlock(string characterId, PlayerInventory payerInventory, out string reason)
    {
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(characterId))
        {
            reason = "Missing character id.";
            return false;
        }

        characterId = characterId.Trim();

        if (IsUnlockedForSelection(characterId))
        {
            reason = "Already unlocked.";
            return false;
        }

        if (!TryResolveEntry(characterId, out CharacterUnlockEntry entry))
        {
            reason = "Character is not unlockable.";
            return false;
        }

        int cost = entry.GoldCost;
        if (cost <= 0)
            return true;

        PlayerInventory inventory = payerInventory != null ? payerInventory : ResolveInventory();
        if (inventory == null)
        {
            reason = "Missing player inventory.";
            return false;
        }

        if (inventory.Gold < cost)
        {
            reason = $"Need {cost} gold.";
            return false;
        }

        return true;
    }

    public static bool TryUnlockForSelection(string characterId, PlayerInventory payerInventory, out string reason)
    {
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(characterId))
        {
            reason = "Missing character id.";
            return false;
        }

        characterId = characterId.Trim();

        if (IsUnlockedForSelection(characterId))
        {
            reason = "Already unlocked.";
            return true;
        }

        if (!CanUnlock(characterId, payerInventory, out reason))
            return false;

        if (SaveManager.Instance == null)
        {
            reason = "Missing save manager.";
            return false;
        }

        int cost = GetGoldCost(characterId);
        if (cost > 0)
        {
            PlayerInventory inventory = payerInventory != null ? payerInventory : ResolveInventory();
            if (inventory == null)
            {
                reason = "Missing player inventory.";
                return false;
            }

            if (!inventory.SpendGold(cost))
            {
                reason = $"Need {cost} gold.";
                return false;
            }
        }

        SaveManager.Instance.SaveCharacterUnlockState(characterId, true);
        SaveManager.Instance.SaveInventoryOnly();

        CharacterUnlocked?.Invoke(characterId);
        return true;
    }

    public static int GetGoldCost(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return 0;

        return TryResolveEntry(characterId.Trim(), out CharacterUnlockEntry entry) ? entry.GoldCost : 0;
    }

    public static string GetDisplayName(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return "Character";

        characterId = characterId.Trim();

        if (TryResolveEntry(characterId, out CharacterUnlockEntry entry))
            return entry.DisplayName;

        if (TryResolveCharacter(characterId, out CharacterStats character) &&
            !string.IsNullOrWhiteSpace(character.characterName))
            return character.characterName;

        return characterId;
    }

    public static string GetLockedMessage(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return string.Empty;

        return TryResolveEntry(characterId.Trim(), out CharacterUnlockEntry entry)
            ? entry.LockedMessage
            : string.Empty;
    }

    static bool TryResolveEntry(string characterId, out CharacterUnlockEntry entry)
    {
        entry = null;

        CharacterDatabase[] databases = Resources.FindObjectsOfTypeAll<CharacterDatabase>();
        for (int i = 0; i < databases.Length; i++)
        {
            CharacterDatabase database = databases[i];
            if (database != null && database.TryGetUnlockEntry(characterId, out entry))
                return true;
        }

        return false;
    }

    static bool TryResolveCharacter(string characterId, out CharacterStats character)
    {
        character = null;

        CharacterDatabase[] databases = Resources.FindObjectsOfTypeAll<CharacterDatabase>();
        for (int i = 0; i < databases.Length; i++)
        {
            CharacterDatabase database = databases[i];
            if (database == null)
                continue;

            character = database.GetById(characterId);
            if (character != null)
                return true;
        }

        return false;
    }

    static PlayerInventory ResolveInventory()
    {
        return UnityEngine.Object.FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
    }
}
