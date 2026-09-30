using System.Collections.Generic;

/// <summary>Shared pools live for their owner's lifetime, independently of loadout rebuilds.</summary>
internal sealed class SkillChargeRegistry
{
    readonly Dictionary<SkillGemDefinition, SkillChargeState> sharedCharges = new();

    public SkillChargeState GetOrCreateCharges(SkillGemDefinition skillDef)
    {
        if (skillDef == null)
            return null;

        if (!sharedCharges.TryGetValue(skillDef, out var charges))
        {
            charges = new SkillChargeState();
            sharedCharges.Add(skillDef, charges);
        }
        return charges;
    }

    public void ResetAllChargesToFull()
    {
        foreach (var charges in sharedCharges.Values)
            charges?.ResetToFull();
    }
}
