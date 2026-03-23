using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(10)]
public sealed class PlayerPassiveProgress : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private LevelSystem levelSystem;
    [SerializeField] private PassiveController passiveController;

    [Header("Config")]
    [SerializeField] private PassiveTreeDefinition passiveTree;
    [SerializeField, Min(0)] private int pointsPerLevel = 1;
    [SerializeField] private bool grantCatchUpPointsFromCurrentLevel = true;

    [Header("Runtime")]
    [SerializeField, Min(0)] private int availablePoints;
    [SerializeField] private List<string> unlockedNodeIds = new();

    readonly List<PassiveDefinition> _unlockedPassives = new();

    CharacterProgressData _progressData;
    bool _isLoaded;
    bool _subscribed;

    public PassiveTreeDefinition PassiveTree => passiveTree;
    public int AvailablePoints => availablePoints;
    public IReadOnlyList<string> UnlockedNodeIds => unlockedNodeIds;

    public event Action Changed;
    public event Action<int> PointsChanged;
    public event Action<string> NodeUnlocked;

    void Awake()
    {
        if (!ctx) TryGetComponent(out ctx);
        if (!levelSystem) TryGetComponent(out levelSystem);
        if (!passiveController) TryGetComponent(out passiveController);

        if (ctx != null)
            ctx.PassiveProgress = this;
    }

    void Start()
    {
        LoadState();
        ApplyUnlockedPassives();
        SubscribeToLevelEvents();
        NotifyChanged();
    }

    void OnEnable()
    {
        if (!_isLoaded)
            return;

        SubscribeToLevelEvents();
        ApplyUnlockedPassives();
        NotifyChanged();
    }

    void OnDisable()
    {
        UnsubscribeFromLevelEvents();
    }

    public void SetPassiveTree(PassiveTreeDefinition newTree, bool applyImmediately = true)
    {
        passiveTree = newTree;

        if (!applyImmediately || !_isLoaded)
            return;

        ApplyUnlockedPassives();
        NotifyChanged();
    }

    public bool IsUnlocked(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return false;

        string resolvedId = nodeId.Trim();
        for (int i = 0; i < unlockedNodeIds.Count; i++)
        {
            if (string.Equals(unlockedNodeIds[i], resolvedId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public bool CanUnlockNode(string nodeId, out string reason)
    {
        reason = string.Empty;

        if (passiveTree == null)
        {
            reason = "Missing passive tree.";
            return false;
        }

        if (!passiveTree.TryGetNode(nodeId, out var node))
        {
            reason = "Node not found.";
            return false;
        }

        if (IsUnlocked(node.RuntimeNodeId))
        {
            reason = "Already unlocked.";
            return false;
        }

        int cost = Mathf.Max(1, node.cost);
        if (availablePoints < cost)
        {
            reason = $"Need {cost} passive points.";
            return false;
        }

        int requiredLevel = Mathf.Max(1, node.requiredLevel);
        if (levelSystem != null && levelSystem.Level < requiredLevel)
        {
            reason = $"Requires level {requiredLevel}.";
            return false;
        }

        if (node.requiredNodeIds != null)
        {
            for (int i = 0; i < node.requiredNodeIds.Count; i++)
            {
                var requiredNodeId = node.requiredNodeIds[i];
                if (string.IsNullOrWhiteSpace(requiredNodeId))
                    continue;

                if (IsUnlocked(requiredNodeId))
                    continue;

                reason = $"Requires node {requiredNodeId}.";
                return false;
            }
        }

        return true;
    }

    public bool TryUnlockNode(string nodeId)
    {
        if (!CanUnlockNode(nodeId, out _))
            return false;

        if (!passiveTree.TryGetNode(nodeId, out var node))
            return false;

        int cost = Mathf.Max(1, node.cost);
        availablePoints = Mathf.Max(0, availablePoints - cost);
        unlockedNodeIds.Add(node.RuntimeNodeId);

        SaveState();
        ApplyUnlockedPassives();

        NodeUnlocked?.Invoke(node.RuntimeNodeId);
        NotifyChanged();
        return true;
    }

    public void GrantPoints(int amount, bool save = true)
    {
        if (amount <= 0)
            return;

        availablePoints += amount;

        if (save)
            SaveState();

        NotifyChanged();
    }

    public void ReloadFromSave()
    {
        LoadState();
        ApplyUnlockedPassives();
        NotifyChanged();
    }

    void LoadState()
    {
        string characterId = ResolveCharacterId();
        _progressData = SaveManager.Instance != null && !string.IsNullOrWhiteSpace(characterId)
            ? SaveManager.Instance.LoadCharacterProgressData(characterId)
            : new CharacterProgressData();

        if (_progressData == null)
            _progressData = new CharacterProgressData();

        if (_progressData.unlockedPassiveNodeIds == null)
            _progressData.unlockedPassiveNodeIds = new List<string>();

        if (!_progressData.passiveProgressInitialized)
        {
            _progressData.passiveProgressInitialized = true;

            if (grantCatchUpPointsFromCurrentLevel && levelSystem != null)
            {
                int catchUpPoints = Mathf.Max(0, levelSystem.Level - 1) * Mathf.Max(0, pointsPerLevel);
                _progressData.skillPoints = Mathf.Max(_progressData.skillPoints, catchUpPoints);
            }

            SaveProgressData(characterId);
        }

        availablePoints = Mathf.Max(0, _progressData.skillPoints);
        unlockedNodeIds.Clear();

        for (int i = 0; i < _progressData.unlockedPassiveNodeIds.Count; i++)
        {
            var nodeId = _progressData.unlockedPassiveNodeIds[i];
            if (string.IsNullOrWhiteSpace(nodeId) || IsUnlocked(nodeId))
                continue;

            unlockedNodeIds.Add(nodeId.Trim());
        }

        _isLoaded = true;
    }

    void SaveState()
    {
        if (_progressData == null)
            _progressData = new CharacterProgressData();

        _progressData.level = levelSystem != null ? levelSystem.Level : _progressData.level;
        _progressData.xp = levelSystem != null ? levelSystem.CurrentXp : _progressData.xp;
        _progressData.skillPoints = Mathf.Max(0, availablePoints);
        _progressData.passiveProgressInitialized = true;

        if (_progressData.unlockedPassiveNodeIds == null)
            _progressData.unlockedPassiveNodeIds = new List<string>();

        _progressData.unlockedPassiveNodeIds.Clear();
        for (int i = 0; i < unlockedNodeIds.Count; i++)
            _progressData.unlockedPassiveNodeIds.Add(unlockedNodeIds[i]);

        SaveProgressData(ResolveCharacterId());
    }

    void SaveProgressData(string characterId)
    {
        if (SaveManager.Instance == null || string.IsNullOrWhiteSpace(characterId))
            return;

        SaveManager.Instance.SaveCharacterProgressData(characterId, _progressData);
    }

    void ApplyUnlockedPassives()
    {
        _unlockedPassives.Clear();

        if (passiveTree != null)
        {
            for (int i = 0; i < unlockedNodeIds.Count; i++)
            {
                if (!passiveTree.TryGetNode(unlockedNodeIds[i], out var node) || node == null || node.passive == null)
                    continue;

                _unlockedPassives.Add(node.passive);
            }
        }

        passiveController?.SetRuntimePassives(_unlockedPassives);
    }

    void SubscribeToLevelEvents()
    {
        if (_subscribed || levelSystem == null)
            return;

        levelSystem.LeveledUp += HandleLeveledUp;
        _subscribed = true;
    }

    void UnsubscribeFromLevelEvents()
    {
        if (!_subscribed || levelSystem == null)
            return;

        levelSystem.LeveledUp -= HandleLeveledUp;
        _subscribed = false;
    }

    void HandleLeveledUp(int oldLevel, int newLevel)
    {
        int gainedLevels = Mathf.Max(0, newLevel - oldLevel);
        int grantedPoints = gainedLevels * Mathf.Max(0, pointsPerLevel);
        GrantPoints(grantedPoints);
    }

    void NotifyChanged()
    {
        PointsChanged?.Invoke(availablePoints);
        Changed?.Invoke();
    }

    string ResolveCharacterId()
    {
        if (ctx != null && ctx.baseStats != null && !string.IsNullOrWhiteSpace(ctx.baseStats.characterId))
            return ctx.baseStats.characterId;

        return string.Empty;
    }
}
