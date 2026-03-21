using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Weapons/Weapon Database", fileName = "WeaponDatabase")]
public class WeaponDatabase : ScriptableObject
{
    [Header("All weapon definitions (GunConfig assets)")]
    public List<GunConfig> weapons = new();

    Dictionary<string, GunConfig> _lookup;

    void OnEnable() => BuildLookup();

#if UNITY_EDITOR
    void OnValidate() => BuildLookup();
#endif

    void BuildLookup()
    {
        _lookup = new Dictionary<string, GunConfig>();

        foreach (var weapon in weapons)
        {
            if (!weapon)
                continue;

            string id = ResolveWeaponId(weapon);
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning($"[WeaponDatabase] Missing weapon id: {weapon.name}", this);
                continue;
            }

            if (_lookup.ContainsKey(id))
            {
                Debug.LogWarning($"[WeaponDatabase] Duplicate weapon id: {id} ({weapon.name})", this);
                continue;
            }

            if (weapon.itemType != ItemType.Weapon)
                Debug.LogWarning($"[WeaponDatabase] itemType is not Weapon: {id} ({weapon.itemType})", this);

            _lookup.Add(id, weapon);
        }
    }

    public GunConfig GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        if (_lookup == null)
            BuildLookup();

        _lookup.TryGetValue(id, out var def);
        return def;
    }

    public bool TryGetById(string id, out GunConfig def)
    {
        def = null;

        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (_lookup == null)
            BuildLookup();

        return _lookup.TryGetValue(id, out def);
    }

    public bool Contains(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (_lookup == null)
            BuildLookup();

        return _lookup.ContainsKey(id);
    }

    static string ResolveWeaponId(GunConfig weapon)
    {
        if (!weapon)
            return null;

        if (!string.IsNullOrWhiteSpace(weapon.itemId))
            return weapon.itemId;

        return weapon.name;
    }
}
