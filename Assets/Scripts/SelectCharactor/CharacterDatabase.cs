using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Characters/Database", fileName = "CharacterDatabase")]
public class CharacterDatabase : ScriptableObject
{
    public List<CharacterStats> characters = new();

    Dictionary<string, CharacterStats> _lookup;

    void OnEnable() => BuildLookup();

    void BuildLookup()
    {
        _lookup = new Dictionary<string, CharacterStats>();

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
    }

    public CharacterStats GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (_lookup == null) BuildLookup();
        _lookup.TryGetValue(id, out var def);
        return def;
    }
}