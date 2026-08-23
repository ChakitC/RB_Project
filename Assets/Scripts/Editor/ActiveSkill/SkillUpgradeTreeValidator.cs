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
        : this(severity, message, null)
    {
    }

    public SkillUpgradeValidationIssue(SkillUpgradeValidationSeverity severity, string message, string nodeId)
    {
        Severity = severity;
        Message = message;
        NodeId = nodeId;
    }

    public SkillUpgradeValidationSeverity Severity { get; }
    public string Message { get; }

    // Node this issue belongs to, or null for a tree-level issue. Consumers (inspector help boxes,
    // graph badges) must match on this rather than searching Message for a quoted id -- a message
    // like "Node 'a' requires missing node 'b'" names two ids but belongs only to 'a'.
    public string NodeId { get; }

    public bool BelongsTo(string nodeId) =>
        NodeId != null && string.Equals(NodeId, nodeId, StringComparison.Ordinal);
}

public static class SkillUpgradeTreeValidator
{
    const float ReferenceViewportWidth = 1320f;
    const float ReferenceViewportHeight = 740f;
    const float FitPadding = 120f;
    const float ReadableScaleWarning = 0.65f;

    public static List<SkillUpgradeValidationIssue> Validate(SkillUpgradeTreeDefinition tree)
        => Validate(tree, FindOwningAssets(tree));

    // Owner discovery scans every SkillDefinitionBase and CharacterStats asset in the project
    // (see FindOwningAssets), so callers that re-validate on every keystroke (the editor window)
    // should compute owners once and pass them in here instead of using the 1-arg overload.
    public static List<SkillUpgradeValidationIssue> Validate(
        SkillUpgradeTreeDefinition tree, IReadOnlyList<SkillDefinitionBase> owners)
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

        owners ??= FindOwningAssets(tree);
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
            CollectStatusRouteIssues(owners, issues);

