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
        SkillExecutionUsageIndex.Rebuild();

        foreach (SkillGemDefinition skill in FindAllSkills())
        {
            var issues = new List<string>();
            var diagnostics = new List<SkillValidationIssue>();
            SkillExecutionUsageIndex.CollectIssues(skill, diagnostics);
            foreach (var diagnostic in diagnostics)
                if (diagnostic.Severity == SkillValidationSeverity.Error) issues.Add(diagnostic.ToString());
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
        => ValidateSkillForExecution(skill, SkillExecutionKind.StandardSkill, issues);

    internal static void ValidateSkillForExecution(SkillGemDefinition skill, SkillExecutionKind kind, List<string> issues)
    {
        if (skill == null)
        {
            issues.Add("Skill asset could not be loaded.");
            return;
        }
        var diagnostics = new List<SkillValidationIssue>();
        skill.CollectValidationIssues(diagnostics, kind, includeAuthoring: true);
        foreach (var diagnostic in diagnostics)
            if (diagnostic.Severity == SkillValidationSeverity.Error)
                issues.Add(diagnostic.ToString());
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
