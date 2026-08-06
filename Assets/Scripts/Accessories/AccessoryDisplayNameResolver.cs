using System.Text;
using UnityEngine;

public static class AccessoryDisplayNameResolver
{
    public static string ResolveName(AccessoryDefinition accessory, AccessoryInstanceData instance)
    {
        return ResolveName(accessory, instance != null ? instance.modifierId : null);
    }

    public static string ResolveName(AccessoryDefinition accessory, string modifierId)
    {
        string baseName = ResolveBaseName(accessory);
        string prefix = ResolveModifierPrefix(accessory, modifierId);
        return string.IsNullOrEmpty(prefix) ? baseName : $"{prefix} {baseName}";
    }

    public static string ResolveModifierPrefix(AccessoryDefinition accessory, string modifierId)
    {
        if (string.IsNullOrWhiteSpace(modifierId))
            return string.Empty;

        AccessoryModifierDefinition modifier = accessory != null
            ? accessory.GetModifierById(modifierId)
            : AccessoryReforgeSettings.FindModifier(modifierId);

        if (modifier == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(modifier.displayName))
            return modifier.displayName.Trim();

        return modifier.RuntimeId;
    }

    public static string BuildModifierEffectSummary(AccessoryModifierDefinition modifier)
    {
        if (modifier == null)
            return string.Empty;

        var builder = new StringBuilder();

        if (modifier.statModifiers != null)
        {
            for (int i = 0; i < modifier.statModifiers.Count; i++)
            {
                PassiveStatModifier statModifier = modifier.statModifiers[i];
                if (statModifier == null)
                    continue;

                if (builder.Length > 0)
                    builder.AppendLine();

                builder.Append(FormatStatModifierLine(statModifier));
            }
        }

        if (modifier.passives != null)
        {
            for (int i = 0; i < modifier.passives.Count; i++)
            {
                PassiveDefinition passive = modifier.passives[i];
                if (passive == null)
                    continue;

                if (builder.Length > 0)
                    builder.AppendLine();

                builder.Append(!string.IsNullOrWhiteSpace(passive.passiveId) ? passive.passiveId.Trim() : passive.name);
            }
        }

        return builder.ToString();
    }

    public static string FormatStatModifierLine(PassiveStatModifier modifier)
    {
        if (modifier == null)
            return string.Empty;

        if (modifier.statType == StatType.Stability)
        {
            return modifier.operation switch
            {
                ModifierOp.Flat => $"{FormatSignedNumber(modifier.value)}% Weapon Stability",
                ModifierOp.AddPercent => $"{FormatSignedNumber(modifier.value)}% increased Weapon Stability",
                ModifierOp.Multiply => $"x{FormatNumber(modifier.value)} Weapon Stability",
                _ => $"{FormatSignedNumber(modifier.value)}% Weapon Stability"
            };
        }

        string statName = MakeReadable(modifier.statType.ToString());

        return modifier.operation switch
        {
            ModifierOp.Flat => $"{FormatSignedNumber(modifier.value)} {statName}",
            ModifierOp.AddPercent => $"{FormatSignedNumber(modifier.value)}% {statName}",
            ModifierOp.Multiply => $"x{FormatNumber(modifier.value)} {statName}",
            _ => $"{FormatSignedNumber(modifier.value)} {statName}"
        };
    }

    static string ResolveBaseName(AccessoryDefinition accessory)
    {
        if (accessory == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(accessory.displayName))
            return accessory.displayName.Trim();

        return accessory.name;
    }

    static string FormatNumber(float value)
    {
        return Mathf.Approximately(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.##");
    }

    static string FormatSignedNumber(float value)
    {
        string prefix = value >= 0f ? "+" : string.Empty;
        return prefix + FormatNumber(value);
    }

    static string MakeReadable(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var builder = new StringBuilder(raw.Length + 8);
        for (int i = 0; i < raw.Length; i++)
        {
            char current = raw[i];
            if (i > 0 && char.IsUpper(current) && !char.IsWhiteSpace(raw[i - 1]))
                builder.Append(' ');

            builder.Append(current);
        }

        return builder.ToString();
    }
}
