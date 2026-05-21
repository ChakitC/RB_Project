using System.Text;
using UnityEngine;

public static class WeaponAffixDisplayUtility
{
    public static string BuildSummary(WeaponInstanceData instance, WeaponAffixDatabase affixDatabase)
    {
        if (instance == null)
            return string.Empty;

        var builder = new StringBuilder();
        AppendAffix(builder, instance.mainAffix, affixDatabase);

        if (instance.subAffixes != null)
        {
            for (int i = 0; i < instance.subAffixes.Count; i++)
                AppendAffix(builder, instance.subAffixes[i], affixDatabase);
        }

        return builder.ToString();
    }

    static void AppendAffix(StringBuilder builder, RolledAffixData rolledAffix, WeaponAffixDatabase affixDatabase)
    {
        if (builder == null || rolledAffix == null || rolledAffix.IsEmpty)
            return;

        if (builder.Length > 0)
            builder.Append("; ");

        var definition = ResolveDefinition(rolledAffix, affixDatabase);
        builder.Append(ResolveAffixName(definition, rolledAffix.affixId));

        string value = BuildValueSummary(definition, rolledAffix);
        if (!string.IsNullOrWhiteSpace(value))
            builder.Append(" (").Append(value).Append(')');
    }

    static WeaponAffixDefinition ResolveDefinition(RolledAffixData rolledAffix, WeaponAffixDatabase affixDatabase)
    {
        if (rolledAffix == null || string.IsNullOrWhiteSpace(rolledAffix.affixId))
            return null;

        if (affixDatabase != null)
        {
            var fromDatabase = affixDatabase.GetById(rolledAffix.affixId);
            if (fromDatabase != null)
                return fromDatabase;
        }

        return WeaponAffixDatabase.GetLoadedAffixById(rolledAffix.affixId);
    }

    static string ResolveAffixName(WeaponAffixDefinition definition, string affixId)
    {
        if (definition != null && !string.IsNullOrWhiteSpace(definition.displayName))
            return definition.displayName.Trim();

        if (!string.IsNullOrWhiteSpace(affixId))
            return affixId.Trim();

        return "Unknown Affix";
    }

    static string BuildValueSummary(WeaponAffixDefinition definition, RolledAffixData rolledAffix)
    {
        if (definition == null)
        {
            return rolledAffix != null && rolledAffix.hasPrimaryValue
                ? FormatSignedNumber(rolledAffix.primaryValue)
                : string.Empty;
        }

        switch (definition.behaviorType)
        {
            case WeaponAffixBehaviorType.StatModifier:
                return BuildStatSummary(definition, rolledAffix);

            case WeaponAffixBehaviorType.TimedBuffOnReload:
                return $"On reload {BuildStatSummary(definition, rolledAffix)}";

            case WeaponAffixBehaviorType.SpecialProjectileEveryNthShot:
                return $"Every {Mathf.Max(1, definition.requiredShots)} shots";

            default:
                return rolledAffix != null && rolledAffix.hasPrimaryValue
                    ? FormatSignedNumber(rolledAffix.primaryValue)
                    : string.Empty;
        }
    }

    static string BuildStatSummary(WeaponAffixDefinition definition, RolledAffixData rolledAffix)
    {
        if (definition == null)
            return string.Empty;

        float value = definition.ResolvePrimaryValue(rolledAffix);
        string statName = MakeReadable(definition.statType.ToString());

        return definition.modifierOp switch
        {
            ModifierOp.Flat => $"{FormatSignedNumber(value)} {statName}",
            ModifierOp.AddPercent => $"{FormatSignedNumber(value)}% {statName}",
            ModifierOp.Multiply => $"x{FormatNumber(value)} {statName}",
            _ => $"{FormatSignedNumber(value)} {statName}"
        };
    }

    static string FormatSignedNumber(float value)
    {
        string prefix = value >= 0f ? "+" : string.Empty;
        return prefix + FormatNumber(value);
    }

    static string FormatNumber(float value)
    {
        return Mathf.Approximately(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.##");
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
