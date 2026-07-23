using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-150)]
public sealed class CharacterActiveSkillProgress : MonoBehaviour
{
    [SerializeField] CharacteContext ctx;
    [SerializeField] LevelSystem levelSystem;

    ActiveSkillProgressModel _model;
    bool _subscribed;

    public ActiveSkillProgressModel Model => _model;
    public int AvailablePoints => _model != null ? _model.AvailablePoints : 0;

    public event Action Changed;
    public event Action<int> PointsChanged;
    public event Action<string, string> TreeChanged;

    void Awake()
    {
        ResolveReferences();
        LoadState();
    }

    void Start()
    {
        ResolveReferences();
        Subscribe();
    }

    void OnEnable()
    {
        Subscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    public bool CanUnlock(
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree,
        string nodeId,
        out string reason)
    {
        EnsureModel();
        bool result = _model.CanUnlock(slotId, optionId, tree, nodeId, out reason, out bool changed);
        if (changed)
            SaveAndNotify();
        return result;
    }

    public bool TryUnlock(
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree,
        string nodeId,
        out string reason)
    {
        EnsureModel();
        if (!_model.TryUnlock(slotId, optionId, tree, nodeId, out reason))
            return false;

        SaveState();
        NotifyChanged(slotId, optionId);
        return true;
    }

    public bool ResetTree(
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree,
        out int refundedPoints)
    {
        EnsureModel();
        if (!_model.ResetTree(slotId, optionId, tree, out refundedPoints))
            return false;

        SaveState();
        NotifyChanged(slotId, optionId);
        return true;
    }

    public SkillUpgradeStatSnapshot BuildSnapshot(
        string slotId,
        string optionId,
        SkillUpgradeTreeDefinition tree)
    {
        EnsureModel();
        SkillUpgradeStatSnapshot snapshot = _model.BuildSnapshot(slotId, optionId, tree, out bool changed);
        if (changed)
            SaveAndNotify();
        return snapshot;
    }

    public void GrantPoints(int amount)
    {
        EnsureModel();
        if (!_model.GrantPoints(amount))
            return;

        SaveAndNotify();
    }

    public void ReloadFromSave()
    {
        LoadState();
        NotifyChanged(null, null);
    }

    void ResolveReferences()
    {
        if (!ctx)
        {
            TryGetComponent(out ctx);
            if (!ctx)
                ctx = GetComponentInParent<CharacteContext>();
        }

        if (!levelSystem && ctx != null)
            levelSystem = ctx.levelSystem;
        if (!levelSystem)
            TryGetComponent(out levelSystem);
        if (!levelSystem && ctx != null)
            levelSystem = ctx.GetComponentInChildren<LevelSystem>(true);

        if (ctx != null && ctx.ActiveSkillProgress != this)
            ctx.ActiveSkillProgress = this;
    }

    void EnsureModel()
    {
        if (_model == null)
            LoadState();
    }

    void LoadState()
    {
        ResolveReferences();
        string characterId = ResolveCharacterId();
        CharacterProgressData data = SaveManager.Instance != null && !string.IsNullOrWhiteSpace(characterId)
            ? SaveManager.Instance.LoadCharacterProgressData(characterId)
            : new CharacterProgressData();

        // LevelSystem restores its save in Start, while this component initializes in Awake.
        // Use whichever source is farther ahead so old saves receive their catch-up points.
        int level = Mathf.Max(data.level, levelSystem != null ? levelSystem.Level : 1);
        _model = new ActiveSkillProgressModel(ctx != null ? ctx.baseStats : null, data, level);
        if (_model.EnsureInitialized())
            PersistState();
    }

    void SaveState()
    {
        if (_model == null)
            return;

        PersistState();
    }

    void PersistState()
    {
        string characterId = ResolveCharacterId();
        if (SaveManager.Instance == null || string.IsNullOrWhiteSpace(characterId) || _model == null)
            return;

        // Passive progress and loadout selection can be owned by other live modules.
        // Merge only this module's fields into a fresh repository snapshot.
        CharacterProgressData latest = SaveManager.Instance.LoadCharacterProgressData(characterId);
        latest.activeSkillPoints = Mathf.Max(0, _model.Data.activeSkillPoints);
        latest.activeSkillProgressInitialized = _model.Data.activeSkillProgressInitialized;
        latest.activeSkillTrees = CloneTrees(_model.Data.activeSkillTrees);
        SaveManager.Instance.SaveCharacterProgressData(characterId, latest);
    }

    static List<CharacterSkillTreeProgressSaveData> CloneTrees(
        List<CharacterSkillTreeProgressSaveData> source)
    {
        var clone = new List<CharacterSkillTreeProgressSaveData>();
        if (source == null)
            return clone;

        for (int i = 0; i < source.Count; i++)
        {
            CharacterSkillTreeProgressSaveData entry = source[i];
            if (entry != null)
                clone.Add(entry.DeepClone());
        }

        return clone;
    }

    void Subscribe()
    {
        if (_subscribed || levelSystem == null)
            return;

        levelSystem.LeveledUp += HandleLeveledUp;
        levelSystem.XpChanged += HandleXpChanged;
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed || levelSystem == null)
            return;

        levelSystem.LeveledUp -= HandleLeveledUp;
        levelSystem.XpChanged -= HandleXpChanged;
        _subscribed = false;
    }

    void HandleLeveledUp(int oldLevel, int newLevel)
    {
        EnsureModel();
        int gainedLevels = Mathf.Max(0, newLevel - oldLevel);
        int pointsPerLevel = ctx != null && ctx.baseStats != null
            ? Mathf.Max(0, ctx.baseStats.activeSkillPointsPerLevel)
            : 1;

        _model.SetCharacterLevel(newLevel);
        _model.GrantPoints(gainedLevels * pointsPerLevel);
        SaveAndNotify();
    }

    void HandleXpChanged(int level, int currentXp, int xpToNext)
    {
        if (_model == null)
            return;

        _model.SetCharacterLevel(level);
        _model.Data.xp = Mathf.Max(0, currentXp);
    }

    void SaveAndNotify()
    {
        SaveState();
        NotifyChanged(null, null);
    }

    void NotifyChanged(string slotId, string optionId)
    {
        PointsChanged?.Invoke(AvailablePoints);
        if (!string.IsNullOrWhiteSpace(slotId) && !string.IsNullOrWhiteSpace(optionId))
            TreeChanged?.Invoke(slotId, optionId);
        Changed?.Invoke();
    }

    string ResolveCharacterId()
    {
        return ctx != null && ctx.baseStats != null && !string.IsNullOrWhiteSpace(ctx.baseStats.characterId)
            ? ctx.baseStats.characterId.Trim()
            : string.Empty;
    }
}
