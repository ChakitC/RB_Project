using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns resolved slots, selected upgrades and cached runtime entries. The component supplies
/// character inputs and handles invalidation before a selected entry is replaced.
/// </summary>
internal sealed class CharacterSkillLoadoutRuntime
{
    static readonly CharacterSkillLoadoutOption[] EmptySkillOptions = Array.Empty<CharacterSkillLoadoutOption>();
    readonly SkillChargeRegistry chargeRegistry;
    readonly Func<List<CharacterSkillSelectionSaveData>> loadSelections;
    readonly Action<string, string> persistSelection;
    readonly Func<CharacterStats, bool> canChangeLoadout;
    CharacterStats baseStats;
    AITargetIdentity targetIdentity;
    SkillSlot[] autonomousSlots;
    CharacterActiveSkillProgress activeSkillProgress;

    public event Action<SkillSlot> SlotInvalidating;
    public event Action<SkillInstance> RuntimeSkillInvalidating;
    public event Action PassiveLoadoutChanged;
    public event Action HelperProcLoadoutChanged;

    public CharacterSkillLoadoutRuntime(SkillChargeRegistry chargeRegistry,
        Func<List<CharacterSkillSelectionSaveData>> loadSelections, Action<string, string> persistSelection,
        Func<CharacterStats, bool> canChangeLoadout)
    {
        this.chargeRegistry = chargeRegistry;
        this.loadSelections = loadSelections;
        this.persistSelection = persistSelection;
        this.canChangeLoadout = canChangeLoadout;
    }

    public void SetInputs(CharacterStats stats, AITargetIdentity identity, SkillSlot[] slots,
        CharacterActiveSkillProgress progress)
    {
        baseStats = stats;
        targetIdentity = identity;
        autonomousSlots = slots;
        activeSkillProgress = progress;
    }

    public IReadOnlyList<SkillSlot> CommandSlots => resolvedCommandSlots;
    public CharacterSkillEntry HelperCommandSkillEntry => helperCommandSkillEntry;
    public bool HasConfiguredPassiveSlots
    {
        get
        {
            RefreshResolvedCommandSlotsIfNeeded();
            return resolvedCommandSlotStates.Exists(state => state != null && state.IsPassive);
        }
    }

    public bool TryGetSelectedHelperProcEntry(SkillHelperDef helperProc, out CharacterSkillEntry entry)
    {
        entry = null;
        if (!TryGetSelectedHelperProcState(helperProc, out var state)) return false;
        entry = state.runtimeEntry ?? GetOrCreateHelperProcEntry(helperProc);
        return entry != null;
    }

    List<CharacterSkillSelectionSaveData> LoadSavedSkillSelections() => loadSelections();
    void PersistSkillSelection(string slotId, string optionId) => persistSelection(slotId, optionId);

    sealed class ResolvedCommandSlotState
    {
        public SkillSlot slot;
        public CharacterSkillLoadoutSlot statsSlot;
        public CharacterSkillLoadoutOption selectedOption;
        public int selectedOptionIndex = -1;
        public string slotId;
        public CharacterSkillSlotKind slotKind;
        public bool usesPrefabOverride;
        public SkillUpgradeStatSnapshot upgradeSnapshot;
        public bool IsPassive;
    }

    /// <summary>
    /// One resolved Helper proc slot: the variant this character currently has equipped, and the
    /// upgrade snapshot its execution skill must run with.
    /// </summary>
    sealed class ResolvedHelperProcState
    {
        public HelperProcLoadoutSlot statsSlot;
        public HelperProcLoadoutOption selectedOption;
        public int selectedOptionIndex = -1;
        public string slotId;
        public SkillUpgradeStatSnapshot upgradeSnapshot;
        public CharacterSkillEntry runtimeEntry;
    }

    private CharacterStats observedBaseStats;
    private int observedAutonomousSlotCount = -1;
    private bool commandSlotsBuilt;

    /// <summary>
    /// The manual command assist, rebuilt from <see cref="CharacterStats.helperCommandSlot"/>.
    ///
    /// Runtime-only on purpose: the helper actor is a shared rig, so a prefab-authored entry would
    /// belong to whoever is currently loaded into it instead of to the character that owns the
    /// skill.
    /// </summary>
    private readonly CharacterSkillEntry helperCommandSkillEntry = new();
    private readonly ResolvedCommandSlotState helperCommandState = new();
    private CharacterStats observedHelperLoadoutStats;
    private bool helperLoadoutResolved;

    readonly List<SkillSlot> resolvedCommandSlots = new();
    readonly List<ResolvedCommandSlotState> resolvedCommandSlotStates = new();
    readonly List<ResolvedHelperProcState> resolvedHelperProcStates = new();

    /// <summary>
    /// Legacy external-entry snapshots keyed by execution definition. The dedicated Helper proc
    /// path below keeps the selected variant snapshot on a proc-keyed runtime entry; this map is
    /// retained for other external skill callers and compatibility with the older entry builder.
    /// </summary>
    readonly Dictionary<SkillGemDefinition, SkillUpgradeStatSnapshot> helperExecutionSnapshots = new();

    /// <summary>
    /// Runtime entries are keyed by proc variant, not only by execution skill. Variants can then
    /// keep their own slot/option snapshot while still sharing the charge pool bound by the
    /// execution skill definition.
    /// </summary>
    readonly Dictionary<SkillHelperDef, CharacterSkillEntry> helperProcRuntimeEntries = new();
    readonly Dictionary<PartyFollowUpSkillDef, CharacterSkillEntry> partyFollowUpRuntimeEntries = new();

    /// <summary>
    /// Entries for skills driven from outside the serialized loadout (helper assists, scripted
    /// casts). Kept for the component's lifetime so each definition keeps one runtime instance.
    /// </summary>
    private readonly Dictionary<SkillGemDefinition, CharacterSkillEntry> externalSkillEntries =
        new Dictionary<SkillGemDefinition, CharacterSkillEntry>();


