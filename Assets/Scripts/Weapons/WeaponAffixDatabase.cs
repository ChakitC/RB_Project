using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Weapons/Weapon Affix Database", fileName = "WeaponAffixDatabase")]
public class WeaponAffixDatabase : ScriptableObject
{
    static readonly List<WeaponAffixDatabase> LoadedDatabases = new();

    [SerializeField] private List<WeaponAffixDefinition> affixes = new();

    readonly Dictionary<string, WeaponAffixDefinition> _lookup = new();

    public IReadOnlyList<WeaponAffixDefinition> Affixes => affixes;

    void OnEnable()
    {
        BuildLookup();

        if (!LoadedDatabases.Contains(this))
            LoadedDatabases.Add(this);
    }

    void OnDisable()
    {
        LoadedDatabases.Remove(this);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        BuildLookup();
    }
#endif

    void BuildLookup()
    {
        _lookup.Clear();

        if (affixes == null)
            return;

        for (int i = 0; i < affixes.Count; i++)
        {
            var affix = affixes[i];
            if (!affix || string.IsNullOrWhiteSpace(affix.affixId))
                continue;

            if (_lookup.ContainsKey(affix.affixId))
            {
                Debug.LogWarning($"[WeaponAffixDatabase] Duplicate affixId: {affix.affixId}", this);
                continue;
            }

            _lookup.Add(affix.affixId, affix);
        }
    }

    public WeaponAffixDefinition GetById(string affixId)
    {
        if (string.IsNullOrWhiteSpace(affixId))
            return null;

        if (_lookup.Count == 0)
            BuildLookup();

        _lookup.TryGetValue(affixId, out var affix);
        return affix;
    }

    public static WeaponAffixDefinition GetLoadedAffixById(string affixId)
    {
        if (string.IsNullOrWhiteSpace(affixId))
            return null;

        RefreshLoadedDatabaseCache();

        for (int i = 0; i < LoadedDatabases.Count; i++)
        {
            var database = LoadedDatabases[i];
            if (database == null)
                continue;

            var affix = database.GetById(affixId);
            if (affix != null)
                return affix;
        }

        return null;
    }

    public void GetCandidates(List<WeaponAffixDefinition> buffer, WeaponAffixSlot slot, WeaponType weaponType, ISet<string> excludedIds = null)
    {
        GetCandidates(buffer, slot, null, weaponType, excludedIds);
    }

    public void GetCandidates(List<WeaponAffixDefinition> buffer, WeaponAffixSlot slot, GunConfig weapon, ISet<string> excludedIds = null)
    {
        GetCandidates(buffer, slot, weapon, weapon != null ? weapon.WeaponType : default, excludedIds);
    }

    void GetCandidates(List<WeaponAffixDefinition> buffer, WeaponAffixSlot slot, GunConfig weapon, WeaponType weaponType, ISet<string> excludedIds)
    {
        if (buffer == null)
            return;

        buffer.Clear();

        if (affixes == null)
            return;

        for (int i = 0; i < affixes.Count; i++)
        {
            var affix = affixes[i];
            if (!affix || affix.slot != slot || (weapon != null ? !affix.SupportsWeapon(weapon) : !affix.SupportsWeaponType(weaponType)))
                continue;

            if (excludedIds != null && !string.IsNullOrWhiteSpace(affix.affixId) && excludedIds.Contains(affix.affixId))
                continue;

            buffer.Add(affix);
        }
    }

    static void RefreshLoadedDatabaseCache()
    {
        LoadedDatabases.RemoveAll(database => database == null);

        if (LoadedDatabases.Count > 0)
            return;

        var discoveredDatabases = Resources.FindObjectsOfTypeAll<WeaponAffixDatabase>();
        for (int i = 0; i < discoveredDatabases.Length; i++)
        {
            var database = discoveredDatabases[i];
            if (database != null && !LoadedDatabases.Contains(database))
                LoadedDatabases.Add(database);
        }
    }
}
