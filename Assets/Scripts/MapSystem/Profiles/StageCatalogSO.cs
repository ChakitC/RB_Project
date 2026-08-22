using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The ordered list of stages the Basement board offers. Adding a stage is an asset edit here rather
/// than a hand-authored page in the scene.
/// </summary>
[CreateAssetMenu(menuName = "Game/Map/Profiles/Stage Catalog")]
public class StageCatalogSO : ScriptableObject
{
    [Tooltip("ด่านทั้งหมดที่บอร์ดใน Basement รู้จัก")]
    [SerializeField] private StageDefinitionSO[] stages;

    public StageDefinitionSO[] Stages => stages;

    /// <summary>Board-visible stages, ordered by Board Order and then by their position in the list.</summary>
    public List<StageDefinitionSO> GetBoardStages()
    {
        var result = new List<StageDefinitionSO>();
        if (stages == null)
            return result;

        var ordered = new List<(StageDefinitionSO Stage, int Index)>();
        for (int i = 0; i < stages.Length; i++)
        {
            StageDefinitionSO stage = stages[i];
            if (stage != null && !stage.HiddenOnBoard)
                ordered.Add((stage, i));
        }

        // List.Sort is not stable, so the authored index is part of the comparison. Stages that
        // share a Board Order then keep the order they were written in.
        ordered.Sort((left, right) =>
        {
            int byOrder = left.Stage.BoardOrder.CompareTo(right.Stage.BoardOrder);
            return byOrder != 0 ? byOrder : left.Index.CompareTo(right.Index);
        });

        for (int i = 0; i < ordered.Count; i++)
            result.Add(ordered[i].Stage);

        return result;
    }

    public StageDefinitionSO Find(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId) || stages == null)
            return null;

        for (int i = 0; i < stages.Length; i++)
        {
            StageDefinitionSO stage = stages[i];
            if (stage != null && string.Equals(stage.ResolveStageId(), stageId, System.StringComparison.Ordinal))
                return stage;
        }

        return null;
    }
}