    /// <summary>
    /// Skill currently assigned to a command slot, for HUD icons and tooltips. This is a plain
    /// read of the resolved loadout: it does not build a runtime skill or touch the charge pool,
    /// so a HUD can poll it alongside <see cref="CharacterSkillManager.TryGetSlotChargeStatus"/> without paying twice.
    /// </summary>
    public bool TryGetSlotSkillDefinition(int slotIndex, out SkillGemDefinition skillDef)
    {
        skillDef = null;

        if (!TryResolveCastSlotIndex(slotIndex, out slotIndex))
            return false;

        if (!TryGetCommandSlot(slotIndex, out SkillSlot slot))
            return false;

        skillDef = slot.skillAsset;
        return skillDef != null;
    }

    /// <summary>
    /// Passive assigned to a command slot, for HUD icons. Returns false for an active slot, so a
    /// HUD can ask this first and fall back to the charge readout only when it comes back empty.
    /// A passive slot has no runtime skill and no charge pool, which is why it can never answer
    /// <see cref="CharacterSkillManager.TryGetSlotChargeStatus"/>.
    /// </summary>
    public bool TryGetSlotPassiveDefinition(int slotIndex, out PassiveDefinition passiveDef)
    {
        passiveDef = null;

        if (!TryResolveCastSlotIndex(slotIndex, out slotIndex))
            return false;

        if (!TryGetCommandSlotState(slotIndex, out ResolvedCommandSlotState state) || !state.IsPassive)
            return false;

        passiveDef = state.selectedOption != null ? state.selectedOption.PassiveAsset : null;
        return passiveDef != null;
    }

    public bool TrySelectSkillOption(int slotIndex, int optionIndex, bool persist = true)
    {

        if (!TryGetCommandSlotState(slotIndex, out ResolvedCommandSlotState state))
            return false;

        if (IsRunLoadoutLocked(state))
            return false;

        if (state.usesPrefabOverride || state.statsSlot == null)
            return false;

        if (!state.statsSlot.TryGetOption(optionIndex, out CharacterSkillLoadoutOption option))
            return false;

        if (!IsCurrentSelectedSkillOption(state, optionIndex, option))
            ApplySelectedSkillOption(state, optionIndex, option);

        if (persist)
            PersistSkillSelection(state.slotId, ResolveOptionId(option, optionIndex));

        return true;
    }

    public bool TrySelectSkillOption(string slotId, string optionId, bool persist = true)
    {

        if (!TryGetCommandSlotState(slotId, out ResolvedCommandSlotState state))
            return false;

        if (IsRunLoadoutLocked(state))
            return false;

        if (state.usesPrefabOverride || state.statsSlot == null)
            return false;

        if (!state.statsSlot.TryGetOptionById(optionId, out int optionIndex, out CharacterSkillLoadoutOption option))
            return false;

        if (!IsCurrentSelectedSkillOption(state, optionIndex, option))
            ApplySelectedSkillOption(state, optionIndex, option);

        if (persist)
            PersistSkillSelection(state.slotId, ResolveOptionId(option, optionIndex));

        return true;
    }

    public bool TryGetSkillOptions(int slotIndex, out IReadOnlyList<CharacterSkillLoadoutOption> options)
    {
        options = EmptySkillOptions;

        if (!TryGetCommandSlotState(slotIndex, out ResolvedCommandSlotState state))
            return false;

        if (state.usesPrefabOverride || state.statsSlot == null)
            return false;

        options = state.statsSlot.Options;
        return options.Count > 0;
    }

    public bool TryGetSkillOptions(string slotId, out IReadOnlyList<CharacterSkillLoadoutOption> options)
    {
        options = EmptySkillOptions;

        if (!TryGetCommandSlotState(slotId, out ResolvedCommandSlotState state))
            return false;

        if (state.usesPrefabOverride || state.statsSlot == null)
            return false;

        options = state.statsSlot.Options;
        return options.Count > 0;
    }

    public bool TryGetSelectedSkillOption(int slotIndex, out CharacterSkillLoadoutOption option)
    {
        option = null;

        if (!TryGetCommandSlotState(slotIndex, out ResolvedCommandSlotState state))
            return false;

        option = state.selectedOption;
        return option != null;
    }

    public bool TryGetSelectedSkillOption(string slotId, out CharacterSkillLoadoutOption option)
    {
        option = null;

        if (!TryGetCommandSlotState(slotId, out ResolvedCommandSlotState state))
            return false;

        option = state.selectedOption;
        return option != null;
    }

    /// <summary>
    /// Selects a variant in any slot this character owns, Stryker or Helper.
    ///
    /// Slot ids are namespaced by <see cref="CharacterSkillLoadoutKeys"/>, so one call resolves
    /// the right half of the loadout without the caller knowing which role it is looking at.
    /// </summary>
    public bool TrySelectLoadoutOption(string slotId, string optionId, bool persist = true)
    {

        if (TryGetCommandSlotState(slotId, out _))
            return TrySelectSkillOption(slotId, optionId, persist);

        return TrySelectHelperOption(slotId, optionId, persist);
    }

    /// <summary>Option id currently equipped in <paramref name="slotId"/>, across both roles.</summary>
    public bool TryGetSelectedLoadoutOptionId(string slotId, out string optionId)
    {
        optionId = null;

        if (string.IsNullOrWhiteSpace(slotId))
            return false;

        if (TryGetCommandSlotState(slotId, out ResolvedCommandSlotState commandState) &&
            commandState.selectedOption != null)
        {
            optionId = ResolveOptionId(commandState.selectedOption, commandState.selectedOptionIndex);
            return true;
        }

        RefreshResolvedCommandSlotsIfNeeded();
        string resolvedSlotId = slotId.Trim();

        if (helperCommandState.statsSlot != null &&
            string.Equals(helperCommandState.slotId, resolvedSlotId, StringComparison.Ordinal) &&
            helperCommandState.selectedOption != null)
        {
            optionId = ResolveOptionId(helperCommandState.selectedOption, helperCommandState.selectedOptionIndex);
            return true;
        }

        if (!TryGetHelperProcState(resolvedSlotId, out ResolvedHelperProcState procState) ||
            procState.selectedOption == null)
        {
            return false;
        }

        optionId = CharacterSkillLoadoutKeys.OptionKey(procState.selectedOption, procState.selectedOptionIndex);
        return true;
    }

