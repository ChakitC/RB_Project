using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Fails the suite when any map content in the project has an error-level defect. Warnings are
/// reported but do not fail, because runtime still has a working fallback for them.
/// </summary>
public sealed class MapContentValidationTests
{
    [Test]
    public void ProjectMapContentHasNoErrors()
    {
        List<MapContentIssue> issues = MapContentValidator.ValidateProject();

        var errors = new List<string>();
        var warnings = new List<string>();
        for (int i = 0; i < issues.Count; i++)
        {
            if (issues[i].IsError)
                errors.Add(issues[i].ToString());
            else
                warnings.Add(issues[i].ToString());
        }

        if (warnings.Count > 0)
            UnityEngine.Debug.Log($"[MapContentValidator] {warnings.Count} warning(s):\n{string.Join("\n", warnings)}");

        Assert.That(errors, Is.Empty, $"{errors.Count} map content error(s):\n{string.Join("\n", errors)}");
    }

    [Test]
    public void StageIdsAreUnique()
    {
        List<MapRunConfigSO> configs = MapContentValidator.LoadAllRunConfigs();
        var byStageId = new Dictionary<string, string>(System.StringComparer.Ordinal);

        for (int i = 0; i < configs.Count; i++)
        {
            string stageId = configs[i].StageId;
            if (string.IsNullOrEmpty(stageId))
                continue;

            Assert.That(
                byStageId.ContainsKey(stageId),
                Is.False,
                $"Stage Id '{stageId}' is used by both '{(byStageId.TryGetValue(stageId, out string other) ? other : string.Empty)}' and '{configs[i].name}'.");
            byStageId[stageId] = configs[i].name;
        }
    }
}
