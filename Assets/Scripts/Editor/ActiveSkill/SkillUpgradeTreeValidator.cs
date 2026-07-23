using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum SkillUpgradeValidationSeverity
{
    Warning,
    Error,
}

public readonly struct SkillUpgradeValidationIssue
{
    public SkillUpgradeValidationIssue(SkillUpgradeValidationSeverity severity, string message)
    {
        Severity = severity;
        Message = message;
    }

    public SkillUpgradeValidationSeverity Severity { get; }
    public string Message { get; }
}

public static class SkillUpgradeTreeValidator
{
    const float ReferenceViewportWidth = 1320f;
    const float ReferenceViewportHeight = 740f;
    const float FitPadding = 120f;
    const float ReadableScaleWarning = 0.65f;

    public static List<SkillUpgradeValidationIssue> Validate(SkillUpgradeTreeDefinition tree)
    {
        var issues = new List<SkillUpgradeValidationIssue>();
        if (tree == null)
        {
            issues.Add(Error("Tree is null."));
            return issues;
        }

        if (string.IsNullOrWhiteSpace(tree.treeId))
            issues.Add(Error("Tree ID is required and must remain stable after release."));

        var nodesById = new Dictionary<string, SkillUpgradeNodeData>(StringComparer.Ordinal);
        if (tree.nodes == null)
        {
            issues.Add(Error("Node list is null."));
            return issues;
        }

        for (int i = 0; i < tree.nodes.Count; i++)
        {
            SkillUpgradeNodeData node = tree.nodes[i];
            if (node == null)
            {
                issues.Add(Error($"Node entry {i} is null."));
                continue;
            }

            string nodeId = node.RuntimeNodeId;
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                issues.Add(Error($"Node entry {i} has no ID."));
                continue;
            }

            if (!nodesById.TryAdd(nodeId, node))
                issues.Add(Error($"Duplicate node ID '{nodeId}'."));

            if (node.cost < 1)
                issues.Add(Error($"Node '{nodeId}' cost must be at least 1."));
            if (node.requiredCharacterLevel < 1)
                issues.Add(Error($"Node '{nodeId}' required character level must be at least 1."));
            if (!float.IsFinite(node.visualScale) ||
                node.visualScale < SkillUpgradeNodeData.MinVisualScale ||
                node.visualScale > SkillUpgradeNodeData.MaxVisualScale)
            {
                issues.Add(Error(
                    $"Node '{nodeId}' visual scale must be between " +
                    $"{SkillUpgradeNodeData.MinVisualScale:0.##} and {SkillUpgradeNodeData.MaxVisualScale:0.##}."));
            }

            bool hasSupportedModifier = false;
            if (node.statModifiers != null)
            {
                for (int modifierIndex = 0; modifierIndex < node.statModifiers.Count; modifierIndex++)
                {
                    StatModifier modifier = node.statModifiers[modifierIndex];
                    if (modifier == null)
                        continue;

                    if (!SkillUpgradeStatSnapshot.Supports(modifier.stat))
                        issues.Add(Error($"Node '{nodeId}' uses unsupported skill stat '{modifier.stat}'."));
                    else
                        hasSupportedModifier = true;

                    if (!float.IsFinite(modifier.add) || !float.IsFinite(modifier.mul))
                        issues.Add(Error($"Node '{nodeId}' contains a non-finite stat modifier."));
                }
            }

            if (node.skillLevelDelta == 0 && !hasSupportedModifier)
                issues.Add(Warning($"Node '{nodeId}' has no gameplay effect."));
        }

