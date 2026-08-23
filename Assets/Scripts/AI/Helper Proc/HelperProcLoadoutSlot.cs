using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A Helper proc slot on a character asset. Mirrors <see cref="CharacterSkillLoadoutSlot"/>: the
/// slot is the stable authoring unit, and each option inside it is a variant the player can
/// switch between while keeping its own Skill Tree progress.
/// </summary>
[Serializable]
public sealed class HelperProcLoadoutSlot
{
    public string slotId;
    public string displayName;
    [Min(0)] public int defaultOptionIndex;
    public List<HelperProcLoadoutOption> options = new();

    public IReadOnlyList<HelperProcLoadoutOption> Options => options ?? (options = new List<HelperProcLoadoutOption>());

    public string ResolvedSlotId => string.IsNullOrWhiteSpace(slotId) ? string.Empty : slotId.Trim();

    public bool HasConfiguredOption
    {
        get
        {
            if (options == null)
                return false;

            for (int i = 0; i < options.Count; i++)
            {
                if (options[i] != null && options[i].IsConfigured)
                    return true;
            }

            return false;
        }
    }

    public bool TryGetOption(int optionIndex, out HelperProcLoadoutOption option)
    {
        option = null;

        if (options == null || optionIndex < 0 || optionIndex >= options.Count)
            return false;

        option = options[optionIndex];
        return option != null && option.IsConfigured;
    }

    public bool TryGetOptionById(string optionId, out int optionIndex, out HelperProcLoadoutOption option)
    {
        optionIndex = -1;
        option = null;

        if (options == null || string.IsNullOrWhiteSpace(optionId))
            return false;

        string resolvedOptionId = optionId.Trim();
        for (int i = 0; i < options.Count; i++)
        {
            HelperProcLoadoutOption candidate = options[i];
            if (candidate == null || !candidate.IsConfigured)
                continue;

            if (!string.Equals(candidate.ResolvedOptionId, resolvedOptionId, StringComparison.Ordinal))
                continue;

            optionIndex = i;
            option = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetDefaultOption(out int optionIndex, out HelperProcLoadoutOption option)
    {
        if (TryGetOption(defaultOptionIndex, out option))
        {
            optionIndex = defaultOptionIndex;
            return true;
        }

        optionIndex = -1;
        if (options == null)
            return false;

        for (int i = 0; i < options.Count; i++)
        {
            if (!TryGetOption(i, out option))
                continue;

            optionIndex = i;
            return true;
        }

        option = null;
        return false;
    }
}
