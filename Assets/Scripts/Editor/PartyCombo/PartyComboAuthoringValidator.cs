using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PartyComboAuthoringValidator
{
    [MenuItem("Tools/Validation/Validate Party Combo Skills")]
    public static void ValidateProject()
    {
        string[] statsGuids = AssetDatabase.FindAssets("t:CharacterStats");
        var comboOwners = new Dictionary<string, CharacterStats>();
        var executionOwners = new Dictionary<SkillGemDefinition, CharacterStats>();
        int errorCount = 0;

        for (int i = 0; i < statsGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(statsGuids[i]);
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(path);
            if (stats == null || stats.partyComboSkill == null)
                continue;

            List<string> issues = Validate(stats, comboOwners, executionOwners);
            for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
            {
                Debug.LogError($"[PartyCombo] {path}: {issues[issueIndex]}", stats);
                errorCount++;
            }
        }

        if (errorCount == 0)
            Debug.Log("[PartyCombo] Authoring validation passed.");
        else
            Debug.LogError($"[PartyCombo] Authoring validation found {errorCount} issue(s).");
    }

    public static List<string> Validate(
        CharacterStats owner,
        Dictionary<string, CharacterStats> comboOwners = null,
        Dictionary<SkillGemDefinition, CharacterStats> executionOwners = null)
    {
        var issues = new List<string>();
        PartyComboSkillDef combo = owner != null ? owner.partyComboSkill : null;
        if (combo == null)
            return issues;

        string comboId = combo.RuntimeId;
        if (string.IsNullOrWhiteSpace(combo.comboId))
            issues.Add("Combo ID is empty.");
        else if (comboOwners != null && comboOwners.TryGetValue(comboId, out CharacterStats existing) && existing != owner)
            issues.Add($"Combo ID '{comboId}' is already owned by '{existing.name}'.");
        else
            comboOwners?.Add(comboId, owner);

        if (combo.executionSkill == null)
            issues.Add("Execution Skill is missing.");
        if (combo.executionProfile == null)
            issues.Add("Execution Profile is missing.");
        else
        {
            if (combo.executionProfile.startTimeoutSeconds <= 0f)
                issues.Add("Execution Profile Start Timeout must be greater than zero.");
            if (combo.executionProfile.recoveryTimeoutSeconds <= 0f)
                issues.Add("Execution Profile Recovery Timeout must be greater than zero.");
            if (combo.executionProfile.requireNavMesh && combo.executionProfile.navMeshSampleDistance <= 0f)
                issues.Add("Execution Profile NavMesh Sample Distance must be greater than zero.");
        }
        if (combo.trigger == null || !PartyComboTriggerEvaluator.IsSupportedMvpTrigger(combo.trigger.kind))
            issues.Add($"Trigger '{combo.trigger?.kind.ToString() ?? "<missing>"}' is unsupported in the MVP.");
        else if (combo.trigger.kind == PartyComboTriggerKind.TargetHasStatus &&
                 combo.trigger.requiredStatus == null &&
                 string.IsNullOrWhiteSpace(combo.trigger.requiredStatusTag))
            issues.Add("Target Has Status requires a Status Effect or Status Tag.");
        if (combo.offerDurationSeconds <= 0f)
            issues.Add("Offer Duration must be greater than zero.");
        if (combo.maxOfferLifetimeSeconds < combo.offerDurationSeconds)
            issues.Add("Max Offer Lifetime must be at least Offer Duration.");
        if (combo.executionSkill != null && ReusesBattleOrHelperSkill(owner, combo.executionSkill))
            issues.Add("Execution Skill is reused by a Battle/Helper role; MVP Combo skills require a dedicated definition.");
        if (combo.executionSkill != null && executionOwners != null)
        {
            if (executionOwners.TryGetValue(combo.executionSkill, out CharacterStats existingOwner) &&
                existingOwner != owner)
            {
                issues.Add($"Execution Skill is already used by Combo owner '{existingOwner.name}'.");
            }
            else if (!executionOwners.ContainsKey(combo.executionSkill))
            {
                executionOwners.Add(combo.executionSkill, owner);
            }
        }

        return issues;
    }

    static bool ReusesBattleOrHelperSkill(CharacterStats owner, SkillGemDefinition execution)
    {
        if (owner == null || execution == null)
            return false;

        if (owner.skillSlots != null)
        {
            for (int slotIndex = 0; slotIndex < owner.skillSlots.Count; slotIndex++)
            {
                CharacterSkillLoadoutSlot slot = owner.skillSlots[slotIndex];
                if (slot?.options == null)
                    continue;
                for (int optionIndex = 0; optionIndex < slot.options.Count; optionIndex++)
                {
                    if (slot.options[optionIndex]?.ActiveSkillAsset == execution)
                        return true;
                }
            }
        }

        if (owner.helperCommandSlot?.options != null)
        {
            for (int i = 0; i < owner.helperCommandSlot.options.Count; i++)
            {
                if (owner.helperCommandSlot.options[i]?.ActiveSkillAsset == execution)
                    return true;
            }
        }

        if (owner.helperProcSlots != null)
        {
            for (int slotIndex = 0; slotIndex < owner.helperProcSlots.Count; slotIndex++)
            {
                HelperProcLoadoutSlot slot = owner.helperProcSlots[slotIndex];
                if (slot?.options == null)
                    continue;
                for (int optionIndex = 0; optionIndex < slot.options.Count; optionIndex++)
                {
                    if (slot.options[optionIndex]?.ExecutionSkill == execution)
                        return true;
                }
            }
        }

        return false;
    }
}