    bool TrySelectHelperOption(string slotId, string optionId, bool persist)
    {
        RefreshResolvedCommandSlotsIfNeeded();

        if (string.IsNullOrWhiteSpace(slotId) || string.IsNullOrWhiteSpace(optionId))
            return false;

        string resolvedSlotId = slotId.Trim();

        if (helperCommandState.statsSlot != null &&
            string.Equals(helperCommandState.slotId, resolvedSlotId, StringComparison.Ordinal))
        {
            if (!helperCommandState.statsSlot.TryGetOptionById(
                    optionId,
                    out int commandOptionIndex,
                    out CharacterSkillLoadoutOption commandOption))
            {
                return false;
            }

            if (commandOptionIndex != helperCommandState.selectedOptionIndex)
                ApplyHelperCommandOption(commandOption, commandOptionIndex);

            if (persist)
                PersistSkillSelection(resolvedSlotId, ResolveOptionId(commandOption, commandOptionIndex));

            HelperProcLoadoutChanged?.Invoke();
            return true;
        }

        if (!TryGetHelperProcState(resolvedSlotId, out ResolvedHelperProcState state))
            return false;

        if (!state.statsSlot.TryGetOptionById(optionId, out int optionIndex, out HelperProcLoadoutOption option))
            return false;

        if (optionIndex != state.selectedOptionIndex)
        {
            // The variant being unequipped may be mid-cast. Its charge pool is keyed by definition
            // and deliberately left alone, so switching back does not hand the player a free reset.
            SkillHelperDef previousProc = state.selectedOption?.helperProc;
            if (previousProc != null &&
                helperProcRuntimeEntries.TryGetValue(previousProc, out CharacterSkillEntry previousEntry))
            {
                RuntimeSkillInvalidating?.Invoke(previousEntry?.runtimeSkill);
            }

            ApplyHelperProcOption(state, option, optionIndex);
        }

        if (persist)
            PersistSkillSelection(resolvedSlotId, CharacterSkillLoadoutKeys.OptionKey(option, optionIndex));

        HelperProcLoadoutChanged?.Invoke();
        return true;
    }

    bool TryGetHelperProcState(string slotId, out ResolvedHelperProcState state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(slotId))
            return false;

