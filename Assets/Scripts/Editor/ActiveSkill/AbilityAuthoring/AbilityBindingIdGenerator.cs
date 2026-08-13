using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Stable ability binding id generation (plan section 11). An id is created once from stable
// project identifiers and never silently rewritten by renames, config edits, or payload type
// swaps -- callers must reuse an existing valid id rather than calling GenerateId again once a
// binding exists.
internal static class AbilityBindingIdGenerator
{
    // <skillId>.<nodeId>.<payloadSlug>, deduped with a deterministic ".2", ".3", ... suffix.
    public static string GenerateId(string skillId, string nodeId, string payloadSlug, Func<string, bool> idExists)
    {
        if (idExists == null)
            throw new ArgumentNullException(nameof(idExists));

        string baseId = $"{Normalize(skillId)}.{Normalize(nodeId)}.{Normalize(payloadSlug)}";
        if (!idExists(baseId))
            return baseId;

        for (int suffix = 2; suffix < 10000; suffix++)
        {
            string candidate = $"{baseId}.{suffix.ToString(CultureInfo.InvariantCulture)}";
            if (!idExists(candidate))
                return candidate;
        }

        throw new InvalidOperationException($"Could not generate a unique ability id from base '{baseId}' after 9999 attempts.");
    }

    // Lowercase, dot-separated, alphanumeric-and-underscore project convention. Matches the
    // normalization already visible on existing ids such as "aires.skill.3.a_crowd_scaling".
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unnamed";

        var builder = new StringBuilder(value.Length);
        char previous = '\0';
        foreach (char c in value.Trim().ToLowerInvariant())
        {
            char normalized = char.IsLetterOrDigit(c) ? c
                : (c == '.' ? '.' : '_');

            // Collapse runs of separators so "Tea Table!!" -> "tea_table", not "tea__table_".
            if ((normalized == '_' || normalized == '.') && (previous == '_' || previous == '.' || previous == '\0'))
                continue;

            builder.Append(normalized);
            previous = normalized;
        }

        while (builder.Length > 0 && (builder[builder.Length - 1] == '_' || builder[builder.Length - 1] == '.'))
            builder.Length--;

        return builder.Length > 0 ? builder.ToString() : "unnamed";
    }

    // Every id currently known within the owning skill/tree pair: every node's granted ids plus
    // every step's required-upgrade-id gate, regardless of whether the id currently resolves to
    // anything. Uniqueness checks must see both sides even before a binding is fully consistent.
    public static HashSet<string> CollectKnownIds(SkillUpgradeTreeDefinition tree, SkillGemDefinition owner)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        if (tree?.nodes != null)
        {
            for (int i = 0; i < tree.nodes.Count; i++)
            {
                List<string> granted = tree.nodes[i]?.grantedUpgradeIds;
                if (granted == null)
                    continue;

                for (int j = 0; j < granted.Count; j++)
                {
                    if (!string.IsNullOrWhiteSpace(granted[j]))
                        known.Add(granted[j].Trim());
                }
            }
        }

        if (owner != null)
        {
            var owners = new List<SkillDefinitionBase> { owner };
            foreach (KeyValuePair<string, List<UpgradeIdUsage>> entry in UpgradeIdUsageScanner.Scan(owners))
                known.Add(entry.Key);
        }

        return known;
    }
}
