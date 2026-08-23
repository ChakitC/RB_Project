using UnityEngine;

/// <summary>
/// Save keys for skill selection and Skill Tree progress.
///
/// Stryker slots keep their raw authored id, so progress written before Helper slots existed
/// still resolves. Helper slots are namespaced because a Helper's command slot and its proc slots
/// are authored independently and could otherwise pick the same id.
/// </summary>
public static class CharacterSkillLoadoutKeys
{
    public const string HelperCommandPrefix = "helper:command:";
    public const string HelperProcPrefix = "helper:proc:";

    public static string StrykerSlotKey(CharacterSkillLoadoutSlot slot, int slotIndex)
    {
        return slot != null && !string.IsNullOrWhiteSpace(slot.ResolvedSlotId)
            ? slot.ResolvedSlotId
            : $"slot:{Mathf.Max(0, slotIndex)}";
    }

    public static string HelperCommandSlotKey(CharacterSkillLoadoutSlot slot)
    {
        string raw = slot != null && !string.IsNullOrWhiteSpace(slot.ResolvedSlotId)
            ? slot.ResolvedSlotId
            : "default";
        return HelperCommandPrefix + raw;
    }

    public static string HelperProcSlotKey(HelperProcLoadoutSlot slot, int slotIndex)
    {
        string raw = slot != null && !string.IsNullOrWhiteSpace(slot.ResolvedSlotId)
            ? slot.ResolvedSlotId
            : $"slot:{Mathf.Max(0, slotIndex)}";
        return HelperProcPrefix + raw;
    }

    public static string OptionKey(CharacterSkillLoadoutOption option, int optionIndex)
    {
        return option != null && !string.IsNullOrWhiteSpace(option.ResolvedOptionId)
            ? option.ResolvedOptionId
            : $"option:{Mathf.Max(0, optionIndex)}";
    }

    public static string OptionKey(HelperProcLoadoutOption option, int optionIndex)
    {
        return option != null && !string.IsNullOrWhiteSpace(option.ResolvedOptionId)
            ? option.ResolvedOptionId
            : $"option:{Mathf.Max(0, optionIndex)}";
    }
}
