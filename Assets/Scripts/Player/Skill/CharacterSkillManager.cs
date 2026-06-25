using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.Serialization;

public class CharacterSkillManager : MonoBehaviour, IGameSaveAble, ISaveOrder
{
    static readonly SkillSlot[] EmptyAutonomousSlots = Array.Empty<SkillSlot>();
    static readonly HelperProcSlot[] EmptyHelperProcSlots = Array.Empty<HelperProcSlot>();
    static readonly PassiveSkillSlot[] EmptyPassiveSlots = Array.Empty<PassiveSkillSlot>();
    static readonly CharacterSkillLoadoutOption[] EmptySkillOptions = Array.Empty<CharacterSkillLoadoutOption>();

    sealed class ResolvedCommandSlotState
    {
        public SkillSlot slot;
        public CharacterSkillLoadoutSlot statsSlot;
        public CharacterSkillLoadoutOption selectedOption;
        public int selectedOptionIndex = -1;
        public string slotId;
        public bool usesPrefabOverride;
    }

    private CharacteContext ctx;
    private CharacterAnimBrain animBrain;
    private WeaponSystem weaponSystem;
    private SkillSlot pendingSlot;
    private SkillCastOrchestrator castOrchestrator;
    private CharacterStats observedBaseStats;
    private int observedAutonomousSlotCount = -1;
    private bool commandSlotsBuilt;

    readonly List<SkillSlot> resolvedCommandSlots = new();
    readonly List<ResolvedCommandSlotState> resolvedCommandSlotStates = new();

    public event Action<ActiveSkillCastInfo> CastStarted;
    public event Action<ActiveSkillCastInfo> CastReleased;
    public event Action<ActiveSkillCastInfo, SkillCastCancelReason> CastCancelled;

    [Header("Autonomous Loadout")]
    public ISkillUser skillUser;
    [FormerlySerializedAs("slots")]
    [SerializeField] private SkillSlot[] autonomousSlots;

    [Header("Player Command")]
    [SerializeField] private CharacterSkillEntry playerCommandSkill;

    [Header("Chain Attack")]
    [SerializeField] private CharacterSkillEntry chainAttackSkill;

    [Header("Helper Proc Loadout")]
    [SerializeField] private HelperProcSlot[] helperProcLoadout = EmptyHelperProcSlots;

    [Header("Passive Loadout")]
    [FormerlySerializedAs("passiveSlots")]
    [SerializeField] private PassiveSkillSlot[] passiveSlots = EmptyPassiveSlots;

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
    public CharacterSkillEntry PlayerCommandSkill => playerCommandSkill;
    public CharacterSkillEntry ChainAttackSkill => chainAttackSkill;
    public IReadOnlyList<HelperProcSlot> HelperProcSlots => helperProcLoadout ?? EmptyHelperProcSlots;
    public IReadOnlyList<PassiveSkillSlot> PassiveSlots => passiveSlots ?? EmptyPassiveSlots;
    public bool HasConfiguredPlayerCommandSkill => IsSkillEntryConfigured(playerCommandSkill);
    public bool HasConfiguredChainAttackSkill => IsSkillEntryConfigured(chainAttackSkill);
    public bool HasConfiguredPassiveSlots => CountConfiguredPassiveSlots() > 0;

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

