using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.Serialization;

public class CharacterSkillManager : MonoBehaviour, IGameSaveAble, ISaveOrder
{
    static readonly SkillSlot[] EmptyAutonomousSlots = Array.Empty<SkillSlot>();
    static readonly CharacterSkillLoadoutOption[] EmptySkillOptions = Array.Empty<CharacterSkillLoadoutOption>();

    sealed class ResolvedCommandSlotState
    {
        public SkillSlot slot;
        public CharacterSkillLoadoutSlot statsSlot;
        public CharacterSkillLoadoutOption selectedOption;
        public int selectedOptionIndex = -1;
        public string slotId;
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

    private CharacteContext ctx;
    private CharacterActiveSkillProgress activeSkillProgress;
    private CharacterAnimBrain animBrain;
    private CharacterAnimDriver animDriver;
    private WeaponSystem weaponSystem;
    private SkillSlot pendingSlot;
    private SkillCastOrchestrator castOrchestrator;
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

    public event Action<ActiveSkillCastInfo> CastStarted;
    public event Action<ActiveSkillCastInfo> CastReleased;
    public event Action<ActiveSkillCastInfo, SkillCastCancelReason> CastCancelled;

    /// <summary>Raised when a payload ran but produced nothing, so the cast cost nothing.</summary>
    public event Action<ActiveSkillCastInfo, SkillExecutionResult> CastExecutionFailed;
    public event Action PassiveLoadoutChanged;

    /// <summary>
    /// Raised when the Helper half of this character's loadout is rebuilt or a proc variant is
    /// switched, so the proc controller drops the definitions that are no longer equipped.
    /// </summary>
    public event Action HelperProcLoadoutChanged;

    [Header("Autonomous Loadout")]
    public ISkillUser skillUser;
    [FormerlySerializedAs("slots")]
    [SerializeField] private SkillSlot[] autonomousSlots;

    [Header("Chain Attack")]
    [SerializeField] private CharacterSkillEntry chainAttackSkill;

    /// <summary>
    /// Entries for skills driven from outside the serialized loadout (helper assists, scripted
    /// casts). Kept for the component's lifetime so each definition keeps one runtime instance.
    /// </summary>
    private readonly Dictionary<SkillGemDefinition, CharacterSkillEntry> externalSkillEntries =
        new Dictionary<SkillGemDefinition, CharacterSkillEntry>();

    public int LoadOrder => -90;
    public IReadOnlyList<SkillSlot> AutonomousSlots
    {
        get
        {
            RefreshResolvedCommandSlotsIfNeeded();
            return commandSlotsBuilt ? resolvedCommandSlots : EmptyAutonomousSlots;
        }
    }
    public IReadOnlyList<SkillSlot> CommandSlots => AutonomousSlots;
    /// <summary>
    /// The manual command this character performs as a helper, or <c>null</c> when it owns none.
    ///
    /// Resolved from <c>ctx.baseStats</c>, never from the prefab: an empty slot on a Helper-role
    /// character means "no manual command" and must not fall back to anything.
    /// </summary>
    public CharacterSkillEntry PlayerCommandSkill
    {
        get
        {
            CacheReferences();
            return IsSkillEntryConfigured(helperCommandSkillEntry) ? helperCommandSkillEntry : null;
        }
    }

    public CharacterSkillEntry ChainAttackSkill => chainAttackSkill;

    public bool HasConfiguredPlayerCommandSkill
    {
        get
        {
            CacheReferences();
            return IsSkillEntryConfigured(helperCommandSkillEntry);
        }
    }

    public bool HasConfiguredChainAttackSkill => IsSkillEntryConfigured(chainAttackSkill);

    public bool HasConfiguredPassiveSlots
    {
        get
        {
            CacheReferences();
            RefreshResolvedCommandSlotsIfNeeded();

            for (int i = 0; i < resolvedCommandSlotStates.Count; i++)
            {
                if (resolvedCommandSlotStates[i]?.IsPassive == true)
                    return true;
            }

            return false;
        }
    }

    private void Awake()
    {
        CacheReferences();
        EnsureCastOrchestrator();

        if (skillUser == null)
            Debug.LogError("CharacterSkillManager requires an ISkillUser component.");

        RebuildAllRuntimeSkills();
    }

