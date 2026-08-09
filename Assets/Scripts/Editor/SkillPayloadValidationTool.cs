#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SkillPayloadValidationTool
{
    // ไม่ใช้ EditorUtility.DisplayDialog — modal dialog บล็อก editor ทั้งตัวจน automation/MCP เรียก menu item
    // นี้แล้วค้างรอจนกว่าจะมีคนกด OK. ValidateAllSkills log ผลลัพธ์เต็มลง Console อยู่แล้ว.
    [MenuItem("Tools/RB/Skills/Validate Embedded Payloads")]
    private static void ValidateMenu()
    {
        ValidateAllSkills(logResult: true);
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
                Debug.Log("All SkillGemDefinition assets have valid embedded payload ownership graphs.");
            else
                Debug.LogError($"Skill payload validation found {issueCount} issues.\n{report}");
        }

        return issueCount;
    }

    internal static void ValidateSkill(SkillGemDefinition skill, List<string> issues)
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

        HashSet<SkillPayloadDef> reachable =
            SkillPayloadAssetUtility.GetReachablePayloads(skill.payload);

        foreach (SkillPayloadDef referenced in reachable)
        {
            if (referenced != null && !SkillPayloadAssetUtility.IsEmbedded(skill, referenced))
            {
                issues.Add(
                    $"Referenced payload '{referenced.name}' is not embedded in the same asset as this skill.");
            }
        }

        for (int i = 0; i < embeddedPayloads.Count; i++)
        {
            SkillPayloadDef embedded = embeddedPayloads[i];
            if (embedded != null && !reachable.Contains(embedded))
                issues.Add($"Embedded payload '{embedded.name}' is not referenced by the root payload or any composite step.");
        }

        skill.payload.CollectValidationIssues(issues);
        skill.CollectRequiredTimelineValidationIssues(issues);
        skill.CollectSkillVfxValidationIssues(issues);
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
