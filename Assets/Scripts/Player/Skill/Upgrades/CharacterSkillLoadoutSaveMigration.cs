using System;
using System.Collections.Generic;

/// <summary>
/// Moves legacy player/ally slot keys to the stable Active/Ultimate keys before a progress model
/// can reconcile tree data. Character rules are added only after both cast slots are confirmed.
/// </summary>
public static class CharacterSkillLoadoutSaveMigration
{
    public const int CurrentVersion = 1;
    public const string ActiveSlotId = "active";
    public const string UltimateSlotId = "ultimate";

    const string FenoCharacterId = "ID.Feno";
    const string FenoActiveOptionId = "feno.skill.minigunterret";
    const string FenoUltimateOptionId = "Feno.Skill_Ulatimate01";

    public static bool Migrate(string characterId, CharacterProgressData progress)
    {
        if (progress == null || progress.combatLoadoutMigrationVersion >= CurrentVersion)
            return false;

        if (!string.Equals(characterId?.Trim(), FenoCharacterId, StringComparison.Ordinal))
            return false;

        bool changed = false;
        bool complete = true;

        progress.selectedSkillOptions ??= new List<CharacterSkillSelectionSaveData>();
        progress.activeSkillTrees ??= new List<CharacterSkillTreeProgressSaveData>();

        changed |= RemapSelection(progress.selectedSkillOptions, "2", ActiveSlotId, FenoActiveOptionId, ref complete);
        changed |= EnsureSelection(progress.selectedSkillOptions, ActiveSlotId, FenoActiveOptionId);
        changed |= EnsureSelection(progress.selectedSkillOptions, UltimateSlotId, FenoUltimateOptionId);
        changed |= RemapTrees(progress.activeSkillTrees, "2", ActiveSlotId, FenoActiveOptionId, ref complete);

        if (complete)
        {
            progress.combatLoadoutMigrationVersion = CurrentVersion;
            changed = true;
        }

        return changed;
    }

    static bool RemapSelection(
        List<CharacterSkillSelectionSaveData> selections,
        string oldSlotId,
        string newSlotId,
        string expectedOptionId,
        ref bool complete)
    {
        CharacterSkillSelectionSaveData legacy = FindSelection(selections, oldSlotId, expectedOptionId);
        if (legacy == null)
            return false;

        CharacterSkillSelectionSaveData destination = FindSelection(selections, newSlotId, null);
        if (destination != null)
        {
            if (!string.Equals(destination.optionId, legacy.optionId, StringComparison.Ordinal))
                complete = false;
            return false;
        }

        legacy.slotId = newSlotId;
        return true;
    }

    static bool EnsureSelection(
        List<CharacterSkillSelectionSaveData> selections,
        string slotId,
        string optionId)
    {
        if (FindSelection(selections, slotId, null) != null)
            return false;

        selections.Add(new CharacterSkillSelectionSaveData { slotId = slotId, optionId = optionId });
        return true;
    }

    static CharacterSkillSelectionSaveData FindSelection(
        List<CharacterSkillSelectionSaveData> selections,
        string slotId,
        string optionId)
    {
        for (int i = 0; i < selections.Count; i++)
        {
            CharacterSkillSelectionSaveData entry = selections[i];
            if (entry == null || !string.Equals(entry.slotId, slotId, StringComparison.Ordinal))
                continue;

            if (optionId == null || string.Equals(entry.optionId, optionId, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    static bool RemapTrees(
        List<CharacterSkillTreeProgressSaveData> trees,
        string oldSlotId,
        string newSlotId,
        string optionId,
        ref bool complete)
    {
        bool changed = false;
        for (int i = 0; i < trees.Count; i++)
        {
            CharacterSkillTreeProgressSaveData legacy = trees[i];
            if (legacy == null ||
                !string.Equals(legacy.slotId, oldSlotId, StringComparison.Ordinal) ||
                !string.Equals(legacy.optionId, optionId, StringComparison.Ordinal))
            {
                continue;
            }

            if (HasTreeKey(trees, newSlotId, optionId))
            {
                complete = false;
                continue;
            }

            legacy.slotId = newSlotId;
            changed = true;
        }

        return changed;
    }

    static bool HasTreeKey(List<CharacterSkillTreeProgressSaveData> trees, string slotId, string optionId)
    {
        for (int i = 0; i < trees.Count; i++)
        {
            CharacterSkillTreeProgressSaveData entry = trees[i];
            if (entry != null &&
                string.Equals(entry.slotId, slotId, StringComparison.Ordinal) &&
                string.Equals(entry.optionId, optionId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
