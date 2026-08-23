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
    public int AvailablePoints => Mathf.Max(0, _data.skillPoints);
    public int CharacterLevel => _characterLevel;

    public bool EnsureInitialized()
    {
        if (_data.skillProgressInitialized)
            return false;

        _data.skillProgressInitialized = true;
        int pointsPerLevel = _stats != null ? Mathf.Max(0, _stats.skillPointsPerLevel) : 1;
        int lifetimeGrant = Mathf.Max(0, _characterLevel - 1) * pointsPerLevel;
        // Points already sunk into nodes are part of the lifetime grant; without subtracting
        // them, catch-up would hand them out a second time on any save missing this flag.
        int alreadySpent = SumAllPaidCost();
        int catchUpPoints = Mathf.Max(0, lifetimeGrant - alreadySpent);
        _data.skillPoints = Mathf.Max(_data.skillPoints, catchUpPoints);
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

        _data.skillPoints = Mathf.Max(0, _data.skillPoints) + amount;
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

        // Authoring is expected to keep exclusions reciprocal (the validator errors otherwise),
        // but the runtime must not depend on that -- a one-way list would make the outcome
        // depend on which of the two nodes the player unlocked first.
        for (int i = 0; i < progress.unlockedNodes.Count; i++)
        {
            CharacterSkillUpgradeNodeSaveData saved = progress.unlockedNodes[i];
            if (saved == null || !tree.TryGetNode(saved.nodeId, out SkillUpgradeNodeData unlocked) ||
                unlocked.mutuallyExclusiveNodeIds == null)
            {
                continue;
            }

            for (int j = 0; j < unlocked.mutuallyExclusiveNodeIds.Count; j++)
            {
                string excludedByUnlocked = unlocked.mutuallyExclusiveNodeIds[j];
                if (string.IsNullOrWhiteSpace(excludedByUnlocked) ||
                    !string.Equals(excludedByUnlocked.Trim(), node.RuntimeNodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                reason = $"Excludes node {saved.nodeId}. Respec to switch branches.";
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
        => TryUnlock(slotId, optionId, tree, nodeId, out reason, out _);

    public bool TryUnlock(
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree,
        string nodeId,
        out string reason,
        out bool progressChanged)
    {
        if (!CanUnlock(slotId, optionId, tree, nodeId, out reason, out progressChanged))
            return false;

        tree.TryGetNode(nodeId, out SkillUpgradeNodeData node);
        CharacterSkillTreeProgressSaveData progress = GetTreeProgress(slotId, optionId, tree, true, out _);
        int paidCost = Mathf.Max(1, node.cost);

        progress.unlockedNodes.Add(new CharacterSkillUpgradeNodeSaveData
        {
            nodeId = node.RuntimeNodeId,
            paidCost = paidCost,
        });

        _data.skillPoints = Mathf.Max(0, AvailablePoints - paidCost);
        progressChanged = true;
        return true;
    }

    public bool ResetTree(
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree,
        out int refundedPoints)
        => ResetTree(slotId, optionId, tree, out refundedPoints, out _);

    public bool ResetTree(
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree,
        out int refundedPoints,
        out bool progressChanged)
    {
        refundedPoints = 0;
        CharacterSkillTreeProgressSaveData progress = GetTreeProgress(slotId, optionId, tree, false, out progressChanged);
        if (progress == null || progress.unlockedNodes == null || progress.unlockedNodes.Count == 0)
            return false;

        refundedPoints = SumPaidCost(progress);
        progress.unlockedNodes.Clear();
        _data.skillPoints = AvailablePoints + refundedPoints;
        progressChanged = true;
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

            _data.skillPoints += SumPaidCost(candidate);
            _data.activeSkillTrees.RemoveAt(i);
            changed = true;
        }

        string resolvedTreeId = tree.RuntimeTreeId;
        if (progress != null && !string.Equals(progress.treeId, resolvedTreeId, StringComparison.Ordinal))
        {
            _data.skillPoints += SumPaidCost(progress);
            progress.treeId = resolvedTreeId;
            progress.unlockedNodes = new List<CharacterSkillUpgradeNodeSaveData>();
            changed = true;
        }

        if (progress != null)
        {
            progress.unlockedNodes ??= new List<CharacterSkillUpgradeNodeSaveData>();

            // A node removed from the tree asset while treeId stayed the same would otherwise
            // keep its paidCost spent forever with no runtime effect (BuildSnapshot silently
            // skips nodes TryGetNode can't find). Refund it the same way a tree replacement
            // does. Dangling requiredNodeIds left behind by this are an authoring error the
            // validator already catches -- dependents are deliberately not cascade-refunded.
            for (int i = progress.unlockedNodes.Count - 1; i >= 0; i--)
            {
                CharacterSkillUpgradeNodeSaveData saved = progress.unlockedNodes[i];
                if (saved != null && tree.TryGetNode(saved.nodeId, out _))
                    continue;

                if (saved != null)
                    _data.skillPoints = Mathf.Max(0, _data.skillPoints) + Mathf.Max(0, saved.paidCost);
                progress.unlockedNodes.RemoveAt(i);
                changed = true;
            }

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

    int SumAllPaidCost()
    {
        int total = 0;
        for (int i = 0; i < _data.activeSkillTrees.Count; i++)
            total += SumPaidCost(_data.activeSkillTrees[i]);
        return total;
    }

    void EnsureCollections()
    {
        _data.activeSkillTrees ??= new List<CharacterSkillTreeProgressSaveData>();
        _data.selectedSkillOptions ??= new List<CharacterSkillSelectionSaveData>();
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
