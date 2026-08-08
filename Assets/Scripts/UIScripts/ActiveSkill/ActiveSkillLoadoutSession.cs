using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ActiveSkillLoadoutSession : IDisposable
{
    readonly CharacteContext _runtimeContext;
    readonly CharacterSkillManager _skillManager;
    readonly CharacterActiveSkillProgress _runtimeProgress;
    readonly string _characterId;

    CharacterProgressData _data;
    ActiveSkillProgressModel _model;

    ActiveSkillLoadoutSession(CharacteContext runtimeContext, CharacterStats stats)
    {
        _runtimeContext = runtimeContext;
        Stats = stats;
        _characterId = stats != null && !string.IsNullOrWhiteSpace(stats.characterId)
            ? stats.characterId.Trim()
            : string.Empty;

        if (_runtimeContext != null)
        {
            _runtimeContext.ResolveReferences();
            _skillManager = _runtimeContext.SkillManager;
            _runtimeProgress = _runtimeContext.ActiveSkillProgress;
            _model = _runtimeProgress != null ? _runtimeProgress.Model : null;
            if (_runtimeProgress != null)
                _runtimeProgress.Changed += HandleRuntimeChanged;
        }

        if (_model == null)
        {
            _data = SaveManager.Instance != null && !string.IsNullOrWhiteSpace(_characterId)
                ? SaveManager.Instance.LoadCharacterProgressData(_characterId)
                : new CharacterProgressData();
            _model = new ActiveSkillProgressModel(Stats, _data, Mathf.Max(1, _data.level));
            if (_model.EnsureInitialized())
                SaveLobbyState();
        }
    }

    public CharacterStats Stats { get; }
    public CharacteContext RuntimeContext => _runtimeContext;
    public bool IsRuntime => _runtimeContext != null;
    public int AvailablePoints => _runtimeProgress != null
        ? _runtimeProgress.AvailablePoints
        : _model != null ? _model.AvailablePoints : 0;

    public event Action Changed;
    public event Action ProgressChanged;

    public static ActiveSkillLoadoutSession CreateRuntime(CharacteContext context)
    {
        if (context == null)
            return null;

        return new ActiveSkillLoadoutSession(context, context.baseStats);
    }

    public static ActiveSkillLoadoutSession CreateLobby(CharacterStats stats)
    {
        return stats != null ? new ActiveSkillLoadoutSession(null, stats) : null;
    }

    public int GetSelectedOptionIndex(int slotIndex)
    {
        if (!TryGetSlot(slotIndex, out CharacterSkillLoadoutSlot slot))
            return -1;

        if (_skillManager != null && _skillManager.TryGetSelectedSkillOption(slotIndex, out CharacterSkillLoadoutOption selected))
        {
            for (int i = 0; i < slot.Options.Count; i++)
            {
                if (ReferenceEquals(slot.Options[i], selected))
                    return i;
            }
        }

        CharacterProgressData progress = GetProgressData();
        string savedOptionId = CharacterSkillSelectionStore.FindOptionId(progress, ResolveSlotId(slot, slotIndex));
        if (!string.IsNullOrWhiteSpace(savedOptionId) && slot.TryGetOptionById(savedOptionId, out int savedIndex, out _))
            return savedIndex;

        return slot.TryGetDefaultOption(out int defaultIndex, out _) ? defaultIndex : -1;
    }

    public bool SelectOption(int slotIndex, int optionIndex)
    {
        if (!TryGetSlot(slotIndex, out CharacterSkillLoadoutSlot slot) ||
            !slot.TryGetOption(optionIndex, out CharacterSkillLoadoutOption option))
        {
            return false;
        }

        if (_skillManager != null && _skillManager.TrySelectSkillOption(slotIndex, optionIndex, true))
        {
            Changed?.Invoke();
            return true;
        }

        CharacterProgressData progress = GetProgressData();
        CharacterSkillSelectionStore.SetOption(
            progress,
            ResolveSlotId(slot, slotIndex),
            ResolveOptionId(option, optionIndex));
        SaveLobbyState();
        Changed?.Invoke();
        return true;
    }

    public bool IsUnlocked(int slotIndex, int optionIndex, string nodeId)
    {
        if (!TryResolveTree(slotIndex, optionIndex, out string slotId, out string optionId, out SkillUpgradeTreeDefinition tree))
            return false;

        bool unlocked = _model.IsUnlocked(slotId, optionId, tree, nodeId, out bool changed);
        if (changed)
            SaveLobbyState();
        return unlocked;
    }

    public bool CanUnlock(int slotIndex, int optionIndex, string nodeId, out string reason)
    {
        if (!TryResolveTree(slotIndex, optionIndex, out string slotId, out string optionId, out SkillUpgradeTreeDefinition tree))
        {
            reason = "Missing Active Skill Tree.";
            return false;
        }

        if (_runtimeProgress != null)
            return _runtimeProgress.CanUnlock(slotId, optionId, tree, nodeId, out reason);

        bool result = _model.CanUnlock(slotId, optionId, tree, nodeId, out reason, out bool changed);
        if (changed)
            SaveLobbyState();
        return result;
    }

    public bool TryUnlock(int slotIndex, int optionIndex, string nodeId, out string reason)
    {
        if (!TryResolveTree(slotIndex, optionIndex, out string slotId, out string optionId, out SkillUpgradeTreeDefinition tree))
        {
            reason = "Missing Active Skill Tree.";
            return false;
        }

        bool result;
        if (_runtimeProgress != null)
            result = _runtimeProgress.TryUnlock(slotId, optionId, tree, nodeId, out reason);
        else
        {
            result = _model.TryUnlock(slotId, optionId, tree, nodeId, out reason);
            if (result)
                SaveLobbyState();
        }

        if (result && _runtimeProgress == null)
            ProgressChanged?.Invoke();
        return result;
    }

    public bool ResetTree(int slotIndex, int optionIndex, out int refundedPoints)
    {
        if (!TryResolveTree(slotIndex, optionIndex, out string slotId, out string optionId, out SkillUpgradeTreeDefinition tree))
        {
            refundedPoints = 0;
            return false;
        }

        bool result;
        if (_runtimeProgress != null)
            result = _runtimeProgress.ResetTree(slotId, optionId, tree, out refundedPoints);
        else
        {
            result = _model.ResetTree(slotId, optionId, tree, out refundedPoints);
            if (result)
                SaveLobbyState();
        }

        if (result && _runtimeProgress == null)
            ProgressChanged?.Invoke();
        return result;
    }

    public FinalSkillStats BuildStatsPreview(int slotIndex, int optionIndex, SkillUpgradeNodeData proposedNode = null)
    {
        if (!TryGetOption(slotIndex, optionIndex, out CharacterSkillLoadoutOption option) || option.ActiveSkillAsset == null)
            return null;

        TryResolveTree(slotIndex, optionIndex, out string slotId, out string optionId, out SkillUpgradeTreeDefinition tree);
        SkillUpgradeStatSnapshot snapshot = tree != null
            ? _model.BuildSnapshot(slotId, optionId, tree, out _)
            : new SkillUpgradeStatSnapshot();

        if (proposedNode != null)
            snapshot.AddNode(proposedNode);

        var instance = new SkillInstance
        {
            def = option.ActiveSkillAsset,
            upgradeSnapshot = snapshot,
        };

        return instance.GetFinalStats(null);
    }

    public void Dispose()
    {
        if (_runtimeProgress != null)
            _runtimeProgress.Changed -= HandleRuntimeChanged;
    }

    bool TryResolveTree(
        int slotIndex,
        int optionIndex,
        out string slotId,
        out string optionId,
        out SkillUpgradeTreeDefinition tree)
    {
        slotId = string.Empty;
        optionId = string.Empty;
        tree = null;

        if (!TryGetSlot(slotIndex, out CharacterSkillLoadoutSlot slot) ||
            !slot.TryGetOption(optionIndex, out CharacterSkillLoadoutOption option))
        {
            return false;
        }

        slotId = ResolveSlotId(slot, slotIndex);
        optionId = ResolveOptionId(option, optionIndex);
        tree = option.ResolvedUpgradeTree;
        return tree != null;
    }

    public bool TryGetOption(int slotIndex, int optionIndex, out CharacterSkillLoadoutOption option)
    {
        option = null;
        return TryGetSlot(slotIndex, out CharacterSkillLoadoutSlot slot) &&
               slot.TryGetOption(optionIndex, out option);
    }

    bool TryGetSlot(int slotIndex, out CharacterSkillLoadoutSlot slot)
    {
        slot = null;
        if (Stats == null || Stats.skillSlots == null || slotIndex < 0 || slotIndex >= Stats.skillSlots.Count)
            return false;

        slot = Stats.skillSlots[slotIndex];
        return slot != null;
    }

    CharacterProgressData GetProgressData()
    {
        if (_runtimeProgress != null && _runtimeProgress.Model != null)
            return _runtimeProgress.Model.Data;

        return _data ?? _model.Data;
    }

    void SaveLobbyState()
    {
        if (_runtimeProgress != null || SaveManager.Instance == null || string.IsNullOrWhiteSpace(_characterId))
            return;

        SaveManager.Instance.SaveCharacterProgressData(_characterId, GetProgressData());
    }

    void HandleRuntimeChanged()
    {
        _model = _runtimeProgress.Model;
        ProgressChanged?.Invoke();
    }

    static string ResolveSlotId(CharacterSkillLoadoutSlot slot, int slotIndex)
    {
        return slot != null && !string.IsNullOrWhiteSpace(slot.ResolvedSlotId)
            ? slot.ResolvedSlotId
            : $"slot:{Mathf.Max(0, slotIndex)}";
    }

    static string ResolveOptionId(CharacterSkillLoadoutOption option, int optionIndex)
    {
        return option != null && !string.IsNullOrWhiteSpace(option.ResolvedOptionId)
            ? option.ResolvedOptionId
            : $"option:{Mathf.Max(0, optionIndex)}";
    }
}
