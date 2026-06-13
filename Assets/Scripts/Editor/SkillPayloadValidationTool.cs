#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SkillPayloadValidationTool
{
    [MenuItem("Tools/RB/Skills/Validate Embedded Payloads")]
    private static void ValidateMenu()
    {
        int issueCount = ValidateAllSkills(logResult: true);
        EditorUtility.DisplayDialog(
            "Skill Payload Validation",
            issueCount == 0
                ? "All skill payloads are valid and embedded."
                : $"Found {issueCount} validation issues. See Console for details.",
            "OK");
    }

    public static int ValidateAllSkills(bool logResult)
    {
        int issueCount = 0;
        var report = new StringBuilder();

        foreach (SkillGemDefinition skill in FindAllSkills())
        {
            var issues = new List<string>();
            ValidateSkill(skill, issues);
            if (issues.Count == 0)
                continue;

            issueCount += issues.Count;
            report.AppendLine($"{AssetDatabase.GetAssetPath(skill)}:");
            for (int i = 0; i < issues.Count; i++)
                report.AppendLine($"- {issues[i]}");
        }

        if (logResult)
        {
            if (issueCount == 0)
                Debug.Log("All SkillGemDefinition assets have exactly one valid embedded payload.");
            else
                Debug.LogError($"Skill payload validation found {issueCount} issues.\n{report}");
        }

        return issueCount;
    }

    private static void ValidateSkill(SkillGemDefinition skill, List<string> issues)
    {
        if (skill == null)
        {
            issues.Add("Skill asset could not be loaded.");
            return;
        }

        if (skill.payload == null)
        {
            issues.Add("Execution payload is missing.");
            return;
        }

        if (!SkillPayloadAssetUtility.IsEmbedded(skill, skill.payload))
            issues.Add("Execution payload is not embedded in the skill asset.");

        List<SkillPayloadDef> embeddedPayloads = SkillPayloadAssetUtility.GetEmbeddedPayloads(skill);
        if (embeddedPayloads.Count == 0)
            issues.Add("The skill asset contains no embedded payload object.");
        else if (embeddedPayloads.Count > 1)
            issues.Add($"The skill asset contains {embeddedPayloads.Count} embedded payload objects; exactly one is allowed.");

        skill.payload.CollectValidationIssues(issues);
    }

    private static List<SkillGemDefinition> FindAllSkills()
    {
        string[] guids = AssetDatabase.FindAssets("t:SkillGemDefinition");
        var skills = new List<SkillGemDefinition>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            SkillGemDefinition skill = AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(path);
            if (skill != null)
                skills.Add(skill);
        }

        return skills;
    }
}
#endif
