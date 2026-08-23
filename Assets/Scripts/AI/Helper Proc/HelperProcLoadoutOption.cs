using System;
using UnityEngine;

/// <summary>
/// One selectable variant inside a <see cref="HelperProcLoadoutSlot"/>.
///
/// The proc definition owns the trigger; the Skill Tree the player spends points in belongs to
/// the proc's execution skill, so the tree is resolved through <see cref="SkillHelperDef.executionSkill"/>
/// rather than authored twice.
/// </summary>
[Serializable]
public sealed class HelperProcLoadoutOption
{
    public string optionId;
    public string displayName;

    [Header("Proc")]
    [Tooltip("Trigger definition. Its Execution Skill supplies the icon, stat preview and Skill Tree.")]
    public SkillHelperDef helperProc;

    public bool IsConfigured => helperProc != null && helperProc.executionSkill != null;

    public SkillGemDefinition ExecutionSkill => helperProc != null ? helperProc.executionSkill : null;

    public SkillUpgradeTreeDefinition ResolvedUpgradeTree
    {
        get
        {
            SkillGemDefinition execution = ExecutionSkill;
            return execution != null ? execution.UpgradeTree : null;
        }
    }

    public string ResolvedOptionId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(optionId))
                return optionId.Trim();

            if (helperProc == null)
                return string.Empty;

            return !string.IsNullOrWhiteSpace(helperProc.helperId)
                ? helperProc.helperId.Trim()
                : helperProc.name;
        }
    }

    public string ResolvedDisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName.Trim();

            if (helperProc == null)
                return ResolvedOptionId;

            return !string.IsNullOrWhiteSpace(helperProc.displayName)
                ? helperProc.displayName.Trim()
                : helperProc.name;
        }
    }
}
