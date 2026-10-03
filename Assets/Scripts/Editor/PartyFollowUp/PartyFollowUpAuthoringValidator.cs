using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PartyFollowUpAuthoringValidator
{
    [MenuItem("Tools/Validation/Validate Party Follow-Up Skills")]
    public static void ValidateProject()
    {
        string[] statsGuids = AssetDatabase.FindAssets("t:CharacterStats");
        var followUpOwners = new Dictionary<string, CharacterStats>();
        var executionOwners = new Dictionary<SkillGemDefinition, CharacterStats>();
        int errorCount = 0;

        for (int i = 0; i < statsGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(statsGuids[i]);
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(path);
            if (stats == null || stats.partyFollowUpSkill == null)
                continue;

            List<string> issues = Validate(stats, followUpOwners, executionOwners);
            for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
            {
                Debug.LogError($"[PartyFollowUp] {path}: {issues[issueIndex]}", stats);
                errorCount++;
            }
        }

        if (errorCount == 0)
            Debug.Log("[PartyFollowUp] Authoring validation passed.");
        else
            Debug.LogError($"[PartyFollowUp] Authoring validation found {errorCount} issue(s).");
    }

    public static List<string> Validate(
        CharacterStats owner,
        Dictionary<string, CharacterStats> followUpOwners = null,
        Dictionary<SkillGemDefinition, CharacterStats> executionOwners = null)
    {
        var issues = new List<string>();
        PartyFollowUpSkillDef followUp = owner != null ? owner.partyFollowUpSkill : null;
        if (followUp == null)
            return issues;

        string followUpId = followUp.RuntimeId;
        if (string.IsNullOrWhiteSpace(followUp.followUpId))
            issues.Add("FollowUp ID is empty.");
        else if (followUpOwners != null && followUpOwners.TryGetValue(followUpId, out CharacterStats existing) && existing != owner)
            issues.Add($"FollowUp ID '{followUpId}' is already owned by '{existing.name}'.");
        else
            followUpOwners?.Add(followUpId, owner);

        if (followUp.executionSkill == null)
            issues.Add("Execution Skill is missing.");
        if (followUp.executionProfile == null)
            issues.Add("Execution Profile is missing.");
        else
        {
            if (followUp.executionProfile.startTimeoutSeconds <= 0f)
                issues.Add("Execution Profile Start Timeout must be greater than zero.");
            if (followUp.executionProfile.recoveryTimeoutSeconds <= 0f)
                issues.Add("Execution Profile Recovery Timeout must be greater than zero.");
            if (followUp.executionProfile.requireNavMesh && followUp.executionProfile.navMeshSampleDistance <= 0f)
                issues.Add("Execution Profile NavMesh Sample Distance must be greater than zero.");
        }
        if (followUp.trigger == null || !PartyFollowUpTriggerEvaluator.IsSupportedMvpTrigger(followUp.trigger.kind))
            issues.Add($"Trigger '{followUp.trigger?.kind.ToString() ?? "<missing>"}' is unsupported in the MVP.");
        else if (followUp.trigger.kind == PartyFollowUpTriggerKind.TargetHasStatus &&
                 followUp.trigger.requiredStatus == null &&
                 string.IsNullOrWhiteSpace(followUp.trigger.requiredStatusTag))
            issues.Add("Target Has Status requires a Status Effect or Status Tag.");
        if (followUp.offerDurationSeconds <= 0f)
            issues.Add("Offer Duration must be greater than zero.");
        if (followUp.maxOfferLifetimeSeconds < followUp.offerDurationSeconds)
            issues.Add("Max Offer Lifetime must be at least Offer Duration.");
        if (followUp.executionSkill != null && ReusesBattleOrHelperSkill(owner, followUp.executionSkill))
            issues.Add("Execution Skill is reused by a Battle/Helper role; MVP FollowUp skills require a dedicated definition.");
        if (followUp.executionSkill != null && executionOwners != null)
        {
            if (executionOwners.TryGetValue(followUp.executionSkill, out CharacterStats existingOwner) &&
                existingOwner != owner)
            {
                issues.Add($"Execution Skill is already used by FollowUp owner '{existingOwner.name}'.");
            }
            else if (!executionOwners.ContainsKey(followUp.executionSkill))
            {
                executionOwners.Add(followUp.executionSkill, owner);
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