        if (ctx != null && ctx.HealthSystem != null)
        {
            ctx.HealthSystem.CharacterDown += OnCharacterDown;
            ctx.HealthSystem.CharacterDead += OnCharacterDead;
        }
    }

    private void OnDisable()
    {
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
        RebuildResolvedCommandSlots();
    }

    private void Update()
    {
        RefreshResolvedCommandSlotsIfNeeded();
        castOrchestrator?.Tick();
        if (castOrchestrator != null && castOrchestrator.HasPendingCast)
            return;

        if (IsSkillStartBlockedByAnimation())
        {
            pendingSlot = null;
            return;
        }

        pendingSlot = null;
        if (resolvedCommandSlots.Count == 0)
            return;

        foreach (SkillSlot slot in resolvedCommandSlots)
        {
            if (slot == null)
                continue;

            if (Input.GetKeyDown(slot.hotkey))
                TryBeginCast(slot);
        }
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

    public bool CanStartPlayerCommandSkill()
    {
        CacheReferences();
        EnsureRuntimeSkill(playerCommandSkill);
        return playerCommandSkill != null &&
               playerCommandSkill.runtimeSkill != null &&
               skillUser != null &&
               !CutsceneDirector.IsCinematicPlaying &&
               !IsSkillStartBlockedByAnimation() &&
               !IsSkillUseBlocked() &&
               playerCommandSkill.runtimeSkill.CanCast(skillUser);
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
        return TryBeginEntryCast(playerCommandSkill, "player-command");
    }

    public bool CanStartExternalSkill(CharacterSkillEntry entry)
    {
        CacheReferences();
        EnsureRuntimeSkill(entry);
        return entry != null &&
               entry.runtimeSkill != null &&
               skillUser != null &&
               !CutsceneDirector.IsCinematicPlaying &&
               !IsSkillStartBlockedByAnimation() &&
               !IsSkillUseBlocked() &&
               entry.runtimeSkill.CanCast(skillUser);
    }

    public SkillCastStartResult TryStartExternalSkill(
        CharacterSkillEntry entry,
        string debugSource,
        CombatTimelineEventName requiredTimelineEvent = CombatTimelineEventName.None)
    {
        CacheReferences();
        return TryBeginEntryCast(entry, debugSource, requiredTimelineEvent);
    }

    public bool TryGetChainAttackRuntimeSkill(out SkillInstance runtimeSkill)
    {
        CacheReferences();
        EnsureRuntimeSkill(chainAttackSkill);

        runtimeSkill = chainAttackSkill != null ? chainAttackSkill.runtimeSkill : null;
        return runtimeSkill != null && runtimeSkill.def != null;
    }

    public bool TryGetChainAttackSkillDefinition(out SkillGemDefinition skillDef, out int skillLevel, out SkillInstance runtimeSkill)
    {
        skillDef = null;
        skillLevel = 1;

        if (!TryGetChainAttackRuntimeSkill(out runtimeSkill))
            return false;

        skillDef = runtimeSkill.def;
        skillLevel = Mathf.Max(1, runtimeSkill.level);
        return true;
    }

    public void AppendConfiguredHelperChainDefinitions(List<SkillHelperDef> buffer, HashSet<SkillHelperDef> dedupe = null)
    {
        if (buffer == null || helperProcLoadout == null)
            return;

        for (int i = 0; i < helperProcLoadout.Length; i++)
        {
            HelperProcSlot slot = helperProcLoadout[i];
            SkillHelperDef definition = slot?.ResolveHelperProc();
            if (definition == null)
                continue;

            if (dedupe != null && !dedupe.Add(definition))
                continue;

            buffer.Add(definition);
        }
    }

    public void AppendConfiguredPassiveDefinitions(List<PassiveDefinition> buffer)
    {
        if (buffer == null || passiveSlots == null)
            return;

        for (int i = 0; i < passiveSlots.Length; i++)
        {
            PassiveDefinition definition = passiveSlots[i]?.passiveAsset;
            if (definition != null)
                buffer.Add(definition);
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

    public void AssignSkillToSlot(int index, SkillGemDefinition asset, int level = 1)
    {
        if (!TryGetCommandSlot(index, out SkillSlot slot))
            return;

        CancelPendingSlotIfNeeded(slot);

        slot.skillAsset = asset;
        slot.skillLevel = level;
        slot.runtimeSkill = BuildRuntimeSkill(slot, asset, level);
        if (TryGetCommandSlotState(index, out ResolvedCommandSlotState state))
        {
            state.selectedOption = null;
            state.selectedOptionIndex = -1;
        }
    }

    private SkillCastStartResult TryBeginCast(SkillSlot slot)
    {
        CacheReferences();
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
            animationDriver: animBrain,
            canProceed: () => IsSlotCastStillValid(slot, runtimeSkill),
            onStarted: StopWeaponActivityForSkillCast,
            useAnimationDriver: true,
            allowImmediateFallback: true,
            debugSource: BuildSlotDebugSource(slot)));

        pendingSlot = result.Kind == SkillCastStartKind.WaitingForAnimation
            ? slot
            : null;

        return result;
    }

    private SkillCastStartResult TryBeginEntryCast(
        CharacterSkillEntry entry,
        string debugSource,
        CombatTimelineEventName requiredTimelineEvent = CombatTimelineEventName.None)
    {
        CacheReferences();
        EnsureRuntimeSkill(entry);

        if (CutsceneDirector.IsCinematicPlaying)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        if (entry == null || entry.runtimeSkill == null || skillUser == null)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        if (IsSkillStartBlockedByAnimation())
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        EnsureCastOrchestrator();
        SkillInstance runtimeSkill = entry.runtimeSkill;
        return castOrchestrator.TryStartCast(new SkillCastRequest(
            runtimeSkill,
            skillUser,
            animationDriver: animBrain,
            canProceed: () => IsSkillEntryCastStillValid(entry, runtimeSkill),
            onStarted: StopWeaponActivityForSkillCast,
            useAnimationDriver: true,
            allowImmediateFallback: true,
            requiredTimelineEvent: requiredTimelineEvent,
            debugSource: debugSource));
    }

    private SkillInstance BuildRuntimeSkill(SkillSlot slot, SkillGemDefinition asset, int level)
    {
        if (slot == null || asset == null)
            return null;

        int resolvedSkillLevel = asset.ClampLevel(level);
        slot.skillLevel = resolvedSkillLevel;

        return CreateRuntimeSkill(asset, slot.supportAssets, resolvedSkillLevel);
    }

    private SkillInstance BuildRuntimeSkill(CharacterSkillEntry entry, SkillGemDefinition asset, int level)
    {
        if (entry == null || asset == null)
            return null;

        int resolvedSkillLevel = asset.ClampLevel(level);
        entry.skillLevel = resolvedSkillLevel;

        return CreateRuntimeSkill(asset, entry.supportAssets, resolvedSkillLevel);
    }

    private static SkillInstance CreateRuntimeSkill(
        SkillGemDefinition asset,
        SupportGemDefinition[] supportAssets,
        int resolvedSkillLevel)
    {
        var instance = new SkillInstance
        {
            def = asset,
            level = resolvedSkillLevel,
        };

        if (supportAssets == null)
            return instance;

        foreach (SupportGemDefinition supportAsset in supportAssets)
        {
            if (supportAsset == null)
                continue;

            instance.supports.Add(new SupportInstance
            {
                def = supportAsset,
                level = Mathf.Clamp(resolvedSkillLevel, 1, Mathf.Max(1, supportAsset.maxLevel)),
            });
        }

        return instance;
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
        EnsureRuntimeSkill(playerCommandSkill);
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

        if (slot.runtimeSkill != null &&
            slot.runtimeSkill.def == slot.skillAsset &&
            slot.runtimeSkill.level == slot.skillLevel)
        {
            return;
        }

        slot.runtimeSkill = BuildRuntimeSkill(slot, slot.skillAsset, slot.skillLevel);
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

        if (entry.runtimeSkill != null &&
            entry.runtimeSkill.def == entry.skillAsset &&
            entry.runtimeSkill.level == entry.skillLevel)
        {
            return;
        }

        entry.runtimeSkill = BuildRuntimeSkill(entry, entry.skillAsset, entry.skillLevel);
    }

    private void CacheReferences()
    {
        if (ctx == null)
        {
            TryGetComponent(out ctx);
            if (ctx == null)
                ctx = GetComponentInParent<CharacteContext>();
        }

        ctx?.ResolveReferences();

        skillUser = GetComponent<ISkillUser>();
        if (skillUser == null && ctx != null && ctx.EnegySystem != null)
            skillUser = ctx.EnegySystem;

        animBrain = ResolveAnimBrainReference();
        weaponSystem = ctx != null ? ctx.WeaponSystem : null;
        if (weaponSystem == null)
            weaponSystem = GetComponent<WeaponSystem>();
        if (weaponSystem == null && ctx != null)
            weaponSystem = ctx.GetComponentInChildren<WeaponSystem>(true);

        if (ctx == null)
            return;

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

    private void RefreshResolvedCommandSlotsIfNeeded()
    {
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
        ApplySkillOptionToSlot(state.slot, state.statsSlot, option);
    }

    private static bool IsCurrentSelectedSkillOption(
        ResolvedCommandSlotState state,
        int optionIndex,
        CharacterSkillLoadoutOption option)
    {
        return state != null &&
               state.slot != null &&
               state.selectedOptionIndex == optionIndex &&
               ReferenceEquals(state.selectedOption, option) &&
               option != null &&
               option.skillAsset != null &&
               state.slot.skillAsset == option.skillAsset &&
               state.slot.runtimeSkill != null &&
               state.slot.runtimeSkill.def == option.skillAsset;
    }

    private void ApplySkillOptionToSlot(
        SkillSlot slot,
        CharacterSkillLoadoutSlot statsSlot,
        CharacterSkillLoadoutOption option)
    {
        if (slot == null)
            return;

        slot.hotkey = statsSlot != null ? statsSlot.hotkey : slot.hotkey;

        if (option == null || option.skillAsset == null)
        {
            ClearRuntimeSlot(slot);
            return;
        }

        slot.skillAsset = option.skillAsset;
        slot.skillLevel = Mathf.Max(1, option.skillLevel);
        slot.supportAssets = option.supportAssets;
        slot.maxSupportSlots = Mathf.Max(0, option.maxSupportSlots);
        slot.runtimeSkill = BuildRuntimeSkill(slot, slot.skillAsset, slot.skillLevel);
    }

    private static void ClearRuntimeSlot(SkillSlot slot)
    {
        if (slot == null)
            return;

        slot.skillAsset = null;
        slot.supportAssets = null;
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
        if (slot != null && !string.IsNullOrWhiteSpace(slot.ResolvedSlotId))
            return slot.ResolvedSlotId;

        return $"slot:{Mathf.Max(0, slotIndex)}";
    }

    private static string ResolveOptionId(CharacterSkillLoadoutOption option, int optionIndex)
    {
        if (option != null && !string.IsNullOrWhiteSpace(option.ResolvedOptionId))
            return option.ResolvedOptionId;

        return $"option:{Mathf.Max(0, optionIndex)}";
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

    private int CountConfiguredPassiveSlots()
    {
        if (passiveSlots == null)
            return 0;

        int count = 0;
        for (int i = 0; i < passiveSlots.Length; i++)
        {
            if (passiveSlots[i] != null && passiveSlots[i].passiveAsset != null)
                count++;
        }

        return count;
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
    public readonly CharacterAnimBrain AnimationDriver;
    public readonly Func<bool> CanProceed;
    public readonly Action OnStarted;
    public readonly int RequestedId;
    public readonly bool IgnoreResourceCosts;
    public readonly bool UseAnimationDriver;
    public readonly bool AllowImmediateFallback;
    public readonly CombatTimelineEventName RequiredTimelineEvent;
    public readonly string DebugSource;

    public SkillCastRequest(
        SkillInstance runtimeSkill,
        ISkillUser skillUser,
        CharacterAnimBrain animationDriver = null,
        Func<bool> canProceed = null,
        Action onStarted = null,
        int requestedId = 0,
        bool ignoreResourceCosts = false,
        bool useAnimationDriver = true,
        bool allowImmediateFallback = true,
        CombatTimelineEventName requiredTimelineEvent = CombatTimelineEventName.None,
        string debugSource = null)
    {
        RuntimeSkill = runtimeSkill;
        SkillUser = skillUser;
        AnimationDriver = animationDriver;
        CanProceed = canProceed;
        OnStarted = onStarted;
        RequestedId = requestedId;
        IgnoreResourceCosts = ignoreResourceCosts;
        UseAnimationDriver = useAnimationDriver;
        AllowImmediateFallback = allowImmediateFallback;
        RequiredTimelineEvent = requiredTimelineEvent;
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
    public readonly CharacterAnimBrain AnimationDriver;
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
        CharacterAnimBrain animationDriver,
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
        public SkillGemDefinition SkillDef;
        public ISkillUser SkillUser;
        public CharacterAnimBrain AnimationDriver;
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
    private readonly Dictionary<SkillGemDefinition, float> sharedCooldownReadyAt = new Dictionary<SkillGemDefinition, float>();

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
        CharacterAnimBrain executionAnimBrain = request.AnimationDriver;
        bool hasExternalSkillExecutionContext = HasActiveSkillExecutionContext(executionAnimBrain, requestId);
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

        request.OnStarted?.Invoke();

        if (request.UseAnimationDriver && executionAnimBrain != null)
        {
            var context = new PendingCastContext
            {
                Request = request,
                RuntimeSkill = runtimeSkill,
                SkillDef = skillDef,
                SkillUser = skillUser,
                AnimationDriver = executionAnimBrain,
                RequestId = requestId,
                CastPointNormalized = skillDef.GetCastPointNormalized(),
                RequiresTimelineEvents = requiresTimelineEvents,
            };

            skillDef.CollectTimelineEventNames(context.TimelineEventNames);
            CombatTimelineEventNames.AddUnique(
                context.TimelineEventNames,
                request.RequiredTimelineEvent);

            bool started = executionAnimBrain.TryPlaySkill(
                context.RequestId,
                context.SkillDef,
                context.CastPointNormalized,
                context.TimelineEventNames);

            if (started)
            {
                Subscribe(context.AnimationDriver);
                pendingCast = context;
                CastStarted?.Invoke(context.ToInfo());
                return new SkillCastStartResult(SkillCastStartKind.WaitingForAnimation, context.RequestId);
            }
        }

        if (requiresTimelineEvents)
        {
            WarnMissingTimelineDriver(skillDef, request.DebugSource);
            return Rejected();
        }

        if (request.UseAnimationDriver && !request.AllowImmediateFallback)
            return Rejected();

        bool released = TryReleaseCast(
            request,
            requestId,
            runtimeSkill,
            skillUser,
            executionAnimBrain);

        if (!released)
            return Rejected();

        CastReleased?.Invoke(new ActiveSkillCastInfo(
            requestId,
            runtimeSkill,
            skillDef,
            skillUser,
            executionAnimBrain,
            skillDef.GetCastPointNormalized(),
            released: true,
            requiresTimelineEvents: false,
            request.DebugSource));

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

        if (request.IgnoreResourceCosts)
            return true;

        if (!runtimeSkill.CanCast(skillUser, out FinalSkillStats castStats))
            return false;

        return IsSharedCooldownReady(runtimeSkill, castStats);
    }

    private bool TryReleaseCast(
        in SkillCastRequest request,
        int requestId,
        SkillInstance runtimeSkill,
        ISkillUser skillUser,
        CharacterAnimBrain executionAnimBrain)
    {
        if (runtimeSkill == null || skillUser == null || runtimeSkill.def == null || runtimeSkill.def.payload == null)
            return false;

        if (!CanProceed(request))
            return false;

        if (request.IgnoreResourceCosts)
        {
            bool executedIgnoringCosts = runtimeSkill.TryCastIgnoringResourceCosts(
                skillUser,
                executionAnimBrain,
                requestId);

            if (executedIgnoringCosts)
                PlayCastCue(runtimeSkill, skillUser);

            return executedIgnoringCosts;
        }

        if (!runtimeSkill.CanCast(skillUser, out FinalSkillStats castStats))
            return false;

        if (!IsSharedCooldownReady(runtimeSkill, castStats))
            return false;

        StampSharedCooldown(runtimeSkill, castStats);
        runtimeSkill.Cast(skillUser, executionAnimBrain, requestId);
        PlayCastCue(runtimeSkill, skillUser);
        return true;
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
            CastCancelled?.Invoke(context.ToInfo(), SkillCastCancelReason.InvalidState);
            return false;
        }

        context.Released = true;
        CastReleased?.Invoke(context.ToInfo());
        return TryReleaseCast(
            context.Request,
            context.RequestId,
            context.RuntimeSkill,
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

    private void Subscribe(CharacterAnimBrain animationDriver)
    {
        if (animationDriver == null)
            return;

        animationDriver.SkillCastMomentReached += OnSkillCastMomentReached;
        animationDriver.SkillCastInterrupted += OnSkillCastInterrupted;
    }

    private void Unsubscribe(CharacterAnimBrain animationDriver)
    {
        if (animationDriver == null)
            return;

        animationDriver.SkillCastMomentReached -= OnSkillCastMomentReached;
        animationDriver.SkillCastInterrupted -= OnSkillCastInterrupted;
    }

    private bool CanProceed(in SkillCastRequest request)
    {
        return request.CanProceed == null || request.CanProceed();
    }

    private bool IsSharedCooldownReady(SkillInstance skill, FinalSkillStats stats)
    {
        if (skill == null || skill.def == null)
            return true;

        if (!sharedCooldownReadyAt.TryGetValue(skill.def, out float readyAt))
            return true;

        if (Time.time >= readyAt)
        {
            sharedCooldownReadyAt.Remove(skill.def);
            return true;
        }

        return false;
    }

    private void StampSharedCooldown(SkillInstance skill, FinalSkillStats stats)
    {
        if (skill == null || skill.def == null)
            return;

        float cooldown = stats != null ? Mathf.Max(0f, stats.cooldown) : 0f;
        if (cooldown <= 0f)
        {
            sharedCooldownReadyAt.Remove(skill.def);
            return;
        }

        sharedCooldownReadyAt[skill.def] = Time.time + cooldown;
    }

    private int ResolveRequestId(int requestedId)
    {
        if (requestedId > 0)
            return requestedId;

        if (nextCastRequestId == int.MaxValue)
            nextCastRequestId = 1;

        return nextCastRequestId++;
    }

    private bool HasActiveSkillExecutionContext(CharacterAnimBrain animationDriver, int requestId)
    {
        return animationDriver != null &&
               requestId > 0 &&
               animationDriver.TryGetActiveSkillNormalizedTime(requestId, out _);
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
