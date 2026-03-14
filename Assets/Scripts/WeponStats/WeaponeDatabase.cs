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

        foreach (var w in weapons)
        {
            if (!w) continue;

            // ใช้ itemId จาก ItemDefinition
            var id = w.itemId;

            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning($"[WeaponDatabase] Missing itemId: {w.name}", this);
                continue;
            }

            if (_lookup.ContainsKey(id))
            {
                Debug.LogWarning($"[WeaponDatabase] Duplicate itemId: {id} ({w.name})", this);
                continue;
            }

            // กันพลาด: ถ้าเผลอใส่ itemType ไม่ใช่ Weapon
            if (w.itemType != ItemType.Weapon)
            {
                Debug.LogWarning($"[WeaponDatabase] itemType is not Weapon: {id} ({w.itemType})", this);
            }

            _lookup.Add(id, w);
        }
    }

    public GunConfig GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (_lookup == null) BuildLookup();
        _lookup.TryGetValue(id, out var def);
        return def;
    }

    public bool TryGetById(string id, out GunConfig def)
    {
        def = null;
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (_lookup == null) BuildLookup();
        return _lookup.TryGetValue(id, out def);
    }

    // optional: เอาไว้เช็คว่ามี id นี้ไหม
    public bool Contains(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (_lookup == null) BuildLookup();
        return _lookup.ContainsKey(id);
    }
}