using System;

[Serializable]
public class RolledAffixData
{
    public string affixId;
    public bool hasPrimaryValue;
    public float primaryValue;
    public bool hasSecondaryValue;
    public float secondaryValue;

    public bool IsEmpty => string.IsNullOrWhiteSpace(affixId);

    public RolledAffixData DeepClone()
    {
        return new RolledAffixData
        {
            affixId = affixId,
            hasPrimaryValue = hasPrimaryValue,
            primaryValue = primaryValue,
            hasSecondaryValue = hasSecondaryValue,
            secondaryValue = secondaryValue
        };
    }
}
