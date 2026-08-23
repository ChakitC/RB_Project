using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns the two halves of a character's Skill Loadout into one descriptor list.
///
/// This is the only place that knows a Stryker reads <c>skillSlots</c> while a Helper reads
/// <c>helperCommandSlot</c> and <c>helperProcSlots</c>. Everything downstream - session, screen,
/// tree view - works from descriptors, so adding a role never means another branch in the UI.
/// </summary>
public static class SkillLoadoutDescriptorFactory
{
    public static List<SkillLoadoutSlotDescriptor> Build(CharacterStats stats)
    {
        var slots = new List<SkillLoadoutSlotDescriptor>();
        if (stats == null)
            return slots;

        if (stats.IsHelperRole)
        {
            AppendHelperCommandSlot(stats, slots);
            AppendHelperProcSlots(stats, slots);
            return slots;
        }

        AppendStrykerSlots(stats, slots);
        return slots;
    }

    static void AppendStrykerSlots(CharacterStats stats, List<SkillLoadoutSlotDescriptor> slots)
    {
        List<CharacterSkillLoadoutSlot> source = stats.skillSlots;
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            CharacterSkillLoadoutSlot slot = source[i];
            if (slot == null)
                continue;

            var descriptor = new SkillLoadoutSlotDescriptor
            {
                SlotId = CharacterSkillLoadoutKeys.StrykerSlotKey(slot, i),
                DisplayName = !string.IsNullOrWhiteSpace(slot.displayName)
                    ? slot.displayName.Trim()
                    : BuildStrykerSlotLabel(i),
                Kind = SkillLoadoutKind.Stryker,
                DefaultOptionIndex = Mathf.Max(0, slot.defaultOptionIndex),
                IsPassiveSlot = slot.IsPassiveSlot,
            };

            AppendSkillOptions(slot, descriptor.Options);
            slots.Add(descriptor);
        }
    }

    static void AppendHelperCommandSlot(CharacterStats stats, List<SkillLoadoutSlotDescriptor> slots)
    {
        CharacterSkillLoadoutSlot slot = stats.helperCommandSlot;
        if (slot == null)
            return;

        var descriptor = new SkillLoadoutSlotDescriptor
        {
            SlotId = CharacterSkillLoadoutKeys.HelperCommandSlotKey(slot),
            DisplayName = !string.IsNullOrWhiteSpace(slot.displayName)
                ? slot.displayName.Trim()
                : "COMMAND",
            Kind = SkillLoadoutKind.HelperCommand,
            DefaultOptionIndex = Mathf.Max(0, slot.defaultOptionIndex),
            IsPassiveSlot = false,
        };

        AppendSkillOptions(slot, descriptor.Options);

        // A Helper with no authored manual command is a valid character, not a broken one, so the
        // tab is simply not offered rather than shown empty.
        if (descriptor.Options.Count > 0)
            slots.Add(descriptor);
    }

    static void AppendHelperProcSlots(CharacterStats stats, List<SkillLoadoutSlotDescriptor> slots)
    {
        List<HelperProcLoadoutSlot> source = stats.helperProcSlots;
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            HelperProcLoadoutSlot slot = source[i];
            if (slot == null)
                continue;

            var descriptor = new SkillLoadoutSlotDescriptor
            {
                SlotId = CharacterSkillLoadoutKeys.HelperProcSlotKey(slot, i),
                DisplayName = !string.IsNullOrWhiteSpace(slot.displayName)
                    ? slot.displayName.Trim()
                    : BuildProcSlotLabel(i),
                Kind = SkillLoadoutKind.HelperProc,
                DefaultOptionIndex = Mathf.Max(0, slot.defaultOptionIndex),
                IsPassiveSlot = false,
            };

            AppendProcOptions(slot, descriptor.Options);

            if (descriptor.Options.Count > 0)
                slots.Add(descriptor);
        }
    }

    static void AppendSkillOptions(CharacterSkillLoadoutSlot slot, List<SkillLoadoutOptionDescriptor> buffer)
    {
        IReadOnlyList<CharacterSkillLoadoutOption> options = slot.Options;
        for (int i = 0; i < options.Count; i++)
        {
            CharacterSkillLoadoutOption option = options[i];
            if (option == null || !option.IsConfigured)
                continue;

            SkillGemDefinition gem = option.ActiveSkillAsset;
            buffer.Add(new SkillLoadoutOptionDescriptor
            {
                OptionId = CharacterSkillLoadoutKeys.OptionKey(option, i),
                DisplayName = option.ResolvedDisplayName,
                Description = gem != null ? gem.description : string.Empty,
                TriggerSummary = string.Empty,
                Icon = option.skillAsset != null ? option.skillAsset.SkillDefinitionIcon : null,
                SkillAsset = gem,
                PassiveAsset = option.PassiveAsset,
                UpgradeTree = option.ResolvedUpgradeTree,
                HelperProc = null,
            });
        }
    }

    static void AppendProcOptions(HelperProcLoadoutSlot slot, List<SkillLoadoutOptionDescriptor> buffer)
    {
        IReadOnlyList<HelperProcLoadoutOption> options = slot.Options;
        for (int i = 0; i < options.Count; i++)
        {
            HelperProcLoadoutOption option = options[i];
            if (option == null || !option.IsConfigured)
                continue;

            SkillHelperDef proc = option.helperProc;
            SkillGemDefinition execution = option.ExecutionSkill;

            buffer.Add(new SkillLoadoutOptionDescriptor
            {
                OptionId = CharacterSkillLoadoutKeys.OptionKey(option, i),
                DisplayName = option.ResolvedDisplayName,
                Description = proc != null && !string.IsNullOrWhiteSpace(proc.description)
                    ? proc.description
                    : execution != null ? execution.description : string.Empty,
                TriggerSummary = BuildTriggerSummary(proc),
                Icon = execution != null ? execution.SkillDefinitionIcon : null,
                SkillAsset = execution,
                PassiveAsset = null,
                UpgradeTree = option.ResolvedUpgradeTree,
                HelperProc = proc,
            });
        }
    }

    /// <summary>
    /// One line describing when a proc fires. Threshold procs deliberately do not show a chance:
    /// they are deterministic, and printing "100%" next to a health condition reads as a roll.
    /// </summary>
    public static string BuildTriggerSummary(SkillHelperDef proc)
    {
        if (proc == null)
            return string.Empty;

        string cooldown = proc.internalCooldownSeconds > 0f
            ? $" · ICD {proc.internalCooldownSeconds:0.##}s"
            : string.Empty;

        if (proc.IsPartyHealthTrigger)
            return $"Party HP ≤ {Mathf.RoundToInt(Mathf.Clamp01(proc.partyHealthThreshold) * 100f)}%{cooldown}";

        return $"On {proc.triggerEvent} · {Mathf.Clamp01(proc.procChance) * 100f:0.##}%{cooldown}";
    }

    static string BuildStrykerSlotLabel(int index)
    {
        return index switch
        {
            0 => "SKILL I",
            1 => "SKILL II",
            2 => "SKILL III",
            _ => $"SKILL {index + 1}",
        };
    }

    static string BuildProcSlotLabel(int index)
    {
        return $"PROC {index + 1}";
    }
}
