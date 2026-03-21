using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Weapons/Weapon Affix Database", fileName = "WeaponAffixDatabase")]
public class WeaponAffixDatabase : ScriptableObject
{
    [SerializeField] private List<WeaponAffixDefinition> affixes = new();

    readonly Dictionary<string, WeaponAffixDefinition> _lookup = new();

    public IReadOnlyList<WeaponAffixDefinition> Affixes => affixes;

    void OnEnable()
    {
        BuildLookup();
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

    public void GetCandidates(List<WeaponAffixDefinition> buffer, WeaponAffixSlot slot, WeaponType weaponType, ISet<string> excludedIds = null)
    {
        if (buffer == null)
            return;

        buffer.Clear();

        if (affixes == null)
            return;

        for (int i = 0; i < affixes.Count; i++)
        {
            var affix = affixes[i];
            if (!affix || affix.slot != slot || !affix.SupportsWeaponType(weaponType))
                continue;

            if (excludedIds != null && !string.IsNullOrWhiteSpace(affix.affixId) && excludedIds.Contains(affix.affixId))
                continue;

            buffer.Add(affix);
        }
    }
}
