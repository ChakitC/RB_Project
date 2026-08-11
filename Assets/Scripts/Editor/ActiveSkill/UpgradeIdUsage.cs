using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One place an upgrade id is consumed, resolved from the owning skill/passive asset by
/// <see cref="UpgradeIdUsageScanner"/>. Purely an editor-side authoring aid: nothing here is
/// read at runtime, and the runtime gate is still <c>SkillCastContext.HasUpgrade</c>.
/// </summary>
public sealed class UpgradeIdUsage
{
    /// <summary>Trimmed id the site listens for.</summary>
    public string UpgradeId;

    /// <summary>Asset (or embedded sub-asset) whose SerializedObject carries the id property.</summary>
    public Object Owner;

    /// <summary>Property path of the id field on <see cref="Owner"/>, used to navigate back to it.</summary>
    public string PropertyPath;

    /// <summary>Index into the owning skill's composite steps, or -1 when the site is not in a step.</summary>
    public int StepIndex = -1;

    /// <summary>Single-line "what this does" sentence, e.g. "Apply Aires3_TeaGuard to Self".</summary>
    public string Summary;

    /// <summary>Resolved status numbers, one label per line. Empty for non-status sites.</summary>
    public List<string> Details = new();

    /// <summary>Where the site lives, e.g. "Step 2: HealArea (Allies)".</summary>
    public string SourceLabel;

    /// <summary>Skill/passive asset this usage was scanned from; resolves base stats for previews.</summary>
    public SkillDefinitionBase OwningSkill;

    /// <summary>True when this usage's numbers come from FinalSkillStats.healPower.</summary>
    public bool ReadsHealPowerStat;

    /// <summary>True when this usage's numbers come from FinalSkillStats.areaRadius.</summary>
    public bool ReadsAreaRadiusStat;

    /// <summary>Authoring problems found while describing this usage, e.g. a missing Status Effect Def.</summary>
    public List<string> Warnings = new();
}
