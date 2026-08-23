using System.Collections.Generic;
using UnityEngine;

/// <summary>Which half of the Skill Loadout a descriptor came from.</summary>
public enum SkillLoadoutKind
{
    /// <summary>A Stryker command slot, cast from a hotkey.</summary>
    Stryker = 0,

    /// <summary>The Helper's manual party-command assist.</summary>
    HelperCommand = 1,

    /// <summary>A Helper proc slot, fired by a trigger rather than by input.</summary>
    HelperProc = 2,
}

/// <summary>
/// One selectable variant, flattened for the Skill screen.
///
/// The screen never branches on which serialized class a variant came from: a Stryker option and
/// a Helper proc option differ only in the fields they leave empty.
/// </summary>
public sealed class SkillLoadoutOptionDescriptor
{
    public string OptionId;
    public string DisplayName;
    public string Description;

    /// <summary>Human-readable trigger line for a proc, or empty for anything the player casts.</summary>
    public string TriggerSummary;

    public Sprite Icon;

    /// <summary>Gem used for stat preview and runtime cast. Null for a passive variant.</summary>
    public SkillGemDefinition SkillAsset;

    /// <summary>Passive definition when this variant is a passive, otherwise null.</summary>
    public PassiveDefinition PassiveAsset;

    public SkillUpgradeTreeDefinition UpgradeTree;

    /// <summary>Proc definition for <see cref="SkillLoadoutKind.HelperProc"/> variants.</summary>
    public SkillHelperDef HelperProc;

    public bool IsPassive => PassiveAsset != null;
    public bool HasTree => UpgradeTree != null;
}

/// <summary>A tab on the Skill screen: one slot and the variants the player may put in it.</summary>
public sealed class SkillLoadoutSlotDescriptor
{
    /// <summary>
    /// Namespaced save key. Stryker slots keep their raw slot id so existing progress still
    /// resolves; Helper slots are prefixed so a proc slot can never collide with a command slot.
    /// </summary>
    public string SlotId;

    public string DisplayName;
    public SkillLoadoutKind Kind;
    public int DefaultOptionIndex;
    public bool IsPassiveSlot;
    public List<SkillLoadoutOptionDescriptor> Options = new();

    public bool TryGetOption(int optionIndex, out SkillLoadoutOptionDescriptor option)
    {
        option = null;
        if (Options == null || optionIndex < 0 || optionIndex >= Options.Count)
            return false;

        option = Options[optionIndex];
        return option != null;
    }

    public bool TryGetOptionById(string optionId, out int optionIndex)
    {
        optionIndex = -1;
        if (Options == null || string.IsNullOrWhiteSpace(optionId))
            return false;

        string resolved = optionId.Trim();
        for (int i = 0; i < Options.Count; i++)
        {
            if (Options[i] != null &&
                string.Equals(Options[i].OptionId, resolved, System.StringComparison.Ordinal))
            {
                optionIndex = i;
                return true;
            }
        }

        return false;
    }
}
