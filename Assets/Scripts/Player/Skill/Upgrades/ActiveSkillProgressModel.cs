using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ActiveSkillProgressModel
{
    readonly CharacterStats _stats;
    readonly CharacterProgressData _data;

    int _characterLevel;

    public ActiveSkillProgressModel(CharacterStats stats, CharacterProgressData data, int characterLevel)
    {
        _stats = stats;
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _characterLevel = Mathf.Max(1, characterLevel);
        EnsureCollections();
    }

    public CharacterProgressData Data => _data;
    public int AvailablePoints => Mathf.Max(0, _data.activeSkillPoints);
    public int CharacterLevel => _characterLevel;

    public bool EnsureInitialized()
    {
        if (_data.activeSkillProgressInitialized)
            return false;

        _data.activeSkillProgressInitialized = true;
        int pointsPerLevel = _stats != null ? Mathf.Max(0, _stats.activeSkillPointsPerLevel) : 1;
        int catchUpPoints = Mathf.Max(0, _characterLevel - 1) * pointsPerLevel;
        _data.activeSkillPoints = Mathf.Max(_data.activeSkillPoints, catchUpPoints);
        return true;
    }

    public void SetCharacterLevel(int level)
    {
        _characterLevel = Mathf.Max(1, level);
        _data.level = _characterLevel;
    }

    public bool GrantPoints(int amount)
    {
        if (amount <= 0)
            return false;

        _data.activeSkillPoints = Mathf.Max(0, _data.activeSkillPoints) + amount;
        return true;
    }

    public bool IsUnlocked(
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree,
        string nodeId,
        out bool progressChanged)
    {
        progressChanged = false;
        CharacterSkillTreeProgressSaveData progress = GetTreeProgress(slotId, optionId, tree, false, out progressChanged);
        return progress != null && FindUnlockedNode(progress, nodeId) != null;
    }

    public bool CanUnlock(
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree,
        string nodeId,
        out string reason,
        out bool progressChanged)
    {
        reason = string.Empty;
        progressChanged = false;

        if (!ValidateTreeKey(slotId, optionId, tree, out reason))
            return false;

        if (!tree.TryGetNode(nodeId, out SkillUpgradeNodeData node) || node == null)
        {
            reason = "Node not found.";
            return false;
        }

        CharacterSkillTreeProgressSaveData progress = GetTreeProgress(slotId, optionId, tree, true, out progressChanged);
        if (FindUnlockedNode(progress, node.RuntimeNodeId) != null)
        {
            reason = "Already unlocked.";
            return false;
        }

        int requiredLevel = Mathf.Max(1, node.requiredCharacterLevel);
        if (_characterLevel < requiredLevel)
        {
            reason = $"Requires character level {requiredLevel}.";
            return false;
        }

        int cost = Mathf.Max(1, node.cost);
        if (AvailablePoints < cost)
        {
            reason = $"Requires {cost} Active Skill Point{(cost == 1 ? string.Empty : "s")}.";
            return false;
        }

        if (node.requiredNodeIds != null)
        {
            for (int i = 0; i < node.requiredNodeIds.Count; i++)
            {
                string requiredNodeId = node.requiredNodeIds[i];
                if (string.IsNullOrWhiteSpace(requiredNodeId))
                    continue;

                if (FindUnlockedNode(progress, requiredNodeId.Trim()) != null)
                    continue;

                reason = $"Requires node {requiredNodeId.Trim()}.";
                return false;
            }
        }

        if (node.mutuallyExclusiveNodeIds != null)
        {
            for (int i = 0; i < node.mutuallyExclusiveNodeIds.Count; i++)
            {
                string exclusiveNodeId = node.mutuallyExclusiveNodeIds[i];
                if (string.IsNullOrWhiteSpace(exclusiveNodeId))
                    continue;

                CharacterSkillUpgradeNodeSaveData unlockedExclusive = FindUnlockedNode(progress, exclusiveNodeId.Trim());
                if (unlockedExclusive == null)
                    continue;

                reason = $"Excludes node {exclusiveNodeId.Trim()}. Respec to switch branches.";
                return false;
            }
        }

        return true;
    }

    public bool TryUnlock(
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree,
        string nodeId,
        out string reason)
    {
        if (!CanUnlock(slotId, optionId, tree, nodeId, out reason, out _))
            return false;

        tree.TryGetNode(nodeId, out SkillUpgradeNodeData node);
        CharacterSkillTreeProgressSaveData progress = GetTreeProgress(slotId, optionId, tree, true, out _);
        int paidCost = Mathf.Max(1, node.cost);

        progress.unlockedNodes.Add(new CharacterSkillUpgradeNodeSaveData
        {
            nodeId = node.RuntimeNodeId,
            paidCost = paidCost,
        });

        _data.activeSkillPoints = Mathf.Max(0, AvailablePoints - paidCost);
        return true;
    }

    public bool ResetTree(
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree,
        out int refundedPoints)
    {
        refundedPoints = 0;
        CharacterSkillTreeProgressSaveData progress = GetTreeProgress(slotId, optionId, tree, false, out _);
        if (progress == null || progress.unlockedNodes == null || progress.unlockedNodes.Count == 0)
            return false;

        refundedPoints = SumPaidCost(progress);
        progress.unlockedNodes.Clear();
        _data.activeSkillPoints = AvailablePoints + refundedPoints;
        return true;
    }

    public SkillUpgradeStatSnapshot BuildSnapshot(
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree,
        out bool progressChanged)
    {
        var snapshot = new SkillUpgradeStatSnapshot();
        CharacterSkillTreeProgressSaveData progress = GetTreeProgress(slotId, optionId, tree, false, out progressChanged);
        if (progress == null || progress.unlockedNodes == null || tree == null)
            return snapshot;

        for (int i = 0; i < progress.unlockedNodes.Count; i++)
        {
            CharacterSkillUpgradeNodeSaveData savedNode = progress.unlockedNodes[i];
            if (savedNode == null || !tree.TryGetNode(savedNode.nodeId, out SkillUpgradeNodeData node))
                continue;

            snapshot.AddNode(node);
        }

        return snapshot;
    }

    public IReadOnlyList<CharacterSkillUpgradeNodeSaveData> GetUnlockedNodes(
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree,
        out bool progressChanged)
    {
        CharacterSkillTreeProgressSaveData progress = GetTreeProgress(slotId, optionId, tree, false, out progressChanged);
        return progress != null && progress.unlockedNodes != null
            ? progress.unlockedNodes
            : Array.Empty<CharacterSkillUpgradeNodeSaveData>();
    }

    CharacterSkillTreeProgressSaveData GetTreeProgress(
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree,
        bool create,
        out bool changed)
    {
        changed = false;
        if (string.IsNullOrWhiteSpace(slotId) || string.IsNullOrWhiteSpace(optionId) || tree == null)
            return null;

        string resolvedSlotId = slotId.Trim();
        string resolvedOptionId = optionId.Trim();
        CharacterSkillTreeProgressSaveData progress = null;

        for (int i = _data.activeSkillTrees.Count - 1; i >= 0; i--)
        {
            CharacterSkillTreeProgressSaveData candidate = _data.activeSkillTrees[i];
            if (candidate == null)
            {
                _data.activeSkillTrees.RemoveAt(i);
                changed = true;
                continue;
            }

            if (!string.Equals(candidate.slotId, resolvedSlotId, StringComparison.Ordinal) ||
                !string.Equals(candidate.optionId, resolvedOptionId, StringComparison.Ordinal))
            {
                continue;
            }

            if (progress == null)
            {
                progress = candidate;
                continue;
            }

            _data.activeSkillPoints += SumPaidCost(candidate);
            _data.activeSkillTrees.RemoveAt(i);
            changed = true;
        }

        string resolvedTreeId = tree.RuntimeTreeId;
        if (progress != null && !string.Equals(progress.treeId, resolvedTreeId, StringComparison.Ordinal))
        {
            _data.activeSkillPoints += SumPaidCost(progress);
            progress.treeId = resolvedTreeId;
            progress.unlockedNodes = new List<CharacterSkillUpgradeNodeSaveData>();
            changed = true;
        }

        if (progress != null)
        {
            progress.unlockedNodes ??= new List<CharacterSkillUpgradeNodeSaveData>();
            return progress;
        }

        if (!create)
            return null;

        progress = new CharacterSkillTreeProgressSaveData
        {
            slotId = resolvedSlotId,
            optionId = resolvedOptionId,
            treeId = resolvedTreeId,
        };
        _data.activeSkillTrees.Add(progress);
        changed = true;
        return progress;
    }

    void EnsureCollections()
    {
        _data.activeSkillTrees ??= new List<CharacterSkillTreeProgressSaveData>();
        _data.selectedSkillOptions ??= new List<CharacterSkillSelectionSaveData>();
        _data.unlockedPassiveNodeIds ??= new List<string>();
    }

    static bool ValidateTreeKey(
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree,
        out string reason)
    {
        if (string.IsNullOrWhiteSpace(slotId))
        {
            reason = "Missing slot ID.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(optionId))
        {
            reason = "Missing option ID.";
            return false;
        }

        if (tree == null)
        {
            reason = "Missing Active Skill Tree.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    static CharacterSkillUpgradeNodeSaveData FindUnlockedNode(
        CharacterSkillTreeProgressSaveData progress,
        string nodeId)
    {
        if (progress == null || progress.unlockedNodes == null || string.IsNullOrWhiteSpace(nodeId))
            return null;

        string resolvedNodeId = nodeId.Trim();
        for (int i = 0; i < progress.unlockedNodes.Count; i++)
        {
            CharacterSkillUpgradeNodeSaveData candidate = progress.unlockedNodes[i];
            if (candidate != null && string.Equals(candidate.nodeId, resolvedNodeId, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    static int SumPaidCost(CharacterSkillTreeProgressSaveData progress)
    {
        if (progress == null || progress.unlockedNodes == null)
            return 0;

        int total = 0;
        for (int i = 0; i < progress.unlockedNodes.Count; i++)
        {
            CharacterSkillUpgradeNodeSaveData node = progress.unlockedNodes[i];
            if (node != null)
                total += Mathf.Max(0, node.paidCost);
        }

        return total;
    }
}
