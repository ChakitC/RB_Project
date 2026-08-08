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

        IReadOnlyList<SkillDefinitionBase> owners = FindOwningAssets(tree);
        bool hasOwner = owners.Count > 0;
        bool ownedByPassive = false;
        for (int ownerIndex = 0; ownerIndex < owners.Count; ownerIndex++)
        {
            if (owners[ownerIndex] is PassiveDefinition)
            {
                ownedByPassive = true;
                break;
            }
        }
        var declaredUpgradeIds = new HashSet<string>(StringComparer.Ordinal);
        if (hasOwner)
        {
            var collectedIds = new List<string>();
            for (int ownerIndex = 0; ownerIndex < owners.Count; ownerIndex++)
                owners[ownerIndex]?.CollectUpgradeIds(collectedIds);
            for (int collectedIndex = 0; collectedIndex < collectedIds.Count; collectedIndex++)
                declaredUpgradeIds.Add(collectedIds[collectedIndex]);
        }
        var grantedUpgradeIdsInTree = new HashSet<string>(StringComparer.Ordinal);

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

                    if (!SkillUpgradeStatSnapshot.Supports(modifier.stat) && !ownedByPassive)
                        issues.Add(Error($"Node '{nodeId}' uses unsupported skill stat '{modifier.stat}'."));
                    else
                        hasSupportedModifier = true;

                    if (!float.IsFinite(modifier.add) || !float.IsFinite(modifier.mul))
                        issues.Add(Error($"Node '{nodeId}' contains a non-finite stat modifier."));
                }
            }

            bool grantsUpgrade = false;
            if (node.grantedUpgradeIds != null)
            {
                var localGrantedIds = new HashSet<string>(StringComparer.Ordinal);
                for (int grantIndex = 0; grantIndex < node.grantedUpgradeIds.Count; grantIndex++)
                {
                    string rawId = node.grantedUpgradeIds[grantIndex];
                    if (string.IsNullOrWhiteSpace(rawId))
                    {
                        issues.Add(Error($"Node '{nodeId}' has a blank granted upgrade id."));
                        continue;
                    }

                    string trimmedId = rawId.Trim();
                    grantsUpgrade = true;
                    if (!localGrantedIds.Add(trimmedId))
                        issues.Add(Warning($"Node '{nodeId}' grants upgrade id '{trimmedId}' more than once."));

                    if (hasOwner && !declaredUpgradeIds.Contains(trimmedId))
                        issues.Add(Error($"Node '{nodeId}' grants upgrade id '{trimmedId}' that no owning skill declares."));

                    grantedUpgradeIdsInTree.Add(trimmedId);
                }
            }

            if (!hasSupportedModifier && !grantsUpgrade)
                issues.Add(Warning($"Node '{nodeId}' has no gameplay effect."));
        }

        if (!hasOwner)
        {
            if (grantedUpgradeIdsInTree.Count > 0)
                issues.Add(Warning("Tree has no owning skill; granted upgrade ids cannot be cross-checked."));
        }
        else
        {
            foreach (string declaredId in declaredUpgradeIds)
            {
                if (!grantedUpgradeIdsInTree.Contains(declaredId))
                    issues.Add(Warning($"Owning skill declares upgrade id '{declaredId}' that no node in this tree grants."));
            }
        }

        var exclusionsByNode = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, SkillUpgradeNodeData> pair in nodesById)
        {
            SkillUpgradeNodeData node = pair.Value;

            var localDependencies = new HashSet<string>(StringComparer.Ordinal);
            if (node.requiredNodeIds != null)
            {
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

            var localExclusions = new HashSet<string>(StringComparer.Ordinal);
            if (node.mutuallyExclusiveNodeIds != null)
            {
                for (int i = 0; i < node.mutuallyExclusiveNodeIds.Count; i++)
                {
                    string excludedId = string.IsNullOrWhiteSpace(node.mutuallyExclusiveNodeIds[i])
                        ? string.Empty
                        : node.mutuallyExclusiveNodeIds[i].Trim();

                    if (string.IsNullOrWhiteSpace(excludedId))
                    {
                        issues.Add(Error($"Node '{pair.Key}' has an empty mutually-exclusive ID."));
                        continue;
                    }

                    if (string.Equals(excludedId, pair.Key, StringComparison.Ordinal))
                    {
                        issues.Add(Error($"Node '{pair.Key}' cannot exclude itself."));
                        continue;
                    }

                    if (!nodesById.ContainsKey(excludedId))
                    {
                        issues.Add(Error($"Node '{pair.Key}' excludes missing node '{excludedId}'."));
                        continue;
                    }

                    if (localDependencies.Contains(excludedId))
                        issues.Add(Error($"Node '{pair.Key}' both requires and excludes node '{excludedId}'."));

                    localExclusions.Add(excludedId);
                }
            }

            exclusionsByNode[pair.Key] = localExclusions;
        }

        foreach (KeyValuePair<string, HashSet<string>> entry in exclusionsByNode)
        {
            foreach (string excludedId in entry.Value)
            {
                if (!exclusionsByNode.TryGetValue(excludedId, out HashSet<string> reciprocal) || !reciprocal.Contains(entry.Key))
                    issues.Add(Error($"Node '{entry.Key}' excludes '{excludedId}' but '{excludedId}' does not exclude it back."));
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

    public static IReadOnlyList<SkillDefinitionBase> FindOwningAssets(SkillUpgradeTreeDefinition tree)
    {
        var owners = new List<SkillDefinitionBase>();
        if (tree == null)
            return owners;

        string[] assetGuids = AssetDatabase.FindAssets("t:SkillDefinitionBase");
        for (int i = 0; i < assetGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
            SkillDefinitionBase asset = AssetDatabase.LoadAssetAtPath<SkillDefinitionBase>(path);
            if (asset != null && asset.UpgradeTree == tree && !owners.Contains(asset))
                owners.Add(asset);
        }

        string[] statGuids = AssetDatabase.FindAssets("t:CharacterStats");
        for (int i = 0; i < statGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(statGuids[i]);
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(path);
            if (stats == null || stats.skillSlots == null)
                continue;

            for (int slotIndex = 0; slotIndex < stats.skillSlots.Count; slotIndex++)
            {
                CharacterSkillLoadoutSlot slot = stats.skillSlots[slotIndex];
                if (slot?.Options == null)
                    continue;

                for (int optionIndex = 0; optionIndex < slot.Options.Count; optionIndex++)
                {
                    CharacterSkillLoadoutOption option = slot.Options[optionIndex];
                    if (option != null && option.upgradeTreeOverride == tree &&
                        option.skillAsset != null && !owners.Contains(option.skillAsset))
                    {
                        owners.Add(option.skillAsset);
                    }
                }
            }
        }

        return owners;
    }

    public static IReadOnlyList<SkillGemDefinition> FindOwningSkills(SkillUpgradeTreeDefinition tree)
    {
        IReadOnlyList<SkillDefinitionBase> assets = FindOwningAssets(tree);
        var owners = new List<SkillGemDefinition>();
        for (int i = 0; i < assets.Count; i++)
        {
            if (assets[i] is SkillGemDefinition skill)
                owners.Add(skill);
        }

        return owners;
    }

    static void ValidateCharacterLoadoutIds(ref int errorCount, ref int warningCount)
    {
        string[] statGuids = AssetDatabase.FindAssets("t:CharacterStats");
        for (int i = 0; i < statGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(statGuids[i]);
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(path);
            if (stats == null)
                continue;

            List<SkillUpgradeValidationIssue> issues = ValidateCharacterLoadout(stats);
            for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
            {
                SkillUpgradeValidationIssue issue = issues[issueIndex];
                string message = $"[ActiveSkillTree] {path}: {issue.Message}";
                if (issue.Severity == SkillUpgradeValidationSeverity.Error)
                {
                    errorCount++;
                    Debug.LogError(message, stats);
                }
                else
                {
                    warningCount++;
                    Debug.LogWarning(message, stats);
                }
            }
        }
    }

    public static List<SkillUpgradeValidationIssue> ValidateCharacterLoadout(CharacterStats stats)
    {
        var issues = new List<SkillUpgradeValidationIssue>();
        if (stats == null || stats.skillSlots == null)
            return issues;

        var slotIds = new HashSet<string>(StringComparer.Ordinal);
        bool seenPassiveSlot = false;
        for (int slotIndex = 0; slotIndex < stats.skillSlots.Count; slotIndex++)
        {
            CharacterSkillLoadoutSlot slot = stats.skillSlots[slotIndex];
            if (slot == null)
                continue;

            if (string.IsNullOrWhiteSpace(slot.ResolvedSlotId))
                issues.Add(Error($"slot {slotIndex} needs an explicit stable slotId."));
            else if (!slotIds.Add(slot.ResolvedSlotId))
                issues.Add(Error($"duplicate slot ID '{slot.ResolvedSlotId}'."));

            bool slotHasActiveOption = false;
            bool slotHasPassiveOption = false;

            var optionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int optionIndex = 0; optionIndex < slot.Options.Count; optionIndex++)
            {
                CharacterSkillLoadoutOption option = slot.Options[optionIndex];
                if (option == null)
                    continue;

                if (string.IsNullOrWhiteSpace(option.optionId))
                    issues.Add(Error($"slot {slotIndex}, option {optionIndex} needs an explicit stable optionId."));

                if (!string.IsNullOrWhiteSpace(option.ResolvedOptionId) && !optionIds.Add(option.ResolvedOptionId))
                    issues.Add(Error($"duplicate option ID '{option.ResolvedOptionId}' in slot '{slot.ResolvedSlotId}'."));

                if (!option.IsConfigured)
                    continue;

                if (option.IsPassive)
                    slotHasPassiveOption = true;
                else
                    slotHasActiveOption = true;

                if (option.PassiveAsset is AlwaysOnPassiveDef && option.ResolvedUpgradeTree != null)
                {
                    issues.Add(Error(
                        $"slot '{slot.ResolvedSlotId}', option {optionIndex} resolves an upgrade tree " +
                        "on an AlwaysOnPassiveDef, which has no gate mechanism (unsupported in Phase 1)."));
                }
            }

            if (slotHasActiveOption && slotHasPassiveOption)
                issues.Add(Error($"slot '{slot.ResolvedSlotId}' mixes active and passive options."));

            if (slotHasPassiveOption)
            {
                seenPassiveSlot = true;

                if (slot.hotkey != KeyCode.None)
                    issues.Add(Warning($"passive slot '{slot.ResolvedSlotId}' has a non-None hotkey."));
            }
            else if (slotHasActiveOption && seenPassiveSlot)
            {
                issues.Add(Error($"slot '{slot.ResolvedSlotId}' is active but appears after a passive slot; passive slots must be last."));
            }
        }

        return issues;
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
