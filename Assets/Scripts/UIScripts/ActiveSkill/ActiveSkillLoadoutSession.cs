using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Backing model for the Skill screen.
///
/// The screen addresses slots and variants by descriptor index. Which serialized list a slot came
/// from - Stryker command slots, the Helper's manual command, or a Helper proc slot - is resolved
/// once here through <see cref="SkillLoadoutDescriptorFactory"/> and never leaks into the views.
/// </summary>
public sealed class ActiveSkillLoadoutSession : IDisposable
{
    static readonly List<SkillLoadoutSlotDescriptor> EmptySlots = new();

    readonly CharacteContext _runtimeContext;
    readonly CharacterSkillManager _skillManager;
    readonly CharacterActiveSkillProgress _runtimeProgress;
    readonly string _characterId;

    List<SkillLoadoutSlotDescriptor> _slots = EmptySlots;
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

        _slots = SkillLoadoutDescriptorFactory.Build(Stats);
    }

    public CharacterStats Stats { get; }
    public CharacteContext RuntimeContext => _runtimeContext;
    public bool IsRuntime => _runtimeContext != null;

    /// <summary>Slot tabs, in authored order. Helper sessions list the manual command slot first.</summary>
    public IReadOnlyList<SkillLoadoutSlotDescriptor> Slots => _slots;

    public bool IsHelperLoadout => Stats != null && Stats.IsHelperRole;

    /// <summary>Header for the screen. Helpers never cast from command slots, so they get their own name.</summary>
    public string ScreenTitle => IsHelperLoadout ? "Helper Skills" : "Active Skills";

    /// <summary>Message for a character with no usable slots in its own half of the loadout.</summary>
    public string EmptyLoadoutMessage => IsHelperLoadout
        ? "No Helper skills configured."
        : "No skill slots configured.";

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

    public bool TryGetSlot(int slotIndex, out SkillLoadoutSlotDescriptor slot)
    {
        slot = null;
        if (_slots == null || slotIndex < 0 || slotIndex >= _slots.Count)
            return false;

        slot = _slots[slotIndex];
        return slot != null;
    }

    public bool TryGetOption(int slotIndex, int optionIndex, out SkillLoadoutOptionDescriptor option)
    {
        option = null;
        return TryGetSlot(slotIndex, out SkillLoadoutSlotDescriptor slot) &&
               slot.TryGetOption(optionIndex, out option);
    }

    public int GetSelectedOptionIndex(int slotIndex)
    {
        if (!TryGetSlot(slotIndex, out SkillLoadoutSlotDescriptor slot) || slot.Options.Count == 0)
            return -1;

        if (_skillManager != null &&
            _skillManager.TryGetSelectedLoadoutOptionId(slot.SlotId, out string runtimeOptionId) &&
            slot.TryGetOptionById(runtimeOptionId, out int runtimeIndex))
        {
            return runtimeIndex;
        }

        CharacterProgressData progress = GetProgressData();
        string savedOptionId = CharacterSkillSelectionStore.FindOptionId(progress, slot.SlotId);
        if (!string.IsNullOrWhiteSpace(savedOptionId) && slot.TryGetOptionById(savedOptionId, out int savedIndex))
            return savedIndex;

        return Mathf.Clamp(slot.DefaultOptionIndex, 0, slot.Options.Count - 1);
    }

    public bool SelectOption(int slotIndex, int optionIndex)
    {
        if (!TryGetSlot(slotIndex, out SkillLoadoutSlotDescriptor slot) ||
            !slot.TryGetOption(optionIndex, out SkillLoadoutOptionDescriptor option))
        {
            return false;
        }

        if (_skillManager != null && _skillManager.TrySelectLoadoutOption(slot.SlotId, option.OptionId, true))
        {
            Changed?.Invoke();
            return true;
        }

        CharacterProgressData progress = GetProgressData();
        CharacterSkillSelectionStore.SetOption(progress, slot.SlotId, option.OptionId);
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
            reason = "No Skill Tree assigned.";
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
            reason = "No Skill Tree assigned.";
            return false;
        }

        bool result;
        if (_runtimeProgress != null)
            result = _runtimeProgress.TryUnlock(slotId, optionId, tree, nodeId, out reason);
        else
        {
            // CanUnlock can reconcile stale progress (refund a removed node, merge duplicate
            // entries) before rejecting the unlock. Persist that even on failure, or the
            // refunded points shown in memory vanish next time this state is loaded.
            result = _model.TryUnlock(slotId, optionId, tree, nodeId, out reason, out bool changed);
            if (changed)
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
            result = _model.ResetTree(slotId, optionId, tree, out refundedPoints, out bool changed);
            if (changed)
                SaveLobbyState();
        }

        if (result && _runtimeProgress == null)
            ProgressChanged?.Invoke();
        return result;
    }

    public FinalSkillStats BuildStatsPreview(int slotIndex, int optionIndex, SkillUpgradeNodeData proposedNode = null)
    {
        if (!TryGetOption(slotIndex, optionIndex, out SkillLoadoutOptionDescriptor option) || option.SkillAsset == null)
            return null;

        var instance = new SkillInstance
        {
            def = option.SkillAsset,
            upgradeSnapshot = BuildSnapshot(slotIndex, optionIndex, proposedNode),
        };

        return instance.GetFinalStats(null);
    }

    /// <summary>
    /// Summon-side preview for the node detail panel. These values are not part of
    /// <see cref="FinalSkillStats"/> — the summon payload resolves them at cast time from the
    /// upgrade snapshot — so they are recomputed here with the same formulas.
    /// </summary>
    public SkillSummonPreview BuildSummonPreview(int slotIndex, int optionIndex, SkillUpgradeNodeData proposedNode = null)
    {
        if (!TryGetOption(slotIndex, optionIndex, out SkillLoadoutOptionDescriptor option) ||
            option.SkillAsset == null ||
            !option.SkillAsset.TryFindPayload(out SummonSkillPayloadDef summon))
        {
            return default;
        }

        SkillUpgradeStatSnapshot snapshot = BuildSnapshot(slotIndex, optionIndex, proposedNode);

        int cap = summon.PerSkillCap;
        if (snapshot.TryGetAggregate(StatType.SummonCap, out float capAdd, out float capMul))
            cap = Mathf.FloorToInt((cap + capAdd) * capMul + 0.5f);
        cap = Mathf.Max(1, cap);

        if (!summon.OverrideMaxHealth)
            return new SkillSummonPreview(true, false, 0f, cap);

        // Owner max HP is only knowable at runtime, so the lobby shows the cap alone rather than
        // a health number that would be wrong once the character is in a run.
        float ownerMaxHealth = ResolveOwnerMaxHealth();
        if (ownerMaxHealth <= 0f)
            return new SkillSummonPreview(true, false, 0f, cap);

        float health = summon.BaseMaxHealth + ownerMaxHealth * summon.OwnerMaxHealthShare;
        if (snapshot.TryGetAggregate(StatType.SummonMaxHP, out float hpAdd, out float hpMul))
            health = (health + hpAdd) * hpMul;

        return new SkillSummonPreview(true, true, Mathf.Max(1f, health), cap);
    }

    SkillUpgradeStatSnapshot BuildSnapshot(int slotIndex, int optionIndex, SkillUpgradeNodeData proposedNode)
    {
        TryResolveTree(slotIndex, optionIndex, out string slotId, out string optionId, out SkillUpgradeTreeDefinition tree);
        SkillUpgradeStatSnapshot snapshot = tree != null
            ? _model.BuildSnapshot(slotId, optionId, tree, out _)
            : new SkillUpgradeStatSnapshot();

        if (proposedNode != null)
            snapshot.AddNode(proposedNode);

        return snapshot;
    }

    float ResolveOwnerMaxHealth()
    {
        if (_runtimeContext == null)
            return 0f;

        _runtimeContext.ResolveReferences();
        return _runtimeContext.StatsHub != null
            ? Mathf.Max(0f, _runtimeContext.StatsHub.GetMaximumHealth())
            : 0f;
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

        if (!TryGetSlot(slotIndex, out SkillLoadoutSlotDescriptor slot) ||
            !slot.TryGetOption(optionIndex, out SkillLoadoutOptionDescriptor option))
        {
            return false;
        }

        slotId = slot.SlotId;
        optionId = option.OptionId;
        tree = option.UpgradeTree;
        return tree != null;
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
}