            var collectedIds = new List<string>();
            for (int ownerIndex = 0; ownerIndex < owners.Count; ownerIndex++)
                owners[ownerIndex]?.CollectUpgradeIds(collectedIds);
            for (int collectedIndex = 0; collectedIndex < collectedIds.Count; collectedIndex++)
                declaredUpgradeIds.Add(collectedIds[collectedIndex]);
        }
        var grantedUpgradeIdsInTree = new HashSet<string>(StringComparer.Ordinal);
        var grantingNodesByUpgradeId = new Dictionary<string, List<string>>(StringComparer.Ordinal);

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
                issues.Add(NodeError(nodeId, $"Duplicate node ID '{nodeId}'."));

            if (node.cost < 1)
                issues.Add(NodeError(nodeId, $"Node '{nodeId}' cost must be at least 1."));
            if (node.requiredCharacterLevel < 1)
                issues.Add(NodeError(nodeId, $"Node '{nodeId}' required character level must be at least 1."));
            if (!float.IsFinite(node.visualScale) ||
                node.visualScale < SkillUpgradeNodeData.MinVisualScale ||
                node.visualScale > SkillUpgradeNodeData.MaxVisualScale)
            {
                issues.Add(NodeError(nodeId,
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
                        issues.Add(NodeError(nodeId, $"Node '{nodeId}' uses unsupported skill stat '{modifier.stat}'."));
                    else
                        hasSupportedModifier = true;

                    if (!float.IsFinite(modifier.add) || !float.IsFinite(modifier.mul))
                        issues.Add(NodeError(nodeId, $"Node '{nodeId}' contains a non-finite stat modifier."));
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
                        issues.Add(NodeError(nodeId, $"Node '{nodeId}' has a blank granted upgrade id."));
                        continue;
                    }

                    string trimmedId = rawId.Trim();
                    grantsUpgrade = true;
                    if (!localGrantedIds.Add(trimmedId))
                        issues.Add(NodeWarning(nodeId, $"Node '{nodeId}' grants upgrade id '{trimmedId}' more than once."));

                    // Warning, not error: an id nothing consumes yet is the normal state while the
                    // tree is authored ahead of the payload. The reverse gap (a payload waiting on
                    // an id no node grants) is the error, because that feature is unreachable.
                    if (hasOwner && !declaredUpgradeIds.Contains(trimmedId))
                        issues.Add(NodeWarning(nodeId, $"Node '{nodeId}' grants upgrade id '{trimmedId}' that no owning skill declares."));

                    grantedUpgradeIdsInTree.Add(trimmedId);
                    if (!grantingNodesByUpgradeId.TryGetValue(trimmedId, out List<string> grantingNodes))
                    {
                        grantingNodes = new List<string>();
                        grantingNodesByUpgradeId[trimmedId] = grantingNodes;
                    }
                    if (!grantingNodes.Contains(nodeId))
                        grantingNodes.Add(nodeId);
                }
            }

            if (!hasSupportedModifier && !grantsUpgrade)
                issues.Add(NodeWarning(nodeId, $"Node '{nodeId}' has no gameplay effect."));
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
                    issues.Add(Error($"Owning skill declares upgrade id '{declaredId}' that no node in this tree grants."));
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
                        issues.Add(NodeError(pair.Key, $"Node '{pair.Key}' has an empty prerequisite ID."));
                        continue;
                    }

                    if (string.Equals(requiredId, pair.Key, StringComparison.Ordinal))
                        issues.Add(NodeError(pair.Key, $"Node '{pair.Key}' cannot require itself."));
                    else if (!nodesById.ContainsKey(requiredId))
                        issues.Add(NodeError(pair.Key, $"Node '{pair.Key}' requires missing node '{requiredId}'."));

                    if (!localDependencies.Add(requiredId))
                        issues.Add(NodeError(pair.Key, $"Node '{pair.Key}' repeats prerequisite '{requiredId}'."));
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
                        issues.Add(NodeError(pair.Key, $"Node '{pair.Key}' has an empty mutually-exclusive ID."));
                        continue;
                    }

                    if (string.Equals(excludedId, pair.Key, StringComparison.Ordinal))
                    {
                        issues.Add(NodeError(pair.Key, $"Node '{pair.Key}' cannot exclude itself."));
                        continue;
                    }

                    if (!nodesById.ContainsKey(excludedId))
                    {
                        issues.Add(NodeError(pair.Key, $"Node '{pair.Key}' excludes missing node '{excludedId}'."));
                        continue;
                    }

                    if (localDependencies.Contains(excludedId))
                        issues.Add(NodeError(pair.Key, $"Node '{pair.Key}' both requires and excludes node '{excludedId}'."));

                    localExclusions.Add(excludedId);
                }
            }

            exclusionsByNode[pair.Key] = localExclusions;
        }

        foreach (KeyValuePair<string, HashSet<string>> entry in exclusionsByNode)
        {
            foreach (string excludedId in entry.Value)
            {
                if (exclusionsByNode.TryGetValue(excludedId, out HashSet<string> reciprocal) && reciprocal.Contains(entry.Key))
                    continue;

                // Both halves of a one-way pair get their own issue so the graph badges and the
                // node inspector show the problem from whichever node the author is looking at.
                issues.Add(NodeError(entry.Key,
                    $"Node '{entry.Key}' excludes '{excludedId}' but '{excludedId}' does not exclude it back."));
                issues.Add(NodeError(excludedId,
                    $"Node '{excludedId}' is excluded by '{entry.Key}' but does not exclude it back."));
            }
        }

        ValidateCrossNodeGrantDuplicates(grantingNodesByUpgradeId, exclusionsByNode, issues);
        DetectCycles(nodesById, issues);
        ValidateNodeOverlaps(nodesById, issues);
        ValidateReadableBounds(nodesById.Values, issues);
        return issues;
    }

    static void CollectStatusRouteIssues(
        IReadOnlyList<SkillDefinitionBase> owners,
        List<SkillUpgradeValidationIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int ownerIndex = 0; ownerIndex < owners.Count; ownerIndex++)
        {
            if (owners[ownerIndex] is not SkillGemDefinition skill)
                continue;

            SkillStatusRouteResolutionResult resolution = SkillStatusRouteResolver.ResolveDetailed(skill);
            for (int issueIndex = 0; issueIndex < resolution.Issues.Count; issueIndex++)
            {
                string skillName = string.IsNullOrEmpty(skill.name) ? "<unnamed skill>" : skill.name;
                string message = $"Skill '{skillName}' has invalid conditional status route metadata: " +
                                 resolution.Issues[issueIndex];
                if (seen.Add(message))
                    issues.Add(Error(message));
            }
        }
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
            if (stats == null)
                continue;

            if (stats.skillSlots != null)
            {
                for (int slotIndex = 0; slotIndex < stats.skillSlots.Count; slotIndex++)
                    AppendSlotOverrideOwners(stats.skillSlots[slotIndex], tree, owners);
            }

            AppendSlotOverrideOwners(stats.helperCommandSlot, tree, owners);

            // A Helper proc points at its tree through the proc's execution skill, so the
            // execution gem is the owner the tree editor and upgrade-id scan must see.
            if (stats.helperProcSlots == null)
                continue;

            for (int slotIndex = 0; slotIndex < stats.helperProcSlots.Count; slotIndex++)
            {
                HelperProcLoadoutSlot procSlot = stats.helperProcSlots[slotIndex];
                if (procSlot?.Options == null)
                    continue;

                for (int optionIndex = 0; optionIndex < procSlot.Options.Count; optionIndex++)
                {
                    SkillGemDefinition execution = procSlot.Options[optionIndex]?.ExecutionSkill;
                    if (execution != null && execution.UpgradeTree == tree && !owners.Contains(execution))
                        owners.Add(execution);
                }
            }
        }

        return owners;
    }

    static void AppendSlotOverrideOwners(
        CharacterSkillLoadoutSlot slot,
        SkillUpgradeTreeDefinition tree,
        List<SkillDefinitionBase> owners)
    {
        if (slot?.Options == null)
            return;

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

    // Upgrade ids the owning skill(s) declare but that no node in this tree grants yet -- the
    // same gap Validate() reports as a warning. Used by the graph's "Add Node" context menu so
    // authoring a node for one of these ids is a single click instead of hunting the dropdown.
    public static List<string> GetUngrantedUpgradeIds(SkillUpgradeTreeDefinition tree)
    {
        var result = new List<string>();
        if (tree == null)
            return result;

        IReadOnlyList<SkillDefinitionBase> owners = FindOwningAssets(tree);
        var declared = new List<string>();
        for (int i = 0; i < owners.Count; i++)
            owners[i]?.CollectUpgradeIds(declared);

        var granted = new HashSet<string>(StringComparer.Ordinal);
        if (tree.nodes != null)
        {
            for (int i = 0; i < tree.nodes.Count; i++)
            {
                List<string> grantedIds = tree.nodes[i]?.grantedUpgradeIds;
                if (grantedIds == null)
                    continue;

                for (int j = 0; j < grantedIds.Count; j++)
                {
                    if (!string.IsNullOrWhiteSpace(grantedIds[j]))
                        granted.Add(grantedIds[j].Trim());
                }
            }
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < declared.Count; i++)
        {
            string id = string.IsNullOrWhiteSpace(declared[i]) ? null : declared[i].Trim();
            if (id == null || granted.Contains(id) || !seen.Add(id))
                continue;

            result.Add(id);
        }

        result.Sort(StringComparer.Ordinal);
        return result;
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
        if (stats == null)
            return issues;

        ValidateOffRoleAuthoring(stats, issues);

        if (stats.IsHelperRole)
        {
            ValidateHelperLoadout(stats, issues);
            return issues;
        }

        if (stats.skillSlots == null)
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

    /// <summary>
    /// Data authored into the half of the loadout this character's role never reads.
    ///
    /// Runtime deliberately ignores it, so this can only ever be a silent no-op for the author -
    /// a proc that never fires, or a command slot the Helper screen never shows.
    /// </summary>
    static void ValidateOffRoleAuthoring(CharacterStats stats, List<SkillUpgradeValidationIssue> issues)
    {
        if (stats.IsHelperRole)
        {
            if (stats.skillSlots != null && stats.skillSlots.Count > 0)
                issues.Add(Warning("Helper role: authored Skill Slots are ignored at runtime."));

            return;
        }

        if (stats.helperProcSlots != null && stats.helperProcSlots.Count > 0)
            issues.Add(Warning("Stryker role: authored Helper Proc Slots are ignored at runtime."));

        if (stats.helperCommandSlot != null && stats.helperCommandSlot.Options.Count > 0)
            issues.Add(Warning("Stryker role: the authored Helper Command Slot is ignored at runtime."));
    }

    static void ValidateHelperLoadout(CharacterStats stats, List<SkillUpgradeValidationIssue> issues)
    {
        var slotIds = new HashSet<string>(StringComparer.Ordinal);

        CharacterSkillLoadoutSlot commandSlot = stats.helperCommandSlot;
        if (commandSlot != null && commandSlot.Options.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(commandSlot.ResolvedSlotId))
                issues.Add(Error("helper command slot needs an explicit stable slotId."));
            else
                slotIds.Add(CharacterSkillLoadoutKeys.HelperCommandSlotKey(commandSlot));

            var commandOptionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int optionIndex = 0; optionIndex < commandSlot.Options.Count; optionIndex++)
            {
                CharacterSkillLoadoutOption option = commandSlot.Options[optionIndex];
                if (option == null)
                    continue;

                if (string.IsNullOrWhiteSpace(option.optionId))
                    issues.Add(Error($"helper command slot, option {optionIndex} needs an explicit stable optionId."));

                if (!string.IsNullOrWhiteSpace(option.ResolvedOptionId) && !commandOptionIds.Add(option.ResolvedOptionId))
                    issues.Add(Error($"duplicate option ID '{option.ResolvedOptionId}' in the helper command slot."));

                // The party command has to be castable. A passive here would leave the command
                // button wired to something that can never run.
                if (option.IsConfigured && option.ActiveSkillAsset == null)
                {
                    issues.Add(Error(
                        $"helper command slot, option {optionIndex} must reference a SkillGemDefinition, not a passive."));
                }
            }
        }

        List<HelperProcLoadoutSlot> procSlots = stats.helperProcSlots;
        if (procSlots == null)
            return;

        for (int slotIndex = 0; slotIndex < procSlots.Count; slotIndex++)
        {
            HelperProcLoadoutSlot slot = procSlots[slotIndex];
            if (slot == null)
                continue;

            if (string.IsNullOrWhiteSpace(slot.ResolvedSlotId))
                issues.Add(Error($"helper proc slot {slotIndex} needs an explicit stable slotId."));
            else if (!slotIds.Add(CharacterSkillLoadoutKeys.HelperProcSlotKey(slot, slotIndex)))
                issues.Add(Error($"duplicate slot ID '{slot.ResolvedSlotId}' in helper proc slots."));

            var optionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int optionIndex = 0; optionIndex < slot.Options.Count; optionIndex++)
            {
                HelperProcLoadoutOption option = slot.Options[optionIndex];
                if (option == null)
                    continue;

                if (string.IsNullOrWhiteSpace(option.optionId))
                {
                    issues.Add(Error(
                        $"helper proc slot {slotIndex}, option {optionIndex} needs an explicit stable optionId."));
                }

                if (!string.IsNullOrWhiteSpace(option.ResolvedOptionId) && !optionIds.Add(option.ResolvedOptionId))
                {
                    issues.Add(Error(
                        $"duplicate option ID '{option.ResolvedOptionId}' in helper proc slot '{slot.ResolvedSlotId}'."));
                }

                if (option.helperProc == null)
                {
                    issues.Add(Error(
                        $"helper proc slot {slotIndex}, option {optionIndex} has no SkillHelperDef."));
                    continue;
                }

                // Without an execution skill the proc has nothing to run and no Skill Tree to
                // spend points in, so it would occupy a tab that does nothing.
                if (option.helperProc.executionSkill == null)
                {
                    issues.Add(Error(
                        $"helper proc '{option.helperProc.RuntimeId}' in slot {slotIndex} has no Execution Skill."));
                }
            }
        }
    }

    // Two nodes granting the same upgrade id normally means a copy-paste slip: the second node
    // costs a point and changes nothing, because HasUpgrade is a set membership test. It is
    // legitimate when the nodes are mutually exclusive -- that is how a branch choice offers the
    // same unlock down two paths -- so only an unexcluded pair is reported.
    static void ValidateCrossNodeGrantDuplicates(
        Dictionary<string, List<string>> grantingNodesByUpgradeId,
        Dictionary<string, HashSet<string>> exclusionsByNode,
        List<SkillUpgradeValidationIssue> issues)
    {
        foreach (KeyValuePair<string, List<string>> entry in grantingNodesByUpgradeId)
        {
            List<string> nodeIds = entry.Value;
            if (nodeIds.Count < 2)
                continue;

            for (int first = 0; first < nodeIds.Count; first++)
            {
                for (int second = first + 1; second < nodeIds.Count; second++)
                {
                    if (AreMutuallyExclusive(exclusionsByNode, nodeIds[first], nodeIds[second]))
                        continue;

                    issues.Add(NodeWarning(nodeIds[first],
                        $"Node '{nodeIds[first]}' grants upgrade id '{entry.Key}', which node " +
                        $"'{nodeIds[second]}' also grants and is not mutually exclusive with."));
                    issues.Add(NodeWarning(nodeIds[second],
                        $"Node '{nodeIds[second]}' grants upgrade id '{entry.Key}', which node " +
                        $"'{nodeIds[first]}' also grants and is not mutually exclusive with."));
                }
            }
        }
    }

    static bool AreMutuallyExclusive(
        Dictionary<string, HashSet<string>> exclusionsByNode,
        string first,
        string second)
    {
        return exclusionsByNode.TryGetValue(first, out HashSet<string> exclusions) &&
               exclusions.Contains(second);
    }

    static void DetectCycles(
        Dictionary<string, SkillUpgradeNodeData> nodesById,
        List<SkillUpgradeValidationIssue> issues)
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string nodeId in nodesById.Keys)
        {
            if (Visit(nodeId, nodesById, state))
                issues.Add(NodeError(nodeId, $"Prerequisite cycle detected from node '{nodeId}'."));
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
                    issues.Add(NodeWarning(first.Key,
                        $"Node '{first.Key}' overlaps node '{second.Key}' at runtime."));
                    issues.Add(NodeWarning(second.Key,
                        $"Node '{second.Key}' overlaps node '{first.Key}' at runtime."));
                }
            }
        }
    }

    static SkillUpgradeValidationIssue Error(string message)
        => new(SkillUpgradeValidationSeverity.Error, message);

    static SkillUpgradeValidationIssue Warning(string message)
        => new(SkillUpgradeValidationSeverity.Warning, message);

    static SkillUpgradeValidationIssue NodeError(string nodeId, string message)
        => new(SkillUpgradeValidationSeverity.Error, message, nodeId);

    static SkillUpgradeValidationIssue NodeWarning(string nodeId, string message)
        => new(SkillUpgradeValidationSeverity.Warning, message, nodeId);
}
