#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks the stage catalog layer: every stage resolves to a run config, stage ids stay unique, and
/// a legacy id is never something that would silently steal another stage's saved progress.
/// </summary>
public static class StageCatalogValidator
{
    public static List<MapContentIssue> ValidateProject()
    {
        var issues = new List<MapContentIssue>();
        List<StageCatalogSO> catalogs = LoadAll<StageCatalogSO>();
        List<StageDefinitionSO> stages = LoadAll<StageDefinitionSO>();

        ValidateStages(stages, issues);
        for (int i = 0; i < catalogs.Count; i++)
            ValidateCatalog(catalogs[i], issues);

        return issues;
    }

    public static List<T> LoadAll<T>() where T : ScriptableObject
    {
        var assets = new List<T>();
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        var paths = new List<string>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
            paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));

        paths.Sort(System.StringComparer.Ordinal);
        for (int i = 0; i < paths.Count; i++)
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(paths[i]);
            if (asset != null)
                assets.Add(asset);
        }

        return assets;
    }

    public static void ValidateStages(List<StageDefinitionSO> stages, List<MapContentIssue> issues)
    {
        var byStageId = new Dictionary<string, StageDefinitionSO>(System.StringComparer.Ordinal);
        var allStageIds = new HashSet<string>(System.StringComparer.Ordinal);

        for (int i = 0; i < stages.Count; i++)
        {
            string id = stages[i].ResolveStageId();
            if (!string.IsNullOrEmpty(id))
                allStageIds.Add(id);
        }

        for (int i = 0; i < stages.Count; i++)
        {
            StageDefinitionSO stage = stages[i];
            string stageId = stage.ResolveStageId();

            if (stage.RunConfig == null)
            {
                Add(issues, MapContentIssueSeverity.Error, stage, "No run config is assigned, so the stage cannot be entered.");
            }
            else if (!string.IsNullOrEmpty(stage.StageId) &&
                     !string.Equals(stage.StageId, stage.RunConfig.StageId, System.StringComparison.Ordinal))
            {
                Add(issues, MapContentIssueSeverity.Error, stage,
                    $"Stage Id '{stage.StageId}' does not match its run config's Stage Id " +
                    $"'{stage.RunConfig.StageId}'. Progress would be saved under one and read under the other.");
            }

            if (string.IsNullOrEmpty(stageId))
            {
                Add(issues, MapContentIssueSeverity.Error, stage, "The stage resolves to an empty Stage Id, so its progress cannot be saved.");
                continue;
            }

            if (byStageId.TryGetValue(stageId, out StageDefinitionSO other))
            {
                Add(issues, MapContentIssueSeverity.Error, stage,
                    $"Stage Id '{stageId}' is already used by '{other.name}'.");
            }
            else
            {
                byStageId[stageId] = stage;
            }

            ValidateLegacyIds(stage, stageId, allStageIds, issues);
        }
    }

    static void ValidateLegacyIds(
        StageDefinitionSO stage,
        string stageId,
        HashSet<string> allStageIds,
        List<MapContentIssue> issues)
    {
        string[] legacyIds = stage.LegacyStageIds;
        if (legacyIds == null)
            return;

        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < legacyIds.Length; i++)
        {
            string legacyId = legacyIds[i];
            if (string.IsNullOrWhiteSpace(legacyId))
            {
                Add(issues, MapContentIssueSeverity.Warning, stage, $"Legacy Stage Ids[{i}] is empty.");
                continue;
            }

            if (string.Equals(legacyId, stageId, System.StringComparison.Ordinal))
            {
                Add(issues, MapContentIssueSeverity.Warning, stage,
                    $"Legacy Stage Ids[{i}] repeats the current Stage Id, which does nothing.");
                continue;
            }

            if (!seen.Add(legacyId))
                Add(issues, MapContentIssueSeverity.Warning, stage, $"Legacy Stage Id '{legacyId}' is listed twice.");

            // A legacy id that is some other stage's live id would adopt that stage's progress.
            if (allStageIds.Contains(legacyId))
            {
                Add(issues, MapContentIssueSeverity.Error, stage,
                    $"Legacy Stage Id '{legacyId}' is another stage's current Stage Id. " +
                    "Loading this stage would take over that stage's saved progress.");
            }
        }
    }

    static void ValidateCatalog(StageCatalogSO catalog, List<MapContentIssue> issues)
    {
        StageDefinitionSO[] stages = catalog.Stages;
        if (stages == null || stages.Length == 0)
        {
            Add(issues, MapContentIssueSeverity.Warning, catalog, "The catalog lists no stages.");
            return;
        }

        var seen = new HashSet<StageDefinitionSO>();
        for (int i = 0; i < stages.Length; i++)
        {
            if (stages[i] == null)
            {
                Add(issues, MapContentIssueSeverity.Error, catalog, $"Stages[{i}] is empty.");
                continue;
            }

            if (!seen.Add(stages[i]))
                Add(issues, MapContentIssueSeverity.Error, catalog, $"'{stages[i].name}' is listed more than once.");
        }
    }

    static void Add(List<MapContentIssue> issues, MapContentIssueSeverity severity, Object context, string message)
    {
        issues.Add(new MapContentIssue(severity, context != null ? context.name : "<unknown>", message, context));
    }
}
#endif