        for (int i = 0; i < resolvedHelperProcStates.Count; i++)
        {
            ResolvedHelperProcState candidate = resolvedHelperProcStates[i];
            if (candidate?.statsSlot == null || candidate.slotId == null)
                continue;

            if (!string.Equals(candidate.slotId, slotId, StringComparison.Ordinal))
                continue;

            state = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetPartyFollowUpRuntimeEntry(
        PartyFollowUpSkillDef combo,
        out CharacterSkillEntry entry)
    {
        entry = null;
        if (combo == null || combo.executionSkill == null)
            return false;

        if (!partyFollowUpRuntimeEntries.TryGetValue(combo, out entry) || entry == null)
        {
            entry = new CharacterSkillEntry { skillAsset = combo.executionSkill };
            entry.runtimeSkill = CreateRuntimeSkill(combo.executionSkill, null);
            partyFollowUpRuntimeEntries[combo] = entry;
        }
        else if (entry.skillAsset != combo.executionSkill ||
                 entry.runtimeSkill == null ||
                 entry.runtimeSkill.def != combo.executionSkill)
        {
            entry.skillAsset = combo.executionSkill;
            entry.runtimeSkill = CreateRuntimeSkill(combo.executionSkill, null);
        }

        if (entry.runtimeSkill != null)
            entry.runtimeSkill.upgradeSnapshot = activeSkillProgress != null && combo.upgradeTree != null
                ? activeSkillProgress.BuildSnapshot(PartyFollowUpSkillDef.ProgressSlotId, combo.RuntimeId, combo.upgradeTree)
                : null;

        return entry.runtimeSkill != null;
    }

    /// <summary>Returns the live runtime entry for the currently selected proc variant.</summary>
    public bool TryGetHelperProcRuntimeSkill(SkillHelperDef helperProc, out SkillInstance runtimeSkill)
    {
        runtimeSkill = null;
        if (helperProc == null)
            return false;

        RefreshResolvedCommandSlotsIfNeeded();

        if (!TryGetSelectedHelperProcState(helperProc, out ResolvedHelperProcState state) ||
            state.runtimeEntry == null)
        {
            return false;
        }

        EnsureRuntimeSkill(state.runtimeEntry);
        runtimeSkill = state.runtimeEntry.runtimeSkill;
        return runtimeSkill != null && runtimeSkill.def == helperProc.executionSkill;
    }

    /// <summary>
    /// One entry per definition, kept for the lifetime of this component. The charge pool itself
    /// lives in the shared registry keyed by definition, so it would survive a rebuilt entry
    /// anyway - caching keeps allocation and cast diagnostics stable.
    /// </summary>
    public CharacterSkillEntry GetOrCreateExternalEntry(SkillGemDefinition skillDef)
    {
        if (skillDef == null)
            return null;

        if (externalSkillEntries.TryGetValue(skillDef, out CharacterSkillEntry entry) && entry != null)
            return entry;

        entry = new CharacterSkillEntry { skillAsset = skillDef };
        externalSkillEntries[skillDef] = entry;
        return entry;
    }

    private CharacterSkillEntry GetOrCreateHelperProcEntry(SkillHelperDef helperProc)
    {
        if (helperProc == null || helperProc.executionSkill == null)
            return null;

        if (helperProcRuntimeEntries.TryGetValue(helperProc, out CharacterSkillEntry entry) && entry != null)
            return entry;

        entry = new CharacterSkillEntry { skillAsset = helperProc.executionSkill };
        helperProcRuntimeEntries[helperProc] = entry;
        return entry;
    }

    bool TryGetSelectedHelperProcState(SkillHelperDef helperProc, out ResolvedHelperProcState state)
    {
        state = null;
        if (helperProc == null)
            return false;

        for (int i = 0; i < resolvedHelperProcStates.Count; i++)
        {
            ResolvedHelperProcState candidate = resolvedHelperProcStates[i];
            if (candidate?.selectedOption?.helperProc != helperProc)
                continue;

            state = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Helper procs this actor contributes: one selected variant per slot, and nothing else.
    ///
    /// Every party-slot rig and the helper actor itself are shared prefabs, so a prefab-authored
    /// proc would belong to whoever is currently loaded into that rig. Reading from
    /// <c>baseStats</c> - the same place command slots come from - keeps the proc tied to the
    /// character and drops it the moment that character leaves the party. Unselected variants are
    /// authored but not equipped, so they never reach the proc controller.
    /// </summary>
    public void AppendConfiguredHelperChainDefinitions(List<SkillHelperDef> buffer, HashSet<SkillHelperDef> dedupe = null)
    {
        if (buffer == null)
            return;

        RefreshResolvedCommandSlotsIfNeeded();

        // Only a character authored as a Helper contributes assists. A Stryker fights in the field
        // and casts from command slots; RefreshHelperLoadout leaves the proc list empty for it.
        for (int i = 0; i < resolvedHelperProcStates.Count; i++)
        {
            SkillHelperDef definition = resolvedHelperProcStates[i]?.selectedOption?.helperProc;
            if (definition == null)
                continue;

            if (dedupe != null && !dedupe.Add(definition))
                continue;

            buffer.Add(definition);
        }
    }

    public void AppendConfiguredPassiveDefinitions(List<EquippedPassive> buffer)
    {
        if (buffer == null)
            return;

        RefreshResolvedCommandSlotsIfNeeded();

        for (int i = 0; i < resolvedCommandSlotStates.Count; i++)
        {
            ResolvedCommandSlotState state = resolvedCommandSlotStates[i];
            if (state == null || !state.IsPassive)
                continue;

            PassiveDefinition definition = state.selectedOption != null ? state.selectedOption.PassiveAsset : null;
            if (definition != null)
                buffer.Add(new EquippedPassive(definition, state.upgradeSnapshot, state.slotId));
        }
    }

    public void ClearSlot(int index)
    {
        if (!TryResolveCastSlotIndex(index, out index) ||
            (TryGetCommandSlotState(index, out ResolvedCommandSlotState state) && IsRunLoadoutLocked(state)))
        {
            return;
        }

        if (!TryGetCommandSlot(index, out SkillSlot slot))
            return;

        SlotInvalidating?.Invoke(slot);

        ClearRuntimeSlot(slot);
        if (TryGetCommandSlotState(index, out ResolvedCommandSlotState updatedState))
        {
            updatedState.selectedOption = null;
            updatedState.selectedOptionIndex = -1;
        }
    }

    public void AssignSkillToSlot(int index, SkillGemDefinition asset)
    {
        if (!TryResolveCastSlotIndex(index, out index) ||
            (TryGetCommandSlotState(index, out ResolvedCommandSlotState state) && IsRunLoadoutLocked(state)))
        {
            return;
        }

        if (!TryGetCommandSlot(index, out SkillSlot slot))
            return;

        SlotInvalidating?.Invoke(slot);

        slot.skillAsset = asset;
        slot.runtimeSkill = BuildRuntimeSkill(slot, asset);
        if (TryGetCommandSlotState(index, out ResolvedCommandSlotState updatedState))
        {
            updatedState.selectedOption = null;
            updatedState.selectedOptionIndex = -1;
        }
    }

    private SkillInstance BuildRuntimeSkill(SkillSlot slot, SkillGemDefinition asset)
    {
        if (slot == null || asset == null)
            return null;

        return CreateRuntimeSkill(asset);
    }

    private SkillInstance BuildRuntimeSkill(CharacterSkillEntry entry, SkillGemDefinition asset)
    {
        if (entry == null || asset == null)
            return null;

        // A Helper proc's execution skill is cast as an external skill, so this is where the
        // selected variant's Skill Tree has to be attached; anything else casts unmodified.
        helperExecutionSnapshots.TryGetValue(asset, out SkillUpgradeStatSnapshot snapshot);
        return CreateRuntimeSkill(asset, snapshot);
    }

    private SkillInstance CreateRuntimeSkill(
        SkillGemDefinition asset,
        SkillUpgradeStatSnapshot upgradeSnapshot = null)
    {
        var instance = new SkillInstance
        {
            def = asset,
            upgradeSnapshot = upgradeSnapshot,
        };

        BindSharedCharges(instance);
        return instance;
    }

    /// <summary>
    /// Every runtime skill for the same definition shares one charge pool, so two loadout slots
    /// holding the same skill draw from â€” and display â€” the same charges.
    /// </summary>
    private void BindSharedCharges(SkillInstance instance)
    {
        if (instance == null || instance.def == null)
            return;

        instance.BindCharges(chargeRegistry.GetOrCreateCharges(instance.def));
    }

    public bool TryGetCommandSlot(int slotIndex, out SkillSlot slot)
    {
        slot = null;
        RefreshResolvedCommandSlotsIfNeeded();

        if (slotIndex < 0 || slotIndex >= resolvedCommandSlots.Count)
            return false;

        slot = resolvedCommandSlots[slotIndex];
        return slot != null;
    }

    /// <summary>
    /// Player, AI, party-command, and HUD callers retain their 0/1 input contract while migrated
    /// character assets resolve those inputs by slot meaning. A third cast input is unavailable.
    /// Legacy and non-character loadouts retain their authored indices.
    /// </summary>
    public bool TryResolveCastSlotIndex(int requestedIndex, out int resolvedIndex)
    {
        resolvedIndex = requestedIndex;
        RefreshResolvedCommandSlotsIfNeeded();

        bool hasActive = false;
        bool hasUltimate = false;
        int activeIndex = -1;
        int ultimateIndex = -1;

        for (int i = 0; i < resolvedCommandSlotStates.Count; i++)
        {
            CharacterSkillSlotKind kind = resolvedCommandSlotStates[i]?.slotKind ?? CharacterSkillSlotKind.Legacy;
            if (kind == CharacterSkillSlotKind.Active)
            {
                hasActive = true;
                activeIndex = i;
            }
            else if (kind == CharacterSkillSlotKind.Ultimate)
            {
                hasUltimate = true;
                ultimateIndex = i;
            }
        }

        bool playerOrAlly = baseStats != null && !baseStats.IsHelperRole &&
            (targetIdentity == AITargetIdentity.Player || targetIdentity == AITargetIdentity.Companion);
        if ((!hasActive || !hasUltimate) && !playerOrAlly)
            return requestedIndex >= 0 && requestedIndex < resolvedCommandSlots.Count;

        if (requestedIndex == 0)
        {
            resolvedIndex = activeIndex;
            return hasActive;
        }

        if (requestedIndex == 1)
        {
            resolvedIndex = ultimateIndex;
            return hasUltimate;
        }

        return false;
    }

    private bool IsRunLoadoutLocked(ResolvedCommandSlotState state)
    {
        if (state?.statsSlot == null || baseStats == null || baseStats.IsHelperRole)
            return false;

        if (targetIdentity != AITargetIdentity.Player && targetIdentity != AITargetIdentity.Companion)
            return false;

        return !canChangeLoadout(baseStats);
    }

    private bool TryGetCommandSlotState(int slotIndex, out ResolvedCommandSlotState state)
    {
        state = null;
        RefreshResolvedCommandSlotsIfNeeded();

        if (slotIndex < 0 || slotIndex >= resolvedCommandSlotStates.Count)
            return false;

        state = resolvedCommandSlotStates[slotIndex];
        return state != null && state.slot != null;
    }

    private bool TryGetCommandSlotState(string slotId, out ResolvedCommandSlotState state)
    {
        state = null;
        RefreshResolvedCommandSlotsIfNeeded();

        if (string.IsNullOrWhiteSpace(slotId))
            return false;

        string resolvedSlotId = slotId.Trim();
        for (int i = 0; i < resolvedCommandSlotStates.Count; i++)
        {
            ResolvedCommandSlotState candidate = resolvedCommandSlotStates[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.slotId))
                continue;

            if (!string.Equals(candidate.slotId, resolvedSlotId, StringComparison.Ordinal))
                continue;

            state = candidate;
            return true;
        }

        return false;
    }

    public void EnsureCommandRuntimeSkill(SkillSlot slot)
    {
        if (slot == null)
            return;

        if (slot.skillAsset == null)
        {
            slot.runtimeSkill = null;
            return;
        }

        if (slot.runtimeSkill != null && slot.runtimeSkill.def == slot.skillAsset)
        {
            // SkillSlot.runtimeSkill is Unity-serialized, so a prefab can hand us an instance that
            // never went through CreateRuntimeSkill and therefore has no shared pool yet.
            if (!slot.runtimeSkill.HasBoundCharges)
                BindSharedCharges(slot.runtimeSkill);
            return;
        }

        slot.runtimeSkill = BuildRuntimeSkill(slot, slot.skillAsset);
    }

    public void EnsureRuntimeSkill(CharacterSkillEntry entry)
    {
        if (entry == null)
            return;

        if (entry.skillAsset == null)
        {
            entry.runtimeSkill = null;
            return;
        }

        if (entry.runtimeSkill != null && entry.runtimeSkill.def == entry.skillAsset)
        {
            if (!entry.runtimeSkill.HasBoundCharges)
                BindSharedCharges(entry.runtimeSkill);
            return;
        }

        entry.runtimeSkill = BuildRuntimeSkill(entry, entry.skillAsset);
    }

    public void RefreshResolvedCommandSlotsIfNeeded()
    {
        RefreshHelperLoadout(force: false);

        CharacterStats currentBaseStats = baseStats;
        int currentAutonomousSlotCount = autonomousSlots != null ? autonomousSlots.Length : 0;

        if (commandSlotsBuilt &&
            observedBaseStats == currentBaseStats &&
            observedAutonomousSlotCount == currentAutonomousSlotCount)
        {
            return;
        }

        RebuildResolvedCommandSlots();
    }

    /// <summary>
    /// Rebuilds the Helper half of the loadout from the character currently loaded into this actor.
    ///
    /// A Stryker never owns one, and a Helper whose command slot has no configured option
    /// deliberately owns none - neither case falls back to anything. Each proc slot resolves the
    /// variant the player selected, so nothing that is merely authored but unequipped can fire.
    /// </summary>
    public void RefreshHelperLoadout(bool force)
    {
        CharacterStats stats = baseStats;

        if (!force && helperLoadoutResolved && observedHelperLoadoutStats == stats)
            return;

        observedHelperLoadoutStats = stats;
        helperLoadoutResolved = true;

        List<CharacterSkillSelectionSaveData> savedSelections = LoadSavedSkillSelections();
        helperExecutionSnapshots.Clear();

        RebuildHelperCommandSlot(stats, savedSelections);
        RebuildHelperProcSlots(stats, savedSelections);

        HelperProcLoadoutChanged?.Invoke();
    }

    private void RebuildHelperCommandSlot(
        CharacterStats stats,
        List<CharacterSkillSelectionSaveData> savedSelections)
    {
        CharacterSkillLoadoutSlot statsSlot = stats != null && stats.IsHelperRole
            ? stats.helperCommandSlot
            : null;

        helperCommandState.statsSlot = statsSlot;
        helperCommandState.slotId = statsSlot != null
            ? CharacterSkillLoadoutKeys.HelperCommandSlotKey(statsSlot)
            : null;
        helperCommandState.selectedOption = null;
        helperCommandState.selectedOptionIndex = -1;
        helperCommandState.upgradeSnapshot = null;

        CharacterSkillLoadoutOption option = null;
        int optionIndex = -1;
        if (statsSlot != null)
        {
            string savedOptionId = FindSavedOptionId(savedSelections, helperCommandState.slotId);
            if (string.IsNullOrWhiteSpace(savedOptionId) ||
                !statsSlot.TryGetOptionById(savedOptionId, out optionIndex, out option))
            {
                statsSlot.TryGetDefaultOption(out optionIndex, out option);
            }
        }

        ApplyHelperCommandOption(option, optionIndex);
    }

    private void ApplyHelperCommandOption(CharacterSkillLoadoutOption option, int optionIndex)
    {
        helperCommandState.selectedOption = option;
        helperCommandState.selectedOptionIndex = optionIndex;

        // A passive can be authored into a loadout slot, but a manual command has to be castable;
        // treating a passive as "the command" would leave the party command button doing nothing.
        SkillGemDefinition definition = option != null ? option.ActiveSkillAsset : null;

        SkillUpgradeStatSnapshot snapshot = null;
        if (definition != null)
        {
            SkillUpgradeTreeDefinition tree = option.ResolvedUpgradeTree;
            snapshot = activeSkillProgress != null && tree != null
                ? activeSkillProgress.BuildSnapshot(
                    helperCommandState.slotId,
                    CharacterSkillLoadoutKeys.OptionKey(option, optionIndex),
                    tree)
                : null;
        }

        helperCommandState.upgradeSnapshot = snapshot;

        SkillInstance previousRuntimeSkill = helperCommandSkillEntry.runtimeSkill;
        if (previousRuntimeSkill != null && previousRuntimeSkill.def != definition)
            RuntimeSkillInvalidating?.Invoke(previousRuntimeSkill);

        helperCommandSkillEntry.skillAsset = definition;
        helperCommandSkillEntry.runtimeSkill = definition != null
            ? CreateRuntimeSkill(definition, snapshot)
            : null;
    }

    private void RebuildHelperProcSlots(
        CharacterStats stats,
        List<CharacterSkillSelectionSaveData> savedSelections)
    {
        resolvedHelperProcStates.Clear();

        List<HelperProcLoadoutSlot> statsSlots = stats != null && stats.IsHelperRole
            ? stats.helperProcSlots
            : null;
        if (statsSlots == null)
            return;

        for (int i = 0; i < statsSlots.Count; i++)
        {
            HelperProcLoadoutSlot statsSlot = statsSlots[i];
            if (statsSlot == null)
                continue;

            var state = new ResolvedHelperProcState
            {
                statsSlot = statsSlot,
                slotId = CharacterSkillLoadoutKeys.HelperProcSlotKey(statsSlot, i),
            };

            string savedOptionId = FindSavedOptionId(savedSelections, state.slotId);
            if (string.IsNullOrWhiteSpace(savedOptionId) ||
                !statsSlot.TryGetOptionById(savedOptionId, out int optionIndex, out HelperProcLoadoutOption option))
            {
                statsSlot.TryGetDefaultOption(out optionIndex, out option);
            }

            ApplyHelperProcOption(state, option, optionIndex);
            resolvedHelperProcStates.Add(state);
        }
    }

    private void ApplyHelperProcOption(
        ResolvedHelperProcState state,
        HelperProcLoadoutOption option,
        int optionIndex)
    {
        if (state == null)
            return;

        state.selectedOption = option;
        state.selectedOptionIndex = optionIndex;
        state.upgradeSnapshot = null;
        state.runtimeEntry = null;

        SkillHelperDef helperProc = option != null ? option.helperProc : null;
        SkillGemDefinition execution = helperProc != null ? helperProc.executionSkill : null;
        if (execution == null)
            return;

        SkillUpgradeTreeDefinition tree = option.ResolvedUpgradeTree;
        if (activeSkillProgress != null && tree != null)
        {
            state.upgradeSnapshot = activeSkillProgress.BuildSnapshot(
                state.slotId,
                CharacterSkillLoadoutKeys.OptionKey(option, optionIndex),
                tree);
        }

        helperExecutionSnapshots[execution] = state.upgradeSnapshot;

        CharacterSkillEntry entry = GetOrCreateHelperProcEntry(helperProc);
        entry.skillAsset = execution;
        if (entry.runtimeSkill == null || entry.runtimeSkill.def != execution)
            entry.runtimeSkill = CreateRuntimeSkill(execution, state.upgradeSnapshot);
        else
            entry.runtimeSkill.upgradeSnapshot = state.upgradeSnapshot;

        state.runtimeEntry = entry;
        ApplyHelperExecutionSnapshot(execution, state.upgradeSnapshot);
    }

    /// <summary>
    /// Pushes a proc variant's snapshot onto the cached external entry for its execution skill.
    ///
    /// The entry is kept for the component's lifetime to preserve its runtime identity.
    /// Charge ownership remains in the registry when the snapshot changes.
    /// </summary>
    private void ApplyHelperExecutionSnapshot(
        SkillGemDefinition execution,
        SkillUpgradeStatSnapshot snapshot)
    {
        if (execution == null)
            return;

        if (externalSkillEntries.TryGetValue(execution, out CharacterSkillEntry entry) &&
            entry?.runtimeSkill != null)
        {
            entry.runtimeSkill.upgradeSnapshot = snapshot;
        }
    }

    public void RebuildResolvedCommandSlots()
    {
        observedBaseStats = baseStats;
        observedAutonomousSlotCount = autonomousSlots != null ? autonomousSlots.Length : 0;
        commandSlotsBuilt = true;

        resolvedCommandSlots.Clear();
        resolvedCommandSlotStates.Clear();

        List<CharacterSkillSelectionSaveData> savedSelections = LoadSavedSkillSelections();
        List<CharacterSkillLoadoutSlot> statsSlots = observedBaseStats != null ? observedBaseStats.skillSlots : null;
        int statsSlotCount = statsSlots != null ? statsSlots.Count : 0;
        int serializedSlotCount = autonomousSlots != null ? autonomousSlots.Length : 0;
        int slotCount = Mathf.Max(statsSlotCount, serializedSlotCount);

        for (int i = 0; i < slotCount; i++)
        {
            SkillSlot serializedSlot = GetSerializedAutonomousSlot(i);
            CharacterSkillLoadoutSlot statsSlot = i < statsSlotCount ? statsSlots[i] : null;

            if (statsSlot != null)
            {
                AddStatsLoadoutSlot(i, statsSlot, savedSelections);
                continue;
            }

            if (IsPrefabOverrideSlot(serializedSlot))
            {
                AddPrefabOverrideSlot(i, serializedSlot);
                continue;
            }

            AddPassthroughSerializedSlot(i, serializedSlot);
        }

        PassiveLoadoutChanged?.Invoke();
    }

    private void AddPrefabOverrideSlot(int slotIndex, SkillSlot slot)
    {
        SkillSlot resolvedSlot = slot ?? new SkillSlot();
        EnsureCommandRuntimeSkill(resolvedSlot);
        AddResolvedSlot(resolvedSlot, new ResolvedCommandSlotState
        {
            slot = resolvedSlot,
            slotId = $"prefab:{slotIndex}",
            usesPrefabOverride = true
        });
    }

    private void AddStatsLoadoutSlot(
        int slotIndex,
        CharacterSkillLoadoutSlot statsSlot,
        List<CharacterSkillSelectionSaveData> savedSelections)
    {
        var runtimeSlot = new SkillSlot
        {
            hotkey = statsSlot.hotkey
        };

        var state = new ResolvedCommandSlotState
        {
            slot = runtimeSlot,
            statsSlot = statsSlot,
            slotId = ResolveSlotId(statsSlot, slotIndex),
            slotKind = statsSlot.ResolvedSlotKind,
        };

        ApplySavedOrDefaultSelection(state, savedSelections);
        AddResolvedSlot(runtimeSlot, state);
    }

    private void AddPassthroughSerializedSlot(int slotIndex, SkillSlot slot)
    {
        SkillSlot resolvedSlot = slot ?? new SkillSlot();
        EnsureCommandRuntimeSkill(resolvedSlot);
        AddResolvedSlot(resolvedSlot, new ResolvedCommandSlotState
        {
            slot = resolvedSlot,
            slotId = $"slot:{slotIndex}"
        });
    }

    private void AddResolvedSlot(SkillSlot slot, ResolvedCommandSlotState state)
    {
        resolvedCommandSlots.Add(slot);
        resolvedCommandSlotStates.Add(state);
    }

    private SkillSlot GetSerializedAutonomousSlot(int slotIndex)
    {
        if (autonomousSlots == null || slotIndex < 0 || slotIndex >= autonomousSlots.Length)
            return null;

        return autonomousSlots[slotIndex];
    }

    private static bool IsPrefabOverrideSlot(SkillSlot slot)
    {
        return slot != null && slot.skillAsset != null;
    }

    private void ApplySavedOrDefaultSelection(
        ResolvedCommandSlotState state,
        List<CharacterSkillSelectionSaveData> savedSelections)
    {
        if (state == null || state.statsSlot == null || state.slot == null)
            return;

        string savedOptionId = FindSavedOptionId(savedSelections, state.slotId);
        if (!string.IsNullOrWhiteSpace(savedOptionId) &&
            state.statsSlot.TryGetOptionById(savedOptionId, out int savedOptionIndex, out CharacterSkillLoadoutOption savedOption))
        {
            ApplySelectedSkillOption(state, savedOptionIndex, savedOption, cancelPending: false);
            return;
        }

        if (state.statsSlot.TryGetDefaultOption(out int defaultOptionIndex, out CharacterSkillLoadoutOption defaultOption))
        {
            ApplySelectedSkillOption(state, defaultOptionIndex, defaultOption, cancelPending: false);
            return;
        }

        ClearRuntimeSlot(state.slot);
    }

    private void ApplySelectedSkillOption(
        ResolvedCommandSlotState state,
        int optionIndex,
        CharacterSkillLoadoutOption option,
        bool cancelPending = true)
    {
        if (state == null || state.slot == null)
            return;

        if (cancelPending)
            SlotInvalidating?.Invoke(state.slot);

        state.selectedOption = option;
        state.selectedOptionIndex = optionIndex;
        ApplySkillOptionToSlot(state, option, ResolveOptionId(option, optionIndex));

        if (state.IsPassive)
            PassiveLoadoutChanged?.Invoke();
    }

    private static bool IsCurrentSelectedSkillOption(
        ResolvedCommandSlotState state,
        int optionIndex,
        CharacterSkillLoadoutOption option)
    {
        if (state == null || state.slot == null || option == null)
            return false;

        if (state.selectedOptionIndex != optionIndex || !ReferenceEquals(state.selectedOption, option))
            return false;

        if (option.IsPassive)
            return state.IsPassive;

        SkillGemDefinition activeAsset = option.ActiveSkillAsset;
        return activeAsset != null &&
               state.slot.skillAsset == activeAsset &&
               state.slot.runtimeSkill != null &&
               state.slot.runtimeSkill.def == activeAsset;
    }

    private void ApplySkillOptionToSlot(
        ResolvedCommandSlotState state,
        CharacterSkillLoadoutOption option,
        string optionId)
    {
        SkillSlot slot = state != null ? state.slot : null;
        if (slot == null)
            return;

        CharacterSkillLoadoutSlot statsSlot = state.statsSlot;
        slot.hotkey = statsSlot != null ? statsSlot.hotkey : slot.hotkey;

        if (option == null || option.skillAsset == null)
        {
            ClearRuntimeSlot(slot);
            state.upgradeSnapshot = null;
            state.IsPassive = false;
            return;
        }

        SkillUpgradeTreeDefinition upgradeTree = option.ResolvedUpgradeTree;
        SkillUpgradeStatSnapshot upgradeSnapshot = activeSkillProgress != null && upgradeTree != null
            ? activeSkillProgress.BuildSnapshot(state.slotId, optionId, upgradeTree)
            : null;
        state.upgradeSnapshot = upgradeSnapshot;

        if (option.IsPassive)
        {
            ClearRuntimeSlot(slot);
            state.IsPassive = true;
            return;
        }

        state.IsPassive = false;
        slot.skillAsset = option.ActiveSkillAsset;
        slot.runtimeSkill = CreateRuntimeSkill(slot.skillAsset, upgradeSnapshot);
    }

    public void HandleActiveSkillTreeChanged(string slotId, string optionId)
    {
        if (string.IsNullOrWhiteSpace(slotId) || string.IsNullOrWhiteSpace(optionId))
        {
            RefreshHelperLoadout(force: true);
            RebuildResolvedCommandSlots();
            return;
        }

        if (HandleHelperTreeChanged(slotId, optionId))
            return;

        if (!TryGetCommandSlotState(slotId, out ResolvedCommandSlotState state) ||
            state.selectedOption == null ||
            !string.Equals(
                ResolveOptionId(state.selectedOption, state.selectedOptionIndex),
                optionId,
                StringComparison.Ordinal))
        {
            return;
        }

        ApplySkillOptionToSlot(state, state.selectedOption, optionId);

        if (state.IsPassive)
            PassiveLoadoutChanged?.Invoke();
    }

    /// <summary>
    /// Re-applies a Helper variant after its Skill Tree changed, so a node unlocked in the lobby
    /// reaches the next cast. Returns true when the key belonged to the Helper half.
    /// </summary>
    private bool HandleHelperTreeChanged(string slotId, string optionId)
    {
        if (!slotId.StartsWith(CharacterSkillLoadoutKeys.HelperCommandPrefix, StringComparison.Ordinal) &&
            !slotId.StartsWith(CharacterSkillLoadoutKeys.HelperProcPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        RefreshResolvedCommandSlotsIfNeeded();
        string resolvedSlotId = slotId.Trim();

        if (helperCommandState.statsSlot != null &&
            string.Equals(helperCommandState.slotId, resolvedSlotId, StringComparison.Ordinal))
        {
            if (helperCommandState.selectedOption != null &&
                string.Equals(
                    ResolveOptionId(helperCommandState.selectedOption, helperCommandState.selectedOptionIndex),
                    optionId,
                    StringComparison.Ordinal))
            {
                ApplyHelperCommandOption(
                    helperCommandState.selectedOption,
                    helperCommandState.selectedOptionIndex);
            }

            return true;
        }

        if (!TryGetHelperProcState(resolvedSlotId, out ResolvedHelperProcState state))
            return true;

        if (state.selectedOption != null &&
            string.Equals(
                CharacterSkillLoadoutKeys.OptionKey(state.selectedOption, state.selectedOptionIndex),
                optionId,
                StringComparison.Ordinal))
        {
            ApplyHelperProcOption(state, state.selectedOption, state.selectedOptionIndex);
        }

        return true;
    }

    private static void ClearRuntimeSlot(SkillSlot slot)
    {
        if (slot == null)
            return;

        slot.skillAsset = null;
        slot.runtimeSkill = null;
    }

    private static string FindSavedOptionId(List<CharacterSkillSelectionSaveData> savedSelections, string slotId)
    {
        if (savedSelections == null || string.IsNullOrWhiteSpace(slotId))
            return null;

        for (int i = 0; i < savedSelections.Count; i++)
        {
            CharacterSkillSelectionSaveData entry = savedSelections[i];
            if (entry == null)
                continue;

            if (string.Equals(entry.slotId, slotId, StringComparison.Ordinal))
                return entry.optionId;
        }

        return null;
    }

    private static string ResolveSlotId(CharacterSkillLoadoutSlot slot, int slotIndex)
    {
        return CharacterSkillLoadoutKeys.StrykerSlotKey(slot, slotIndex);
    }

    private static string ResolveOptionId(CharacterSkillLoadoutOption option, int optionIndex)
    {
        return CharacterSkillLoadoutKeys.OptionKey(option, optionIndex);
    }

    public bool IsPassiveSlot(SkillSlot slot)
    {
        for (int i = 0; i < resolvedCommandSlotStates.Count; i++)
        {
            ResolvedCommandSlotState state = resolvedCommandSlotStates[i];
            if (state != null && ReferenceEquals(state.slot, slot))
                return state.IsPassive;
        }

        return false;
    }

    public string BuildSlotDebugSource(SkillSlot slot)
    {
        for (int i = 0; i < resolvedCommandSlotStates.Count; i++)
        {
            ResolvedCommandSlotState state = resolvedCommandSlotStates[i];
            if (state == null || !ReferenceEquals(state.slot, slot))
                continue;

            return !string.IsNullOrWhiteSpace(state.slotId)
                ? $"slot:{state.slotId}"
                : $"slot:{i}";
        }

        return slot != null ? $"slot:{slot.hotkey}" : "slot:<null>";
    }
}
