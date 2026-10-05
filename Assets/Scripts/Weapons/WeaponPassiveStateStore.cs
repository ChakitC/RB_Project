/// <summary>Lazy weapon-owned storage. Legacy progress is claimed once by the original rolled grant.</summary>
public sealed class WeaponPassiveStateStore : IPassiveStateStore
{
    readonly WeaponInstanceData weapon;
    readonly string grantId;
    readonly string legacyId;
    public WeaponPassiveStateStore(WeaponInstanceData weapon, string grantId, string legacyId)
    { this.weapon = weapon; this.grantId = grantId; this.legacyId = legacyId; }
    WeaponAffixRuntimeStateData Find(bool create)
    {
        if (weapon.affixRuntimeStates != null)
        {
            foreach (var state in weapon.affixRuntimeStates) if (state != null && state.affixId == grantId) return state;
            foreach (var state in weapon.affixRuntimeStates)
                if (!string.IsNullOrEmpty(legacyId) && state != null && state.affixId == legacyId)
                {
                    if (!create) return state;
                    state.affixId = grantId; return state;
                }
        }
        return create ? weapon.GetOrCreateAffixState(grantId, 2) : null;
    }
    public bool TryRead(string key, out float value)
    {
        var state = Find(false);
        if (state?.entries != null) foreach (var entry in state.entries)
            if (entry != null && entry.key == key)
            { value = state.schemaVersion < 2 ? entry.intValue : entry.floatValue; return true; }
        value = 0; return false;
    }
    public void Write(string key, float value)
    {
        var state = Find(true);
        state.entries ??= new System.Collections.Generic.List<WeaponAffixRuntimeStateEntry>();
        if (state.schemaVersion < 2)
        {
            foreach (var old in state.entries) if (old != null) old.floatValue = old.intValue;
            state.schemaVersion = 2;
        }
        foreach (var entry in state.entries)
            if (entry != null && entry.key == key) { entry.floatValue = value; entry.intValue = (int)value; return; }
        state.entries.Add(new WeaponAffixRuntimeStateEntry { key = key, floatValue = value, intValue = (int)value });
    }
}
