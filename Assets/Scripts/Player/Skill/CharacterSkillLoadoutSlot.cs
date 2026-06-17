using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CharacterSkillLoadoutSlot
{
    public string slotId;
    public string displayName;
    public KeyCode hotkey;
    [Min(0)] public int defaultOptionIndex;
    public List<CharacterSkillLoadoutOption> options = new();

    public IReadOnlyList<CharacterSkillLoadoutOption> Options => options ?? (options = new List<CharacterSkillLoadoutOption>());

    public string ResolvedSlotId => string.IsNullOrWhiteSpace(slotId) ? string.Empty : slotId.Trim();

    public bool TryGetOption(int optionIndex, out CharacterSkillLoadoutOption option)
    {
        option = null;

        if (options == null || optionIndex < 0 || optionIndex >= options.Count)
            return false;

        option = options[optionIndex];
        return option != null && option.IsConfigured;
    }

    public bool TryGetOptionById(string optionId, out int optionIndex, out CharacterSkillLoadoutOption option)
    {
        optionIndex = -1;
        option = null;

        if (options == null || string.IsNullOrWhiteSpace(optionId))
            return false;

        string resolvedOptionId = optionId.Trim();
        for (int i = 0; i < options.Count; i++)
        {
            CharacterSkillLoadoutOption candidate = options[i];
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

    public bool TryGetDefaultOption(out int optionIndex, out CharacterSkillLoadoutOption option)
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
