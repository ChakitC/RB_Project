using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.Serialization;

public class CharacterSkillManager : MonoBehaviour, IGameSaveAble, ISaveOrder
{
    static readonly SkillSlot[] EmptyAutonomousSlots = Array.Empty<SkillSlot>();
    static readonly HelperProcSlot[] EmptyHelperProcSlots = Array.Empty<HelperProcSlot>();
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

    readonly List<SkillSlot> resolvedCommandSlots = new();
    readonly List<ResolvedCommandSlotState> resolvedCommandSlotStates = new();

    public event Action<ActiveSkillCastInfo> CastStarted;
    public event Action<ActiveSkillCastInfo> CastReleased;
    public event Action<ActiveSkillCastInfo, SkillCastCancelReason> CastCancelled;

    /// <summary>Raised when a payload ran but produced nothing, so the cast cost nothing.</summary>
    public event Action<ActiveSkillCastInfo, SkillExecutionResult> CastExecutionFailed;
    public event Action PassiveLoadoutChanged;

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
    public bool HasConfiguredPlayerCommandSkill => IsSkillEntryConfigured(playerCommandSkill);
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

        if (IsSkillStartBlockedByAnimation())
        {
            pendingSlot = null;
            return;
        }

        pendingSlot = null;
        if (resolvedCommandSlotStates.Count == 0)
            return;

        for (int i = 0; i < resolvedCommandSlotStates.Count; i++)
        {
            ResolvedCommandSlotState state = resolvedCommandSlotStates[i];
            if (state == null || state.IsPassive || state.slot == null)
                continue;

            if (Input.GetKeyDown(state.slot.hotkey))
                TryBeginCast(state.slot);
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
        bool stampCooldown = true)
    {
        CacheReferences();
        return TryBeginEntryCast(entry, debugSource, requiredTimelineEvent, usePlanarRootMotion, ignoreResourceCosts, stampCooldown);
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
        bool stampCooldown = true)
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
            animationDriver: animDriver,
            canProceed: () => IsSkillEntryCastStillValid(entry, runtimeSkill),
            onStarted: StopWeaponActivityForSkillCast,
            useAnimationDriver: true,
            allowImmediateFallback: true,
            requiredTimelineEvent: requiredTimelineEvent,
            usePlanarRootMotion: usePlanarRootMotion,
            ignoreResourceCosts: ignoreResourceCosts,
            stampCooldown: stampCooldown,
            debugSource: debugSource));
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

        return CreateRuntimeSkill(asset);
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
            if (ctx == null)
                ctx = GetComponentInParent<CharacteContext>();
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
            RebuildResolvedCommandSlots();
            return;
        }

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
    public readonly bool IgnoreResourceCosts;
    public readonly bool StampCooldown;
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
        string debugSource = null)
    {
        RuntimeSkill = runtimeSkill;
        SkillUser = skillUser;
        AnimationDriver = animationDriver;
        CanProceed = canProceed;
        OnStarted = onStarted;
        RequestedId = requestedId;
        IgnoreResourceCosts = ignoreResourceCosts;
        StampCooldown = stampCooldown;
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
        StampCooldownForBlockedPreCast(context, reason);
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

        request.OnStarted?.Invoke();

        if (request.UseAnimationDriver && executionAnimDriver != null)
        {
            var context = new PendingCastContext
            {
                Request = request,
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
            executionAnimDriver);

        if (!released)
            return Rejected();

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

        return runtimeSkill.CanCast(skillUser, out _);
    }

    private bool TryReleaseCast(
        in SkillCastRequest request,
        int requestId,
        SkillInstance runtimeSkill,
        ISkillUser skillUser,
        CharacterAnimDriver executionAnimDriver)
    {
        if (runtimeSkill == null || skillUser == null || runtimeSkill.def == null || runtimeSkill.def.payload == null)
            return false;

        if (!CanProceed(request))
            return false;

        CharacterAnimBrain executionAnimBrain = executionAnimDriver != null
            ? executionAnimDriver.Brain
            : null;

        if (request.IgnoreResourceCosts)
        {
            bool executedIgnoringCosts = runtimeSkill.TryCastIgnoringResourceCosts(
                skillUser,
                executionAnimBrain,
                requestId,
                request.StampCooldown,
                out SkillExecutionResult freeCastResult);

            if (executedIgnoringCosts)
                PlayCastCue(runtimeSkill, skillUser);
            else
                RaiseExecutionFailed(request, requestId, runtimeSkill, skillUser, executionAnimDriver, freeCastResult);

            return executedIgnoringCosts;
        }

        // CanCast already reads the shared pool this instance is bound to, so there is no second
        // readiness check and no second deduction — SkillInstance is the only place a charge is
        // ever spent.
        if (!runtimeSkill.CanCast(skillUser, out _))
            return false;

        if (!runtimeSkill.Cast(skillUser, executionAnimBrain, requestId, out SkillExecutionResult castResult))
        {
            RaiseExecutionFailed(request, requestId, runtimeSkill, skillUser, executionAnimDriver, castResult);
            return false;
        }

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

    private void StampCooldownForBlockedPreCast(PendingCastContext context, SkillCastCancelReason reason)
    {
        if (reason != SkillCastCancelReason.Blocked)
            return;

        if (context == null ||
            context.Released ||                 // defensive: block path already guards this
            !context.Request.StampCooldown ||   // respects stampCooldown:false interruption paths
            context.RuntimeSkill == null ||
            context.SkillUser == null)
        {
            return;
        }

        // Summon-style skills deploy something into the world. Being interrupted before the cast
        // point means nothing was deployed, so the cast must cost nothing at all. Every other
        // skill keeps the existing rule where a blocked pre-cast still burns its cooldown.
        if (IsSummonSkill(context.SkillDef))
            return;

        // Consumes the shared pool through the instance — the single deduction site.
        context.RuntimeSkill.TryStampCooldownOnly(context.SkillUser, out _);
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