        foreach (KeyValuePair<string, SkillUpgradeNodeData> pair in nodesById)
        {
            SkillUpgradeNodeData node = pair.Value;
            if (node.requiredNodeIds == null)
                continue;

            var localDependencies = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < node.requiredNodeIds.Count; i++)
            {
                string requiredId = string.IsNullOrWhiteSpace(node.requiredNodeIds[i])
                    ? string.Empty
                    : node.requiredNodeIds[i].Trim();

                if (string.IsNullOrWhiteSpace(requiredId))
                {
                    issues.Add(Error($"Node '{pair.Key}' has an empty prerequisite ID."));
                    continue;
                }

                if (string.Equals(requiredId, pair.Key, StringComparison.Ordinal))
                    issues.Add(Error($"Node '{pair.Key}' cannot require itself."));
                else if (!nodesById.ContainsKey(requiredId))
                    issues.Add(Error($"Node '{pair.Key}' requires missing node '{requiredId}'."));

                if (!localDependencies.Add(requiredId))
                    issues.Add(Error($"Node '{pair.Key}' repeats prerequisite '{requiredId}'."));
            }
        }

        DetectCycles(nodesById, issues);
        ValidateNodeOverlaps(nodesById, issues);
        ValidateReadableBounds(nodesById.Values, issues);
        return issues;
    }

    [MenuItem("Tools/RB/Skills/Validate Active Skill Trees")]
    public static void ValidateProject()
    {
        int errorCount = 0;
        int warningCount = 0;
        string[] treeGuids = AssetDatabase.FindAssets("t:SkillUpgradeTreeDefinition");
        for (int i = 0; i < treeGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(treeGuids[i]);
            SkillUpgradeTreeDefinition tree = AssetDatabase.LoadAssetAtPath<SkillUpgradeTreeDefinition>(path);
            List<SkillUpgradeValidationIssue> issues = Validate(tree);
            for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
            {
                SkillUpgradeValidationIssue issue = issues[issueIndex];
                string message = $"[ActiveSkillTree] {path}: {issue.Message}";
                if (issue.Severity == SkillUpgradeValidationSeverity.Error)
                {
                    errorCount++;
                    Debug.LogError(message, tree);
                }
                else
                {
                    warningCount++;
                    Debug.LogWarning(message, tree);
                }
            }
        }

        ValidateCharacterLoadoutIds(ref errorCount, ref warningCount);
        Debug.Log($"[ActiveSkillTree] Validation complete: {treeGuids.Length} trees, {errorCount} errors, {warningCount} warnings.");
    }

    static void ValidateCharacterLoadoutIds(ref int errorCount, ref int warningCount)
    {
        string[] statGuids = AssetDatabase.FindAssets("t:CharacterStats");
        for (int i = 0; i < statGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(statGuids[i]);
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(path);
            if (stats == null || stats.skillSlots == null)
                continue;

            var slotIds = new HashSet<string>(StringComparer.Ordinal);
            for (int slotIndex = 0; slotIndex < stats.skillSlots.Count; slotIndex++)
            {
                CharacterSkillLoadoutSlot slot = stats.skillSlots[slotIndex];
                if (slot == null)
                    continue;

                if (string.IsNullOrWhiteSpace(slot.ResolvedSlotId))
                {
                    errorCount++;
                    Debug.LogError($"[ActiveSkillTree] {path}: slot {slotIndex} needs an explicit stable slotId.", stats);
                }
                else if (!slotIds.Add(slot.ResolvedSlotId))
                {
                    errorCount++;
                    Debug.LogError($"[ActiveSkillTree] {path}: duplicate slot ID '{slot.ResolvedSlotId}'.", stats);
                }

                var optionIds = new HashSet<string>(StringComparer.Ordinal);
                for (int optionIndex = 0; optionIndex < slot.Options.Count; optionIndex++)
                {
                    CharacterSkillLoadoutOption option = slot.Options[optionIndex];
                    if (option == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(option.optionId))
                    {
                        errorCount++;
                        Debug.LogError($"[ActiveSkillTree] {path}: slot {slotIndex}, option {optionIndex} needs an explicit stable optionId.", stats);
                    }

                    if (!string.IsNullOrWhiteSpace(option.ResolvedOptionId) && !optionIds.Add(option.ResolvedOptionId))
                    {
                        errorCount++;
                        Debug.LogError($"[ActiveSkillTree] {path}: duplicate option ID '{option.ResolvedOptionId}' in slot '{slot.ResolvedSlotId}'.", stats);
                    }
                }
            }
        }
    }

    static void DetectCycles(
        Dictionary<string, SkillUpgradeNodeData> nodesById,
        List<SkillUpgradeValidationIssue> issues)
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string nodeId in nodesById.Keys)
        {
            if (Visit(nodeId, nodesById, state))
                issues.Add(Error($"Prerequisite cycle detected from node '{nodeId}'."));
        }
    }

    static bool Visit(
        string nodeId,
        Dictionary<string, SkillUpgradeNodeData> nodesById,
        Dictionary<string, int> state)
    {
        if (state.TryGetValue(nodeId, out int currentState))
            return currentState == 1;

        state[nodeId] = 1;
        SkillUpgradeNodeData node = nodesById[nodeId];
        if (node.requiredNodeIds != null)
        {
            for (int i = 0; i < node.requiredNodeIds.Count; i++)
            {
                string dependency = node.requiredNodeIds[i];
                if (string.IsNullOrWhiteSpace(dependency) || !nodesById.ContainsKey(dependency.Trim()))
                    continue;

                if (Visit(dependency.Trim(), nodesById, state))
                    return true;
            }
        }

        state[nodeId] = 2;
        return false;
    }

    static void ValidateReadableBounds(
        Dictionary<string, SkillUpgradeNodeData>.ValueCollection nodes,
        List<SkillUpgradeValidationIssue> issues)
    {
        bool hasBounds = false;
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;
        foreach (SkillUpgradeNodeData node in nodes)
        {
            Rect bounds = node.RuntimeUiBounds;
            if (!hasBounds)
            {
                min = bounds.min;
                max = bounds.max;
                hasBounds = true;
            }
            else
            {
                min = Vector2.Min(min, bounds.min);
                max = Vector2.Max(max, bounds.max);
            }
        }

        if (!hasBounds)
            return;

        Vector2 size = max - min + Vector2.one * FitPadding;
        float scale = Mathf.Min(
            size.x > 0f ? ReferenceViewportWidth / size.x : 1f,
            size.y > 0f ? ReferenceViewportHeight / size.y : 1f,
            1f);
        if (scale < ReadableScaleWarning)
            issues.Add(Warning($"Runtime auto-fit scale is approximately {scale:0.00}; node icons may be difficult to read."));
    }

    static void ValidateNodeOverlaps(
        Dictionary<string, SkillUpgradeNodeData> nodesById,
        List<SkillUpgradeValidationIssue> issues)
    {
        var nodes = new List<KeyValuePair<string, SkillUpgradeNodeData>>(nodesById);
        for (int firstIndex = 0; firstIndex < nodes.Count; firstIndex++)
        {
            KeyValuePair<string, SkillUpgradeNodeData> first = nodes[firstIndex];
            for (int secondIndex = firstIndex + 1; secondIndex < nodes.Count; secondIndex++)
            {
                KeyValuePair<string, SkillUpgradeNodeData> second = nodes[secondIndex];
                if (first.Value.RuntimeUiBounds.Overlaps(second.Value.RuntimeUiBounds))
                {
                    issues.Add(Warning(
                        $"Nodes '{first.Key}' and '{second.Key}' overlap at runtime."));
                }
            }
        }
    }

    static SkillUpgradeValidationIssue Error(string message)
        => new(SkillUpgradeValidationSeverity.Error, message);

    static SkillUpgradeValidationIssue Warning(string message)
        => new(SkillUpgradeValidationSeverity.Warning, message);
}
