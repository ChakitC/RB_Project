using System;
using System.Collections.Generic;

[Serializable]
public sealed class WeaponAffixRuntimeStateData
{
    public string affixId;
    public int schemaVersion = 1;
    public List<WeaponAffixRuntimeStateEntry> entries = new();
    public WeaponAffixRuntimeStateData DeepClone()
    {
        var clone = new WeaponAffixRuntimeStateData { affixId = affixId, schemaVersion = schemaVersion };
        if (entries != null) for (int i = 0; i < entries.Count; i++) if (entries[i] != null) clone.entries.Add(entries[i].DeepClone());
        return clone;
    }
}

[Serializable]
public sealed class WeaponAffixRuntimeStateEntry
{
    public string key;
    public int intValue;
    public float floatValue;
    public bool boolValue;
    public string stringValue;
    public WeaponAffixRuntimeStateEntry DeepClone() => new WeaponAffixRuntimeStateEntry
    { key = key, intValue = intValue, floatValue = floatValue, boolValue = boolValue, stringValue = stringValue };
}
