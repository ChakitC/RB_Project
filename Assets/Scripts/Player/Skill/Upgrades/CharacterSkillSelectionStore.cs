using System;
using System.Collections.Generic;

public static class CharacterSkillSelectionStore
{
    public static string FindOptionId(CharacterProgressData progress, string slotId)
    {
        if (progress == null || progress.selectedSkillOptions == null || string.IsNullOrWhiteSpace(slotId))
            return null;

        for (int i = 0; i < progress.selectedSkillOptions.Count; i++)
        {
            CharacterSkillSelectionSaveData entry = progress.selectedSkillOptions[i];
            if (entry != null && string.Equals(entry.slotId, slotId, StringComparison.Ordinal))
                return entry.optionId;
        }

        return null;
    }

    public static bool SetOption(CharacterProgressData progress, string slotId, string optionId)
    {
        if (progress == null || string.IsNullOrWhiteSpace(slotId) || string.IsNullOrWhiteSpace(optionId))
            return false;

        progress.selectedSkillOptions ??= new List<CharacterSkillSelectionSaveData>();
        string resolvedSlotId = slotId.Trim();
        string resolvedOptionId = optionId.Trim();
        CharacterSkillSelectionSaveData target = null;

        for (int i = progress.selectedSkillOptions.Count - 1; i >= 0; i--)
        {
            CharacterSkillSelectionSaveData entry = progress.selectedSkillOptions[i];
            if (entry == null)
            {
                progress.selectedSkillOptions.RemoveAt(i);
                continue;
            }

            if (!string.Equals(entry.slotId, resolvedSlotId, StringComparison.Ordinal))
                continue;

            if (target == null)
                target = entry;
            else
                progress.selectedSkillOptions.RemoveAt(i);
        }

        if (target == null)
        {
            target = new CharacterSkillSelectionSaveData();
            progress.selectedSkillOptions.Add(target);
        }

        bool changed = !string.Equals(target.optionId, resolvedOptionId, StringComparison.Ordinal) ||
                       !string.Equals(target.slotId, resolvedSlotId, StringComparison.Ordinal);
        target.slotId = resolvedSlotId;
        target.optionId = resolvedOptionId;
        return changed;
    }
}
