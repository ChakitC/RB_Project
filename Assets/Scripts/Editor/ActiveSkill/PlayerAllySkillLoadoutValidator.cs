#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PlayerAllySkillLoadoutValidator
{
    [MenuItem("Tools/RB/Validate Player Ally Skill Loadouts")]
    public static void ValidateProject()
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterStats");
        int invalid = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(path);
            if (stats == null || stats.IsHelperRole)
                continue;

            if (Validate(stats, out List<string> errors))
                continue;

            invalid++;
            Debug.LogError($"[PlayerAllySkillLoadout] {path}\n- {string.Join("\n- ", errors)}", stats);
        }

        if (invalid == 0)
            Debug.Log("[PlayerAllySkillLoadout] All migrated Player/Ally loadouts are valid.");
        else
            Debug.LogWarning($"[PlayerAllySkillLoadout] {invalid} character loadout(s) need authoring decisions.");
    }

    public static bool Validate(CharacterStats stats, out List<string> errors)
    {
        errors = new List<string>();
        if (stats == null)
        {
            errors.Add("CharacterStats is missing.");
            return false;
        }

        if (stats.IsHelperRole)
            return true;

        int activeCount = 0;
        int ultimateCount = 0;
        var slotIds = new HashSet<string>(StringComparer.Ordinal);
        List<CharacterSkillLoadoutSlot> slots = stats.skillSlots;
        for (int i = 0; i < (slots?.Count ?? 0); i++)
        {
            CharacterSkillLoadoutSlot slot = slots[i];
            if (slot == null)
                continue;

            string slotId = CharacterSkillLoadoutKeys.StrykerSlotKey(slot, i);
            if (!slotIds.Add(slotId))
                errors.Add($"Duplicate slot id '{slotId}'.");

            CharacterSkillSlotKind kind = slot.ResolvedSlotKind;
            if (kind == CharacterSkillSlotKind.Active)
                activeCount++;
            else if (kind == CharacterSkillSlotKind.Ultimate)
                ultimateCount++;
            else if (kind == CharacterSkillSlotKind.Legacy)
                errors.Add($"Slot '{slotId}' has not been assigned Active, Ultimate, or Passive semantics.");

            ValidateOptions(slot, slotId, kind, errors);
        }

        if (activeCount != 1)
            errors.Add($"Expected exactly one Active slot; found {activeCount}.");
        if (ultimateCount != 1)
            errors.Add($"Expected exactly one Ultimate slot; found {ultimateCount}.");

        return errors.Count == 0;
    }

    static void ValidateOptions(
        CharacterSkillLoadoutSlot slot,
        string slotId,
        CharacterSkillSlotKind kind,
        List<string> errors)
    {
        var optionIds = new HashSet<string>(StringComparer.Ordinal);
        int configured = 0;
        IReadOnlyList<CharacterSkillLoadoutOption> options = slot.Options;
        for (int i = 0; i < options.Count; i++)
        {
            CharacterSkillLoadoutOption option = options[i];
            if (option == null || !option.IsConfigured)
                continue;

            configured++;
            string optionId = CharacterSkillLoadoutKeys.OptionKey(option, i);
            if (!optionIds.Add(optionId))
                errors.Add($"Slot '{slotId}' has duplicate option id '{optionId}'.");

            if ((kind == CharacterSkillSlotKind.Active || kind == CharacterSkillSlotKind.Ultimate) && option.IsPassive)
                errors.Add($"Cast slot '{slotId}' contains passive option '{optionId}'.");
            if (kind == CharacterSkillSlotKind.Passive && !option.IsPassive)
                errors.Add($"Passive slot '{slotId}' contains cast option '{optionId}'.");
        }

        if ((kind == CharacterSkillSlotKind.Active || kind == CharacterSkillSlotKind.Ultimate) && configured == 0)
            errors.Add($"{kind} slot '{slotId}' has no configured skill asset.");
    }
}
#endif