    private void OnEnable()
    {
        CacheReferences();
        SubscribeToActiveSkillProgress();

        if (ctx != null && ctx.HealthSystem != null)
        {
            ctx.HealthSystem.CharacterDown += OnCharacterDown;
            ctx.HealthSystem.CharacterDead += OnCharacterDead;
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromActiveSkillProgress();

        if (ctx != null && ctx.HealthSystem != null)
        {
            ctx.HealthSystem.CharacterDown -= OnCharacterDown;
            ctx.HealthSystem.CharacterDead -= OnCharacterDead;
        }

        pendingSlot = null;
        castOrchestrator?.CancelPendingCast(SkillCastCancelReason.Disabled);
    }

    private void OnDestroy()
    {
        pendingSlot = null;
        castOrchestrator?.CancelPendingCast(SkillCastCancelReason.Disabled);
    }

    public void OnSave(GameSaveData data)
    {
    }

    public void OnLoad(GameSaveData data)
    {
        CacheReferences();

        // Charges are deliberately not persisted, so a loaded run always starts with full pools.
        EnsureCastOrchestrator();
        castOrchestrator.ResetAllChargesToFull();

        RebuildResolvedCommandSlots();
    }

    private void Update()
    {
        RefreshResolvedCommandSlotsIfNeeded();
        castOrchestrator?.Tick();
        if (castOrchestrator != null && castOrchestrator.HasPendingCast)
            return;

        // No cast pending: release any reserved slot so CancelPendingSlotIfNeeded doesn't touch a stale one.
        pendingSlot = null;
    }

    public bool TryCastSlot(int slotIndex)
    {
        return TryStartCastSlot(slotIndex).Started;
    }

    public SkillCastStartResult TryStartCastSlot(int slotIndex)
    {
        CacheReferences();

        if (!TryGetCommandSlot(slotIndex, out SkillSlot slot))
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        return TryBeginCast(slot);
    }

    public bool HasConfiguredCommandSlot(int slotIndex)
    {
        return TryGetCommandSlot(slotIndex, out SkillSlot slot) &&
               slot != null &&
               slot.skillAsset != null;
    }

    /// <summary>
    /// Skill currently assigned to a command slot, for HUD icons and tooltips. This is a plain
    /// read of the resolved loadout: it does not build a runtime skill or touch the charge pool,
    /// so a HUD can poll it alongside <see cref="TryGetSlotChargeStatus"/> without paying twice.
    /// </summary>
    public bool TryGetSlotSkillDefinition(int slotIndex, out SkillGemDefinition skillDef)
    {
        skillDef = null;

        if (!TryGetCommandSlot(slotIndex, out SkillSlot slot))
            return false;

        skillDef = slot.skillAsset;
        return skillDef != null;
    }

    public bool CanStartCastSlot(int slotIndex)
    {
        CacheReferences();

        if (!TryGetCommandSlot(slotIndex, out SkillSlot slot))
            return false;

        EnsureCommandRuntimeSkill(slot);
        return slot != null &&
               slot.runtimeSkill != null &&
               skillUser != null &&
               !CutsceneDirector.IsCinematicPlaying &&
               !IsSkillStartBlockedByAnimation() &&
               !IsSkillUseBlocked() &&
               slot.runtimeSkill.CanCast(skillUser);
    }

    /// <summary>
    /// Charge readout for a command slot, taken from the same shared pool the cast path spends
    /// from. A slot whose skill has never been cast reports a full pool, not "unknown".
    /// </summary>
    public bool TryGetSlotChargeStatus(int slotIndex, out SkillChargeStatus status)
    {
        status = default;
        CacheReferences();

        if (!TryGetCommandSlot(slotIndex, out SkillSlot slot) || skillUser == null)
            return false;

        EnsureCommandRuntimeSkill(slot);
        return slot.runtimeSkill != null &&
               slot.runtimeSkill.TryGetChargeStatus(skillUser, out status);
    }

    public bool TrySelectSkillOption(int slotIndex, int optionIndex, bool persist = true)
    {
        CacheReferences();

        if (!TryGetCommandSlotState(slotIndex, out ResolvedCommandSlotState state))
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
        CacheReferences();

        if (!TryGetCommandSlotState(slotId, out ResolvedCommandSlotState state))
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
        CacheReferences();
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
        CacheReferences();
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
        CacheReferences();
        option = null;

        if (!TryGetCommandSlotState(slotIndex, out ResolvedCommandSlotState state))
            return false;

        option = state.selectedOption;
        return option != null;
    }

    public bool TryGetSelectedSkillOption(string slotId, out CharacterSkillLoadoutOption option)
    {
        CacheReferences();
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
        CacheReferences();

        if (TrySelectSkillOption(slotId, optionId, persist))
            return true;

        return TrySelectHelperOption(slotId, optionId, persist);
    }

    /// <summary>Option id currently equipped in <paramref name="slotId"/>, across both roles.</summary>
    public bool TryGetSelectedLoadoutOptionId(string slotId, out string optionId)
    {
        CacheReferences();
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
                CancelActiveCastFor(previousEntry?.runtimeSkill);
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

    public bool CanStartPlayerCommandSkill()
    {
        CacheReferences();
        return helperCommandSkillEntry.runtimeSkill != null &&
               skillUser != null &&
               !CutsceneDirector.IsCinematicPlaying &&
               !IsSkillStartBlockedByAnimation() &&
               !IsSkillUseBlocked() &&
               helperCommandSkillEntry.runtimeSkill.CanCast(skillUser);
    }

    public bool TryGetActiveCast(out ActiveSkillCastInfo castInfo)
    {
        EnsureCastOrchestrator();
        return castOrchestrator.TryGetActiveCast(out castInfo);
    }

    public bool TryCancelActiveCast(SkillCastCancelReason reason)
    {
        EnsureCastOrchestrator();
        return castOrchestrator.TryCancelActiveCast(reason);
    }

    public SkillCastStartResult TryStartPlayerCommandSkill()
    {
        CacheReferences();
        return TryBeginEntryCast(
            helperCommandSkillEntry,
            "player-command",
            usePlanarRootMotion: true);
    }

    public bool CanStartExternalSkill(CharacterSkillEntry entry, bool ignoreResourceCosts = false)
    {
        CacheReferences();
        EnsureRuntimeSkill(entry);
        if (entry == null || entry.runtimeSkill == null || skillUser == null)
            return false;
        if (CutsceneDirector.IsCinematicPlaying || IsSkillStartBlockedByAnimation() || IsSkillUseBlocked())
            return false;
        if (ignoreResourceCosts)
            return true;
        return entry.runtimeSkill.CanCast(skillUser);
    }

    public SkillCastStartResult TryStartExternalSkill(
        CharacterSkillEntry entry,
        string debugSource,
        CombatTimelineEventName requiredTimelineEvent = CombatTimelineEventName.None,
        bool usePlanarRootMotion = false,
        bool ignoreResourceCosts = false,
        bool stampCooldown = true,
        SkillTargetHandle primaryTarget = null,
        SkillCastCostPolicy costPolicy = SkillCastCostPolicy.Normal,
        int externalAnimationRequestId = 0)
    {
        CacheReferences();
        return TryBeginEntryCast(
            entry,
            debugSource,
            requiredTimelineEvent,
            usePlanarRootMotion,
            ignoreResourceCosts,
            stampCooldown,
            primaryTarget,
            costPolicy,
            externalAnimationRequestId);
    }

    /// <summary>
    /// Starts a skill this character owns at runtime rather than through a serialized slot.
    ///
    /// Everything an external caster needs to get right lives on this path: the entry is cached,
    /// so its <see cref="SkillInstance"/> is created once, and creating it through the manager is
    /// what binds it to this character's shared charge pool for that definition. Building a
    /// <c>new SkillInstance</c> by hand instead leaves it on a private throwaway pool, which
    /// silently gives the skill no cooldown at all.
    ///
    /// Pass <paramref name="externalAnimationRequestId"/> when the caller is already playing this
    /// skill's animation and only wants the metered cast bolted onto it - see
    /// <see cref="TryBeginEntryCast"/> for what that changes.
    /// </summary>
    public SkillCastStartResult TryStartExternalSkill(
        SkillGemDefinition skillDef,
        string debugSource,
        CombatTimelineEventName requiredTimelineEvent = CombatTimelineEventName.None,
        bool usePlanarRootMotion = false,
        bool stampCooldown = true,
        SkillTargetHandle primaryTarget = null,
        SkillCastCostPolicy costPolicy = SkillCastCostPolicy.Normal,
        int externalAnimationRequestId = 0)
    {
        CharacterSkillEntry entry = GetOrCreateExternalEntry(skillDef);
        if (entry == null)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        return TryStartExternalSkill(
            entry,
            debugSource,
            requiredTimelineEvent,
            usePlanarRootMotion,
            ignoreResourceCosts: false,
            stampCooldown: stampCooldown,
            primaryTarget: primaryTarget,
            costPolicy: costPolicy,
            externalAnimationRequestId: externalAnimationRequestId);
    }

    /// <summary>
    /// Starts the selected runtime entry for one Helper proc. Resolving by proc definition keeps
    /// the selected slot/option snapshot attached even when another proc variant uses the same
    /// execution skill. The runtime skill still binds to the shared charge pool for that skill.
    /// </summary>
    public SkillCastStartResult TryStartHelperProcSkill(
        SkillHelperDef helperProc,
        string debugSource,
        CombatTimelineEventName requiredTimelineEvent = CombatTimelineEventName.None,
        bool usePlanarRootMotion = false,
        bool stampCooldown = true,
        SkillTargetHandle primaryTarget = null,
        SkillCastCostPolicy costPolicy = SkillCastCostPolicy.Normal,
        int externalAnimationRequestId = 0)
    {
        CacheReferences();
        RefreshResolvedCommandSlotsIfNeeded();

        if (!TryGetSelectedHelperProcState(helperProc, out ResolvedHelperProcState state))
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        CharacterSkillEntry entry = state.runtimeEntry ?? GetOrCreateHelperProcEntry(helperProc);
        if (entry == null)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        return TryStartExternalSkill(
            entry,
            debugSource,
            requiredTimelineEvent,
            usePlanarRootMotion,
            ignoreResourceCosts: false,
            stampCooldown: stampCooldown,
            primaryTarget: primaryTarget,
            costPolicy: costPolicy,
            externalAnimationRequestId: externalAnimationRequestId);
    }

    /// <summary>Returns the live runtime entry for the currently selected proc variant.</summary>
    public bool TryGetHelperProcRuntimeSkill(SkillHelperDef helperProc, out SkillInstance runtimeSkill)
    {
        runtimeSkill = null;
        if (helperProc == null)
            return false;

        CacheReferences();
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

    /// <summary>Charge readout for a selected Helper proc, using its variant snapshot.</summary>
    public bool TryGetHelperProcChargeStatus(SkillHelperDef helperProc, out SkillChargeStatus status)
    {
        status = default;
        if (!TryGetHelperProcRuntimeSkill(helperProc, out SkillInstance runtimeSkill) ||
            skillUser == null)
        {
            return false;
        }

        return runtimeSkill.TryGetChargeStatus(skillUser, out status);
    }

    /// <summary>Affordability check for the definition-based external cast path.</summary>
    public bool CanStartExternalSkill(SkillGemDefinition skillDef, SkillCastCostPolicy costPolicy)
    {
        CharacterSkillEntry entry = GetOrCreateExternalEntry(skillDef);
        if (entry == null)
            return false;

        CacheReferences();
        EnsureRuntimeSkill(entry);

        if (entry.runtimeSkill == null || skillUser == null)
            return false;

        if (CutsceneDirector.IsCinematicPlaying || IsSkillStartBlockedByAnimation() || IsSkillUseBlocked())
            return false;

        return entry.runtimeSkill.CanCast(skillUser, costPolicy, out _);
    }

    /// <summary>Charge readout for an externally driven skill, for cooldown scheduling and HUD.</summary>
    public bool TryGetExternalSkillChargeStatus(SkillGemDefinition skillDef, out SkillChargeStatus status)
    {
        status = default;

        CharacterSkillEntry entry = GetOrCreateExternalEntry(skillDef);
        if (entry == null)
            return false;

        CacheReferences();
        EnsureRuntimeSkill(entry);

        return entry.runtimeSkill != null &&
               skillUser != null &&
               entry.runtimeSkill.TryGetChargeStatus(skillUser, out status);
    }

    /// <summary>
    /// One entry per definition, kept for the lifetime of this component. The charge pool itself
    /// lives in the orchestrator and is keyed by definition, so it would survive a rebuilt entry
    /// anyway - caching keeps allocation and cast diagnostics stable.
    /// </summary>
    private CharacterSkillEntry GetOrCreateExternalEntry(SkillGemDefinition skillDef)
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

    public bool TryGetChainAttackRuntimeSkill(out SkillInstance runtimeSkill)
    {
        CacheReferences();
        EnsureRuntimeSkill(chainAttackSkill);

        runtimeSkill = chainAttackSkill != null ? chainAttackSkill.runtimeSkill : null;
        return runtimeSkill != null && runtimeSkill.def != null;
    }

    public bool TryGetChainAttackSkillDefinition(out SkillGemDefinition skillDef, out SkillInstance runtimeSkill)
    {
        skillDef = null;

        if (!TryGetChainAttackRuntimeSkill(out runtimeSkill))
            return false;

        skillDef = runtimeSkill.def;
        return true;
    }

    /// <summary>
    /// Helper procs this actor contributes: one selected variant per slot, and nothing else.
    ///
    /// Every party-slot rig and the helper actor itself are shared prefabs, so a prefab-authored
    /// proc would belong to whoever is currently loaded into that rig. Reading from
    /// <c>ctx.baseStats</c> - the same place command slots come from - keeps the proc tied to the
    /// character and drops it the moment that character leaves the party. Unselected variants are
    /// authored but not equipped, so they never reach the proc controller.
    /// </summary>
    public void AppendConfiguredHelperChainDefinitions(List<SkillHelperDef> buffer, HashSet<SkillHelperDef> dedupe = null)
    {
        if (buffer == null)
            return;

        CacheReferences();
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

        CacheReferences();
        RefreshResolvedCommandSlotsIfNeeded();

        for (int i = 0; i < resolvedCommandSlotStates.Count; i++)
        {
            ResolvedCommandSlotState state = resolvedCommandSlotStates[i];
            if (state == null || !state.IsPassive)
                continue;

            PassiveDefinition definition = state.selectedOption != null ? state.selectedOption.PassiveAsset : null;
            if (definition != null)
                buffer.Add(new EquippedPassive(definition, state.upgradeSnapshot));
        }
    }

    public void ClearSlot(int index)
    {
        if (!TryGetCommandSlot(index, out SkillSlot slot))
            return;

        CancelPendingSlotIfNeeded(slot);

        ClearRuntimeSlot(slot);
        if (TryGetCommandSlotState(index, out ResolvedCommandSlotState state))
        {
            state.selectedOption = null;
            state.selectedOptionIndex = -1;
        }
    }

    public void AssignSkillToSlot(int index, SkillGemDefinition asset)
    {
        if (!TryGetCommandSlot(index, out SkillSlot slot))
            return;

        CancelPendingSlotIfNeeded(slot);

        slot.skillAsset = asset;
        slot.runtimeSkill = BuildRuntimeSkill(slot, asset);
        if (TryGetCommandSlotState(index, out ResolvedCommandSlotState state))
        {
            state.selectedOption = null;
            state.selectedOptionIndex = -1;
        }
    }

    private SkillCastStartResult TryBeginCast(SkillSlot slot)
    {
        CacheReferences();

        if (IsPassiveSlot(slot))
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        EnsureCommandRuntimeSkill(slot);

        if (CutsceneDirector.IsCinematicPlaying)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        if (slot == null || slot.runtimeSkill == null || skillUser == null)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        if (IsSkillStartBlockedByAnimation())
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        EnsureCastOrchestrator();
        SkillInstance runtimeSkill = slot.runtimeSkill;
        SkillCastStartResult result = castOrchestrator.TryStartCast(new SkillCastRequest(
            runtimeSkill,
            skillUser,
            animationDriver: animDriver,
            canProceed: () => IsSlotCastStillValid(slot, runtimeSkill),
            onStarted: () => OnCommandSkillCastStarted(runtimeSkill),
            useAnimationDriver: true,
            allowImmediateFallback: true,
            debugSource: BuildSlotDebugSource(slot)));

        pendingSlot = result.Kind == SkillCastStartKind.WaitingForAnimation
            ? slot
            : null;

        return result;
    }

    private void OnCommandSkillCastStarted(SkillInstance runtimeSkill)
    {
        StopWeaponActivityForSkillCast();

        SkillPayloadDef payload = runtimeSkill != null && runtimeSkill.def != null
            ? runtimeSkill.def.payload
            : null;
        if (payload == null ||
            payload.HelperFacingMode != SkillHelperFacingMode.FaceDetectedTargetOnCast ||
            skillUser == null)
        {
            return;
        }

        Vector3 lookDirection = Vector3.ProjectOnPlane(skillUser.AimDirection, Vector3.up);
        if (lookDirection.sqrMagnitude <= 0.0001f || skillUser is not Component skillUserComponent)
            return;

        CharacteContext characterContext = skillUserComponent.GetComponentInParent<CharacteContext>();
        Transform actorRoot = characterContext != null
            ? characterContext.transform
            : skillUserComponent.transform;
        actorRoot.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    private SkillCastStartResult TryBeginEntryCast(
        CharacterSkillEntry entry,
        string debugSource,
        CombatTimelineEventName requiredTimelineEvent = CombatTimelineEventName.None,
        bool usePlanarRootMotion = false,
        bool ignoreResourceCosts = false,
        bool stampCooldown = true,
        SkillTargetHandle primaryTarget = null,
        SkillCastCostPolicy costPolicy = SkillCastCostPolicy.Normal,
        int externalAnimationRequestId = 0)
    {
        CacheReferences();
        EnsureRuntimeSkill(entry);

        if (CutsceneDirector.IsCinematicPlaying)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        if (entry == null || entry.runtimeSkill == null || skillUser == null)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        // A caller that already drives this skill's animation (the helper summon path) asks to be
        // bolted onto that request rather than starting a second one. Starting our own playback
        // would be refused anyway, and the blocking playback we would be refused for is the very
        // animation this cast belongs to - so the guard has to stand down for that case only.
        bool attachToExternalAnimation = externalAnimationRequestId > 0;

        if (!attachToExternalAnimation && IsSkillStartBlockedByAnimation())
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        EnsureCastOrchestrator();
        SkillInstance runtimeSkill = entry.runtimeSkill;
        return castOrchestrator.TryStartCast(new SkillCastRequest(
            runtimeSkill,
            skillUser,
            animationDriver: animDriver,
            canProceed: () => IsSkillEntryCastStillValid(entry, runtimeSkill),
            onStarted: StopWeaponActivityForSkillCast,
            requestedId: externalAnimationRequestId,
            useAnimationDriver: !attachToExternalAnimation,
            allowImmediateFallback: true,
            requiredTimelineEvent: requiredTimelineEvent,
            usePlanarRootMotion: usePlanarRootMotion,
            ignoreResourceCosts: ignoreResourceCosts,
            stampCooldown: stampCooldown,
            debugSource: debugSource,
            primaryTarget: primaryTarget,
            costPolicy: costPolicy));
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
    /// holding the same skill draw from — and display — the same charges.
    /// </summary>
    private void BindSharedCharges(SkillInstance instance)
    {
        if (instance == null || instance.def == null)
            return;

        EnsureCastOrchestrator();
        instance.BindCharges(castOrchestrator.GetOrCreateCharges(instance.def));
    }

    private bool TryGetCommandSlot(int slotIndex, out SkillSlot slot)
    {
        slot = null;
        RefreshResolvedCommandSlotsIfNeeded();

        if (slotIndex < 0 || slotIndex >= resolvedCommandSlots.Count)
            return false;

        slot = resolvedCommandSlots[slotIndex];
        return slot != null;
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

    private void RebuildAllRuntimeSkills()
    {
        RebuildResolvedCommandSlots();
        RefreshHelperLoadout(force: true);
        EnsureRuntimeSkill(chainAttackSkill);
    }

    private void EnsureCommandRuntimeSkill(SkillSlot slot)
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

    private void EnsureRuntimeSkill(CharacterSkillEntry entry)
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

    private void CacheReferences()
    {
        if (ctx == null)
        {
            TryGetComponent(out ctx);

            // includeInactive matters: this manager is routinely asked about a helper actor that
            // is deactivated between summons, and the default overload skips inactive objects.
            // Walking ancestors is also what keeps the answer correct - several actors share one
            // squad root, and a wider search from an inactive actor can surface a sibling
            // character's context instead of this one's.
            if (ctx == null)
                ctx = GetComponentInParent<CharacteContext>(true);

            // Last resort for prefabs that host the context below this component.
            if (ctx == null)
                ctx = CharacterContextModuleLookup.ResolveContext(this);
        }

        ctx?.ResolveReferences();

        skillUser = GetComponent<ISkillUser>();
        if (skillUser == null && ctx != null && ctx.EnegySystem != null)
            skillUser = ctx.EnegySystem;

        animDriver = ResolveAnimDriverReference();
        animBrain = animDriver != null && animDriver.Brain != null
            ? animDriver.Brain
            : ResolveAnimBrainReference();
        weaponSystem = ctx != null ? ctx.WeaponSystem : null;
        if (weaponSystem == null)
            weaponSystem = GetComponent<WeaponSystem>();
        if (weaponSystem == null && ctx != null)
            weaponSystem = ctx.GetComponentInChildren<WeaponSystem>(true);

        if (ctx == null)
            return;

        CharacterActiveSkillProgress resolvedProgress = ctx.ActiveSkillProgress;
        if (activeSkillProgress != resolvedProgress)
        {
            UnsubscribeFromActiveSkillProgress();
            activeSkillProgress = resolvedProgress;
            if (isActiveAndEnabled)
                SubscribeToActiveSkillProgress();
        }

        if (ctx.stateHub == null)
            ctx.stateHub = ctx.GetComponentInChildren<StateHub>(true);

        if (ctx.HealthSystem == null)
            ctx.HealthSystem = ctx.GetComponentInChildren<HealthSystem>(true);

        if (ctx.SkillManager == null)
            ctx.SkillManager = this;

        if (ctx.AnimBrain == null)
            ctx.AnimBrain = animBrain;
        else if (animBrain == null)
            animBrain = ctx.AnimBrain;

        if (ctx.WeaponSystem == null)
            ctx.WeaponSystem = weaponSystem;
        else if (weaponSystem == null)
            weaponSystem = ctx.WeaponSystem;

        RefreshResolvedCommandSlotsIfNeeded();
    }

    /// <summary>
    /// Re-resolves everything this actor takes from its character asset.
    ///
    /// Call this when <c>ctx.baseStats</c> is swapped from outside - a helper is loaded with a new
    /// character while its GameObject is deactivated between summons, so it cannot notice the
    /// change from its own <c>Update</c>.
    /// </summary>
    public void RefreshCharacterOwnedLoadout()
    {
        CacheReferences();
        RefreshResolvedCommandSlotsIfNeeded();
    }

    private void RefreshResolvedCommandSlotsIfNeeded()
    {
        RefreshHelperLoadout(force: false);

        CharacterStats currentBaseStats = ctx != null ? ctx.baseStats : null;
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
    private void RefreshHelperLoadout(bool force)
    {
        CharacterStats stats = ctx != null ? ctx.baseStats : null;

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
            CancelActiveCastFor(previousRuntimeSkill);

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
    /// The entry is kept for the component's lifetime so the skill keeps one shared charge pool;
    /// re-creating it on a variant switch would hand the player a free reset of that cooldown.
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

    /// <summary>Cancels an in-flight cast, but only when it is the one running <paramref name="runtimeSkill"/>.</summary>
    private void CancelActiveCastFor(SkillInstance runtimeSkill)
    {
        if (runtimeSkill == null || castOrchestrator == null)
            return;

        if (castOrchestrator.TryGetActiveCast(out ActiveSkillCastInfo castInfo) &&
            ReferenceEquals(castInfo.RuntimeSkill, runtimeSkill))
        {
            castOrchestrator.TryCancelActiveCast(SkillCastCancelReason.InvalidState);
        }
    }

    private void RebuildResolvedCommandSlots()
    {
        observedBaseStats = ctx != null ? ctx.baseStats : null;
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
            slotId = ResolveSlotId(statsSlot, slotIndex)
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
            CancelPendingSlotIfNeeded(state.slot);

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

    private void SubscribeToActiveSkillProgress()
    {
        if (activeSkillProgress == null)
            return;

        activeSkillProgress.TreeChanged -= HandleActiveSkillTreeChanged;
        activeSkillProgress.TreeChanged += HandleActiveSkillTreeChanged;
    }

    private void UnsubscribeFromActiveSkillProgress()
    {
        if (activeSkillProgress != null)
            activeSkillProgress.TreeChanged -= HandleActiveSkillTreeChanged;
    }

    private void HandleActiveSkillTreeChanged(string slotId, string optionId)
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

    private void CancelPendingSlotIfNeeded(SkillSlot slot)
    {
        if (!ReferenceEquals(pendingSlot, slot))
            return;

        pendingSlot = null;
        castOrchestrator?.CancelPendingCast(SkillCastCancelReason.InvalidState);
    }

    private List<CharacterSkillSelectionSaveData> LoadSavedSkillSelections()
    {
        string characterId = ResolveCharacterIdForSave();
        if (string.IsNullOrWhiteSpace(characterId) || SaveManager.Instance == null)
            return null;

        CharacterProgressData progress = SaveManager.Instance.LoadCharacterProgressData(characterId);
        return progress != null ? progress.selectedSkillOptions : null;
    }

    private void PersistSkillSelection(string slotId, string optionId)
    {
        string characterId = ResolveCharacterIdForSave();
        if (string.IsNullOrWhiteSpace(characterId) ||
            string.IsNullOrWhiteSpace(slotId) ||
            string.IsNullOrWhiteSpace(optionId) ||
            SaveManager.Instance == null)
        {
            return;
        }

        CharacterProgressData progress = SaveManager.Instance.LoadCharacterProgressData(characterId);
        progress.selectedSkillOptions ??= new List<CharacterSkillSelectionSaveData>();

        CharacterSkillSelectionSaveData target = null;
        for (int i = progress.selectedSkillOptions.Count - 1; i >= 0; i--)
        {
            CharacterSkillSelectionSaveData entry = progress.selectedSkillOptions[i];
            if (entry == null)
            {
                progress.selectedSkillOptions.RemoveAt(i);
                continue;
            }

            if (!string.Equals(entry.slotId, slotId, StringComparison.Ordinal))
                continue;

            if (target == null)
                target = entry;
            else
                progress.selectedSkillOptions.RemoveAt(i);
        }

        if (target == null)
        {
            target = new CharacterSkillSelectionSaveData();
            progress.selectedSkillOptions.Add(target);
        }

        target.slotId = slotId;
        target.optionId = optionId;
        SaveManager.Instance.SaveCharacterProgressData(characterId, progress);
    }

    private string ResolveCharacterIdForSave()
    {
        CharacterStats stats = ctx != null ? ctx.baseStats : null;
        return stats != null && !string.IsNullOrWhiteSpace(stats.characterId)
            ? stats.characterId.Trim()
            : null;
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

    private bool IsPassiveSlot(SkillSlot slot)
    {
        for (int i = 0; i < resolvedCommandSlotStates.Count; i++)
        {
            ResolvedCommandSlotState state = resolvedCommandSlotStates[i];
            if (state != null && ReferenceEquals(state.slot, slot))
                return state.IsPassive;
        }

        return false;
    }

    private string BuildSlotDebugSource(SkillSlot slot)
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

    private void EnsureCastOrchestrator()
    {
        if (castOrchestrator != null)
            return;

        castOrchestrator = new SkillCastOrchestrator(this);
        castOrchestrator.CastStarted += OnCastStarted;
        castOrchestrator.CastReleased += OnCastReleased;
        castOrchestrator.CastCancelled += OnCastCancelled;
        castOrchestrator.CastExecutionFailed += OnCastExecutionFailed;
    }

    private void OnCastExecutionFailed(ActiveSkillCastInfo castInfo, SkillExecutionResult result)
    {
        CastExecutionFailed?.Invoke(castInfo, result);
    }

    private void OnCastStarted(ActiveSkillCastInfo castInfo)
    {
        CastStarted?.Invoke(castInfo);
    }

    private void OnCastReleased(ActiveSkillCastInfo castInfo)
    {
        pendingSlot = null;
        CastReleased?.Invoke(castInfo);
    }

    private void OnCastCancelled(ActiveSkillCastInfo castInfo, SkillCastCancelReason reason)
    {
        pendingSlot = null;
        CastCancelled?.Invoke(castInfo, reason);
    }

    private CharacterAnimBrain ResolveAnimBrainReference()
    {
        if (ctx != null && ctx.AnimBrain != null)
            return ctx.AnimBrain;

        var resolved = GetComponent<CharacterAnimBrain>();
        if (resolved != null)
            return resolved;

        resolved = GetComponentInChildren<CharacterAnimBrain>(true);
        if (resolved != null)
            return resolved;

        resolved = GetComponentInParent<CharacterAnimBrain>();
        if (resolved != null)
            return resolved;

        return ctx != null ? ctx.GetComponentInChildren<CharacterAnimBrain>(true) : null;
    }

    private CharacterAnimDriver ResolveAnimDriverReference()
    {
        if (ctx != null && ctx.AnimDriver != null)
            return ctx.AnimDriver;

        var resolved = GetComponent<CharacterAnimDriver>();
        if (resolved != null)
            return resolved;

        resolved = GetComponentInChildren<CharacterAnimDriver>(true);
        if (resolved != null)
            return resolved;

        return GetComponentInParent<CharacterAnimDriver>();
    }

    private bool IsSlotCastStillValid(SkillSlot slot, SkillInstance runtimeSkill)
    {
        if (slot == null || runtimeSkill == null || runtimeSkill.def == null)
            return false;

        if (slot.runtimeSkill != runtimeSkill)
            return false;

        if (slot.skillAsset != runtimeSkill.def)
            return false;

        return !IsSkillUseBlocked();
    }

    private bool IsSkillEntryCastStillValid(CharacterSkillEntry entry, SkillInstance runtimeSkill)
    {
        if (entry == null || runtimeSkill == null || runtimeSkill.def == null)
            return false;

        if (entry.runtimeSkill != runtimeSkill)
            return false;

        if (entry.skillAsset != runtimeSkill.def)
            return false;

        return !IsSkillUseBlocked();
    }

    private static bool IsSkillEntryConfigured(CharacterSkillEntry entry)
    {
        return entry != null && entry.skillAsset != null;
    }

    private bool IsSkillUseBlocked()
    {
        return ctx != null && ctx.stateHub != null && !ctx.stateHub.CanUseSkill();
    }

    private bool IsSkillStartBlockedByAnimation()
    {
        return animBrain != null && animBrain.IsShootBlockingPlaybackActive;
    }

    private void StopWeaponActivityForSkillCast()
    {
        if (weaponSystem == null)
            weaponSystem = ctx != null ? ctx.WeaponSystem : GetComponent<WeaponSystem>();

        weaponSystem?.SetFiring(false);
        ctx?.stateHub?.SetFireHeld(false);

        if (weaponSystem != null && weaponSystem.IsReloading)
            weaponSystem.CancelReload();
    }

    private void OnCharacterDown()
    {
        pendingSlot = null;
        castOrchestrator?.CancelPendingCast(SkillCastCancelReason.CharacterDown);
    }

    private void OnCharacterDead()
    {
        pendingSlot = null;
        castOrchestrator?.CancelPendingCast(SkillCastCancelReason.CharacterDead);
    }
}

public enum SkillCastStartKind
{
    Rejected,
    ImmediateSuccess,
    WaitingForAnimation,
}

public readonly struct SkillCastStartResult
{
    public readonly SkillCastStartKind Kind;
    public readonly int RequestId;

    public bool Started => Kind != SkillCastStartKind.Rejected;

    public SkillCastStartResult(SkillCastStartKind kind, int requestId)
    {
        Kind = kind;
        RequestId = requestId;
    }
}

public readonly struct SkillCastRequest
{
    public readonly SkillInstance RuntimeSkill;
    public readonly ISkillUser SkillUser;
    public readonly CharacterAnimDriver AnimationDriver;
    public readonly Func<bool> CanProceed;
    public readonly Action OnStarted;
    public readonly int RequestedId;
    public readonly SkillCastCostPolicy CostPolicy;
    public readonly bool StampCooldown;

    /// <summary>
    /// Character this cast is aimed at, locked before the animation starts. Never null: a cast
    /// with no target carries <see cref="SkillTargetHandle.None"/>.
    /// </summary>
    public readonly SkillTargetHandle PrimaryTarget;

    /// <summary>Legacy read of <see cref="CostPolicy"/>. True only for the "free of everything" policy.</summary>
    public bool IgnoreResourceCosts => CostPolicy.IgnoresCharge();
    public readonly bool UseAnimationDriver;
    public readonly bool AllowImmediateFallback;
    public readonly CombatTimelineEventName RequiredTimelineEvent;
    public readonly bool UsePlanarRootMotion;
    public readonly string DebugSource;

    public SkillCastRequest(
        SkillInstance runtimeSkill,
        ISkillUser skillUser,
        CharacterAnimDriver animationDriver = null,
        Func<bool> canProceed = null,
        Action onStarted = null,
        int requestedId = 0,
        bool ignoreResourceCosts = false,
        bool stampCooldown = true,
        bool useAnimationDriver = true,
        bool allowImmediateFallback = true,
        CombatTimelineEventName requiredTimelineEvent = CombatTimelineEventName.None,
        bool usePlanarRootMotion = false,
        string debugSource = null,
        SkillTargetHandle primaryTarget = null,
        SkillCastCostPolicy costPolicy = SkillCastCostPolicy.Normal)
    {
        RuntimeSkill = runtimeSkill;
        SkillUser = skillUser;
        AnimationDriver = animationDriver;
        CanProceed = canProceed;
        OnStarted = onStarted;
        RequestedId = requestedId;

        // Two ways in, one field. The bool is the original API and stays authoritative for every
        // caller that never heard of the enum; an explicitly non-Normal policy wins because the
        // only way to ask for it is to pass it.
        CostPolicy = costPolicy != SkillCastCostPolicy.Normal
            ? costPolicy
            : SkillCastCostPolicies.FromLegacyFlag(ignoreResourceCosts);

        StampCooldown = stampCooldown;
        PrimaryTarget = primaryTarget ?? SkillTargetHandle.None;
        UseAnimationDriver = useAnimationDriver;
        AllowImmediateFallback = allowImmediateFallback;
        RequiredTimelineEvent = requiredTimelineEvent;
        UsePlanarRootMotion = usePlanarRootMotion;
        DebugSource = debugSource;
    }
}

public enum SkillCastCancelReason
{
    InvalidState,
    AnimationInterrupted,
    Disabled,
    Blocked,
    Stunned,
    Staggered,
    CharacterDown,
    CharacterDead,
}

public readonly struct ActiveSkillCastInfo
{
    public readonly int RequestId;
    public readonly SkillInstance RuntimeSkill;
    public readonly SkillGemDefinition SkillDef;
    public readonly ISkillUser SkillUser;
    public readonly CharacterAnimDriver AnimationDriver;
    public readonly float CastPointNormalized;
    public readonly bool Released;
    public readonly bool RequiresTimelineEvents;
    public readonly string DebugSource;

    public bool IsValid => RequestId > 0 && RuntimeSkill != null && SkillDef != null && SkillUser != null;

    public ActiveSkillCastInfo(
        int requestId,
        SkillInstance runtimeSkill,
        SkillGemDefinition skillDef,
        ISkillUser skillUser,
        CharacterAnimDriver animationDriver,
        float castPointNormalized,
        bool released,
        bool requiresTimelineEvents,
        string debugSource)
    {
        RequestId = requestId;
        RuntimeSkill = runtimeSkill;
        SkillDef = skillDef;
        SkillUser = skillUser;
        AnimationDriver = animationDriver;
        CastPointNormalized = castPointNormalized;
        Released = released;
        RequiresTimelineEvents = requiresTimelineEvents;
        DebugSource = debugSource;
    }
}

public sealed class SkillCastOrchestrator
{
    private sealed class PendingCastContext
    {
        public SkillCastRequest Request;
        public SkillInstance RuntimeSkill;

        /// <summary>
        /// Charge, energy, and the stats snapshot this cast was priced with, taken out of the
        /// pools the moment the cast started. Settled exactly once: committed at the cast point,
        /// rolled back on cancellation.
        /// </summary>
        public SkillCastReservation Reservation;
        public SkillGemDefinition SkillDef;
        public ISkillUser SkillUser;
        public CharacterAnimDriver AnimationDriver;
        public int RequestId;
        public bool Released;
        public bool Cancelled;
        public float CastPointNormalized;
        public bool RequiresTimelineEvents;
        public readonly List<CombatTimelineEventName> TimelineEventNames = new List<CombatTimelineEventName>();

        public ActiveSkillCastInfo ToInfo()
        {
            return new ActiveSkillCastInfo(
                RequestId,
                RuntimeSkill,
                SkillDef,
                SkillUser,
                AnimationDriver,
                CastPointNormalized,
                Released,
                RequiresTimelineEvents,
                Request.DebugSource);
        }
    }

    private readonly Component owner;

    // Charge pool shared by every slot that points at the same skill definition. A skill with
    // baseMaxCharges = 1 behaves exactly like the single shared cooldown this replaced.
    private readonly Dictionary<SkillGemDefinition, SkillChargeState> sharedCharges =
        new Dictionary<SkillGemDefinition, SkillChargeState>();

    private PendingCastContext pendingCast;
    private int nextCastRequestId = 1;

    public SkillCastOrchestrator(Component owner)
    {
        this.owner = owner;
    }

    public bool HasPendingCast => pendingCast != null;
    public int ActiveRequestId => pendingCast != null ? pendingCast.RequestId : 0;

    public event Action<ActiveSkillCastInfo> CastStarted;
    public event Action<ActiveSkillCastInfo> CastReleased;
    public event Action<ActiveSkillCastInfo, SkillCastCancelReason> CastCancelled;

    /// <summary>
    /// The payload ran but produced no gameplay effect, so nothing was committed: no energy,
    /// no charge, no cooldown. Presenters use this to explain the refusal to the player.
    /// </summary>
    public event Action<ActiveSkillCastInfo, SkillExecutionResult> CastExecutionFailed;

    public void Tick()
    {
        if (pendingCast == null)
            return;

        if (!CanProceed(pendingCast.Request))
            CancelPendingCast(SkillCastCancelReason.InvalidState);
    }

    public void CancelPendingCast()
    {
        CancelPendingCast(SkillCastCancelReason.Disabled);
    }

    public void CancelPendingCast(SkillCastCancelReason reason)
    {
        if (pendingCast == null)
            return;

        PendingCastContext context = pendingCast;
        pendingCast = null;
        Unsubscribe(context.AnimationDriver);
        CancelPendingCastRequest(context, stopAnimation: reason != SkillCastCancelReason.AnimationInterrupted);
        SettleCancelledCast(context, reason);
        CastCancelled?.Invoke(context.ToInfo(), reason);
    }

    public bool TryGetActiveCast(out ActiveSkillCastInfo castInfo)
    {
        if (pendingCast == null)
        {
            castInfo = default;
            return false;
        }

        castInfo = pendingCast.ToInfo();
        return true;
    }

    public bool TryCancelActiveCast(SkillCastCancelReason reason)
    {
        if (pendingCast == null || pendingCast.Released)
            return false;

        CancelPendingCast(reason);
        return true;
    }

    public SkillCastStartResult TryStartCast(in SkillCastRequest request)
    {
        if (pendingCast != null)
            return Rejected();

        if (!TryResolveStartState(request, out SkillInstance runtimeSkill, out ISkillUser skillUser, out SkillGemDefinition skillDef))
            return Rejected();

        int requestId = ResolveRequestId(request.RequestedId);
        CharacterAnimDriver executionAnimDriver = request.AnimationDriver;
        bool hasExternalSkillExecutionContext = HasActiveSkillExecutionContext(executionAnimDriver, requestId);
        bool requiresTimelineEvents =
            skillDef.RequiresSkillTimelineEvents ||
            CombatTimelineEventNames.IsValid(request.RequiredTimelineEvent);

        if (!request.UseAnimationDriver &&
            requiresTimelineEvents &&
            !hasExternalSkillExecutionContext)
        {
            WarnMissingTimelineDriver(skillDef, request.DebugSource);
            return Rejected();
        }

        // Resources leave the pools here, before a single animation frame plays. Holding them for
        // the whole wind-up is what stops a second press from spending the same charge, and the
        // stats snapshot taken with them is what this cast keeps being priced by even if a buff
        // lands halfway through.
        if (!runtimeSkill.TryReserveCast(
                skillUser,
                request.CostPolicy,
                request.StampCooldown,
                out SkillCastReservation reservation))
        {
            return Rejected();
        }

        if (request.UseAnimationDriver && executionAnimDriver != null)
        {
            var context = new PendingCastContext
            {
                Request = request,
                Reservation = reservation,
                RuntimeSkill = runtimeSkill,
                SkillDef = skillDef,
                SkillUser = skillUser,
                AnimationDriver = executionAnimDriver,
                RequestId = requestId,
                CastPointNormalized = skillDef.GetCastPointNormalized(),
                RequiresTimelineEvents = requiresTimelineEvents,
            };

            skillDef.CollectTimelineEventNames(context.TimelineEventNames);
            CombatTimelineEventNames.AddUnique(
                context.TimelineEventNames,
                request.RequiredTimelineEvent);

            bool started = executionAnimDriver.TryPlaySkill(
                context.RequestId,
                context.SkillDef,
                context.CastPointNormalized,
                context.TimelineEventNames,
                request.UsePlanarRootMotion);

            if (started)
            {
                Subscribe(context.AnimationDriver);
                pendingCast = context;

                // OnStarted has side effects on the caster (weapon stow, facing snap), so it only
                // runs once the animation driver has actually accepted the cast.
                request.OnStarted?.Invoke();
                CastStarted?.Invoke(context.ToInfo());
                return new SkillCastStartResult(SkillCastStartKind.WaitingForAnimation, context.RequestId);
            }
        }

        // Reaching here means no animation of our own will raise the timeline events - fatal for a
        // payload that needs them, unless someone else is already playing this exact request and
        // will raise them for us. That is the whole point of the external-execution-context path.
        if (requiresTimelineEvents && !hasExternalSkillExecutionContext)
        {
            reservation.Release();
            WarnMissingTimelineDriver(skillDef, request.DebugSource);
            return Rejected();
        }

        if (request.UseAnimationDriver && !request.AllowImmediateFallback)
        {
            reservation.Release();
            return Rejected();
        }

        if (!CanProceed(request))
        {
            reservation.Release();
            return Rejected();
        }

        request.OnStarted?.Invoke();

        // No wind-up on this path, so the cast point is now. CastReleased always means "reached the
        // cast point"; whether the payload then produced anything is reported separately.
        CastReleased?.Invoke(new ActiveSkillCastInfo(
            requestId,
            runtimeSkill,
            skillDef,
            skillUser,
            executionAnimDriver,
            skillDef.GetCastPointNormalized(),
            released: true,
            requiresTimelineEvents: false,
            request.DebugSource));

        if (!ExecuteReservedCast(
                request,
                requestId,
                runtimeSkill,
                reservation,
                skillUser,
                executionAnimDriver))
        {
            return Rejected();
        }

        return new SkillCastStartResult(SkillCastStartKind.ImmediateSuccess, requestId);
    }

    private bool TryResolveStartState(
        in SkillCastRequest request,
        out SkillInstance runtimeSkill,
        out ISkillUser skillUser,
        out SkillGemDefinition skillDef)
    {
        runtimeSkill = request.RuntimeSkill;
        skillUser = request.SkillUser;
        skillDef = runtimeSkill != null ? runtimeSkill.def : null;

        if (runtimeSkill == null || skillUser == null || skillDef == null || skillDef.payload == null)
            return false;

        if (!CanProceed(request))
            return false;

        return runtimeSkill.CanCast(skillUser, request.CostPolicy, out _);
    }

    /// <summary>
    /// Runs the payload for a cast that has reached its cast point, then settles its reservation:
    /// commit when the payload produced a gameplay effect, roll everything back when it did not.
    /// </summary>
    private bool ExecuteReservedCast(
        in SkillCastRequest request,
        int requestId,
        SkillInstance runtimeSkill,
        SkillCastReservation reservation,
        ISkillUser skillUser,
        CharacterAnimDriver executionAnimDriver)
    {
        if (runtimeSkill == null || reservation == null || reservation.IsSettled)
            return false;

        CharacterAnimBrain executionAnimBrain = executionAnimDriver != null
            ? executionAnimDriver.Brain
            : null;

        if (!runtimeSkill.ExecuteReserved(
                reservation,
                executionAnimBrain,
                requestId,
                out SkillExecutionResult result,
                request.PrimaryTarget))
        {
            reservation.Release();
            RaiseExecutionFailed(request, requestId, runtimeSkill, skillUser, executionAnimDriver, result);
            return false;
        }

        reservation.Commit();
        PlayCastCue(runtimeSkill, skillUser);
        return true;
    }

    private void RaiseExecutionFailed(
        in SkillCastRequest request,
        int requestId,
        SkillInstance runtimeSkill,
        ISkillUser skillUser,
        CharacterAnimDriver animationDriver,
        SkillExecutionResult result)
    {
        if (result.Success)
            return;

        SkillGemDefinition skillDef = runtimeSkill != null ? runtimeSkill.def : null;
        if (!string.IsNullOrEmpty(result.DebugMessage))
        {
            Debug.Log(
                $"[SkillCast] '{(skillDef != null ? skillDef.name : "<unknown>")}' produced no effect ({result.Reason}): {result.DebugMessage}",
                owner);
        }

        CastExecutionFailed?.Invoke(
            new ActiveSkillCastInfo(
                requestId,
                runtimeSkill,
                skillDef,
                skillUser,
                animationDriver,
                skillDef != null ? skillDef.GetCastPointNormalized() : 0f,
                released: true,
                requiresTimelineEvents: false,
                request.DebugSource),
            result);
    }

    private bool ReleasePendingCast(int requestId)
    {
        if (pendingCast == null ||
            pendingCast.RequestId != requestId ||
            pendingCast.Cancelled ||
            pendingCast.Released)
        {
            return false;
        }

        PendingCastContext context = pendingCast;
        pendingCast = null;
        Unsubscribe(context.AnimationDriver);

        if (!CanProceed(context.Request))
        {
            CancelPendingCastRequest(context, stopAnimation: false);
            SettleCancelledCast(context, SkillCastCancelReason.InvalidState);
            CastCancelled?.Invoke(context.ToInfo(), SkillCastCancelReason.InvalidState);
            return false;
        }

        context.Released = true;
        CastReleased?.Invoke(context.ToInfo());
        return ExecuteReservedCast(
            context.Request,
            context.RequestId,
            context.RuntimeSkill,
            context.Reservation,
            context.SkillUser,
            context.AnimationDriver);
    }

    private void CancelPendingCastRequest(PendingCastContext context, bool stopAnimation)
    {
        if (context == null)
            return;

        context.Cancelled = true;
        if (stopAnimation)
            context.AnimationDriver?.CancelSkillCastRequest(context.RequestId);
    }

    private void OnSkillCastMomentReached(int requestId)
    {
        ReleasePendingCast(requestId);
    }

    private void OnSkillCastInterrupted(int requestId)
    {
        if (pendingCast == null || pendingCast.RequestId != requestId)
            return;

        CancelPendingCast(SkillCastCancelReason.AnimationInterrupted);
    }

    private void Subscribe(CharacterAnimDriver animationDriver)
    {
        CharacterAnimBrain animationBrain = animationDriver != null ? animationDriver.Brain : null;
        if (animationBrain == null)
            return;

        animationBrain.SkillCastMomentReached += OnSkillCastMomentReached;
        animationBrain.SkillCastInterrupted += OnSkillCastInterrupted;
    }

    private void Unsubscribe(CharacterAnimDriver animationDriver)
    {
        CharacterAnimBrain animationBrain = animationDriver != null ? animationDriver.Brain : null;
        if (animationBrain == null)
            return;

        animationBrain.SkillCastMomentReached -= OnSkillCastMomentReached;
        animationBrain.SkillCastInterrupted -= OnSkillCastInterrupted;
    }

    private bool CanProceed(in SkillCastRequest request)
    {
        return request.CanProceed == null || request.CanProceed();
    }

    /// <summary>
    /// The one charge pool for this definition on this character. Created on demand and always
    /// non-null for a real definition, so a skill that has never been cast reads as full rather
    /// than as "unknown".
    /// </summary>
    public SkillChargeState GetOrCreateCharges(SkillGemDefinition skillDef)
    {
        if (skillDef == null)
            return null;

        if (!sharedCharges.TryGetValue(skillDef, out SkillChargeState charges))
        {
            charges = new SkillChargeState();
            sharedCharges[skillDef] = charges;
        }

        return charges;
    }

    /// <summary>Refills every pool. Used on load, where charges are deliberately not persisted.</summary>
    public void ResetAllChargesToFull()
    {
        foreach (KeyValuePair<SkillGemDefinition, SkillChargeState> pair in sharedCharges)
            pair.Value?.ResetToFull();
    }

    /// <summary>
    /// Rolls back a cast that never reached its cast point.
    ///
    /// Interrupted, stunned, and invalid-state casts cost nothing at all. A Blocked cast still
    /// burns its cooldown but keeps its energy - except for summon-style skills, which deploy
    /// something into the world: being blocked before the cast point means nothing was deployed,
    /// so that cast must be free.
    /// </summary>
    private void SettleCancelledCast(PendingCastContext context, SkillCastCancelReason reason)
    {
        SkillCastReservation reservation = context != null ? context.Reservation : null;
        if (reservation == null || reservation.IsSettled)
            return;

        // Defensive: past the cast point the execution path owns the settle.
        if (context.Released)
            return;

        bool burnsCooldown =
            reason == SkillCastCancelReason.Blocked &&
            context.Request.StampCooldown &&
            !IsSummonSkill(context.SkillDef);

        if (burnsCooldown)
            reservation.CommitChargeOnly();
        else
            reservation.Release();
    }

    private static bool IsSummonSkill(SkillGemDefinition skillDef)
    {
        return skillDef != null && (skillDef.tags & SkillTag.Minion) != 0;
    }

    private int ResolveRequestId(int requestedId)
    {
        if (requestedId > 0)
            return requestedId;

        if (nextCastRequestId == int.MaxValue)
            nextCastRequestId = 1;

        return nextCastRequestId++;
    }

    private bool HasActiveSkillExecutionContext(CharacterAnimDriver animationDriver, int requestId)
    {
        CharacterAnimBrain animationBrain = animationDriver != null ? animationDriver.Brain : null;
        return animationBrain != null &&
               requestId > 0 &&
               animationBrain.TryGetActiveSkillNormalizedTime(requestId, out _);
    }

    private void PlayCastCue(SkillInstance skill, ISkillUser skillUser)
    {
        if (skill == null || skill.def == null || skill.def.castCue == null)
            return;

        Transform castOrigin = skillUser != null && skillUser.CastOrigin != null
            ? skillUser.CastOrigin
            : owner != null
                ? owner.transform
                : null;

        if (castOrigin == null)
            return;

        AudioService.Instance.PlayAttached(skill.def.castCue, castOrigin, Vector3.zero);
    }

    private void WarnMissingTimelineDriver(SkillGemDefinition skillDef, string debugSource)
    {
        string skillLabel = skillDef != null ? skillDef.name : "<unknown>";
        string sourceLabel = string.IsNullOrWhiteSpace(debugSource) ? string.Empty : $" ({debugSource})";
        Debug.LogWarning(
            $"Skill '{skillLabel}' requires Animancer skill timeline events, but no active skill animation driver was available{sourceLabel}.",
            owner);
    }

    private static SkillCastStartResult Rejected()
    {
        return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);
    }
}
