using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class CharacterSkillManager : MonoBehaviour, IGameSaveAble, ISaveOrder
{
    readonly SkillChargeRegistry chargeRegistry = new();
    CharacterSkillLoadoutRuntime loadout;

    CharacterSkillLoadoutRuntime Loadout
    {
        get
        {
            if (loadout == null)
            {
                loadout = new CharacterSkillLoadoutRuntime(chargeRegistry,
                    LoadSavedSkillSelections, PersistSkillSelection, CharacterSkillLoadoutAccess.CanChange);
                loadout.SlotInvalidating += CancelPendingSlotIfNeeded;
                loadout.RuntimeSkillInvalidating += CancelActiveCastFor;
                loadout.PassiveLoadoutChanged += () => PassiveLoadoutChanged?.Invoke();
                loadout.HelperProcLoadoutChanged += () => HelperProcLoadoutChanged?.Invoke();
            }
            loadout.SetInputs(ctx != null ? ctx.baseStats : null,
                ctx != null ? ctx.TargetIdentity : default, autonomousSlots, activeSkillProgress);
            return loadout;
        }
    }

    private CharacteContext ctx;
    private CharacterActiveSkillProgress activeSkillProgress;
    private CharacterAnimBrain animBrain;
    private CharacterAnimDriver animDriver;
    private WeaponSystem weaponSystem;
    private SkillSlot pendingSlot;
    private SkillCastOrchestrator castOrchestrator;

    public event Action<ActiveSkillCastInfo> CastStarted;
    public event Action<ActiveSkillCastInfo> CastReleased;
    public event Action<ActiveSkillCastInfo> CastCommitted;
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

    public int LoadOrder => -90;
    public IReadOnlyList<SkillSlot> AutonomousSlots
    {
        get { RefreshResolvedCommandSlotsIfNeeded(); return Loadout.CommandSlots; }
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
            return IsSkillEntryConfigured(Loadout.HelperCommandSkillEntry) ? Loadout.HelperCommandSkillEntry : null;
        }
    }

    public CharacterSkillEntry ChainAttackSkill => chainAttackSkill;

    public bool HasConfiguredPlayerCommandSkill
    {
        get
        {
            CacheReferences();
            return IsSkillEntryConfigured(Loadout.HelperCommandSkillEntry);
        }
    }

    public bool HasConfiguredChainAttackSkill => IsSkillEntryConfigured(chainAttackSkill);

    public bool HasConfiguredPassiveSlots
    {
        get { CacheReferences(); return Loadout.HasConfiguredPassiveSlots; }
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

        if (!TryResolveCastSlotIndex(slotIndex, out slotIndex))
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        if (!TryGetCommandSlot(slotIndex, out SkillSlot slot))
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        return TryBeginCast(slot);
    }

    public bool HasConfiguredCommandSlot(int slotIndex)
    {
        if (!TryResolveCastSlotIndex(slotIndex, out slotIndex))
            return false;

        return TryGetCommandSlot(slotIndex, out SkillSlot slot) &&
               slot != null &&
               slot.skillAsset != null;
    }

    public bool CanStartCastSlot(int slotIndex)
    {
        CacheReferences();

        if (!TryResolveCastSlotIndex(slotIndex, out slotIndex))
            return false;

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

        if (!TryResolveCastSlotIndex(slotIndex, out slotIndex))
            return false;

        if (!TryGetCommandSlot(slotIndex, out SkillSlot slot) || skillUser == null)
            return false;

        EnsureCommandRuntimeSkill(slot);
        return slot.runtimeSkill != null &&
               slot.runtimeSkill.TryGetChargeStatus(skillUser, out status);
    }

    public bool CanStartPlayerCommandSkill()
    {
        CacheReferences();
        return Loadout.HelperCommandSkillEntry.runtimeSkill != null &&
               skillUser != null &&
               !CutsceneDirector.IsCinematicPlaying &&
               !IsSkillStartBlockedByAnimation() &&
               !IsSkillUseBlocked() &&
               Loadout.HelperCommandSkillEntry.runtimeSkill.CanCast(skillUser);
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
            Loadout.HelperCommandSkillEntry,
            "player-command",
            usePlanarRootMotion: true);
    }

    public SkillCastStartResult TryStartDirectedPartyCommand(
        int slotIndex, SkillTargetHandle target, Func<bool> canContinue,
        SkillGemDefinition explicitSkill = null, bool useCommandSkill = true)
    {
        CacheReferences();
        if (explicitSkill != null || (useCommandSkill && HasConfiguredPlayerCommandSkill))
        {
            CharacterSkillEntry entry = explicitSkill != null
                ? GetOrCreateExternalEntry(explicitSkill) : Loadout.HelperCommandSkillEntry;
            return TryBeginEntryCast(entry, "player-directed-command",
                usePlanarRootMotion: false, primaryTarget: target,
                additionalCanProceed: canContinue);
        }

        if (!TryResolveCastSlotIndex(slotIndex, out slotIndex) ||
            !TryGetCommandSlot(slotIndex, out SkillSlot slot))
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        return TryBeginCast(slot, target, canContinue);
    }

    public bool CanStartExternalSkill(CharacterSkillEntry entry, bool ignoreResourceCosts = false)
    {
        CacheReferences();
        EnsureRuntimeSkill(entry);
        if (entry == null || entry.runtimeSkill == null || entry.runtimeSkill.def == null || entry.runtimeSkill.def.IsCombo || skillUser == null)
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
        int externalAnimationRequestId = 0,
        Action<ActiveSkillCastInfo> onCommitted = null,
        Action<ActiveSkillCastInfo, SkillExecutionResult> onExecutionFailed = null,
        Action<ActiveSkillCastInfo, SkillCastCancelReason> onCancelled = null,
        ulong combatChainId = 0,
        int combatDepth = 0,
        FollowUpExecutionProvenance followUpProvenance = default)
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
            externalAnimationRequestId,
            onCommitted,
            onExecutionFailed,
            onCancelled,
            combatChainId,
            combatDepth,
            followUpProvenance);
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
        int externalAnimationRequestId = 0,
        Action<ActiveSkillCastInfo> onCommitted = null,
        Action<ActiveSkillCastInfo, SkillExecutionResult> onExecutionFailed = null,
        Action<ActiveSkillCastInfo, SkillCastCancelReason> onCancelled = null,
        ulong combatChainId = 0,
        int combatDepth = 0,
        FollowUpExecutionProvenance followUpProvenance = default)
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
            externalAnimationRequestId: externalAnimationRequestId,
            onCommitted: onCommitted,
            onExecutionFailed: onExecutionFailed,
            onCancelled: onCancelled,
            combatChainId: combatChainId,
            combatDepth: combatDepth,
            followUpProvenance: followUpProvenance);
    }

    public bool TryGetPartyFollowUpChargeStatus(
        PartyFollowUpSkillDef combo,
        out SkillChargeStatus status)
    {
        status = default;
        return TryGetPartyFollowUpRuntimeEntry(combo, out CharacterSkillEntry entry) &&
               skillUser != null &&
               entry.runtimeSkill.TryGetChargeStatus(skillUser, out status);
    }

    public SkillCastStartResult TryStartPartyFollowUpSkill(
        PartyFollowUpSkillDef combo,
        string debugSource,
        SkillTargetHandle primaryTarget,
        Action<ActiveSkillCastInfo> onCommitted,
        Action<ActiveSkillCastInfo, SkillExecutionResult> onExecutionFailed,
        Action<ActiveSkillCastInfo, SkillCastCancelReason> onCancelled,
        ulong combatChainId,
        int combatDepth,
        FollowUpExecutionProvenance followUpProvenance,
        bool usePlanarRootMotion = false)
    {
        if (!TryGetPartyFollowUpRuntimeEntry(combo, out CharacterSkillEntry entry))
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        return TryStartExternalSkill(
            entry,
            debugSource,
            usePlanarRootMotion: usePlanarRootMotion,
            stampCooldown: true,
            primaryTarget: primaryTarget,
            costPolicy: SkillCastCostPolicy.IgnoreEnergyRespectCharge,
            onCommitted: onCommitted,
            onExecutionFailed: onExecutionFailed,
            onCancelled: onCancelled,
            combatChainId: combatChainId,
            combatDepth: combatDepth,
            followUpProvenance: followUpProvenance);
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

        if (!Loadout.TryGetSelectedHelperProcEntry(helperProc, out CharacterSkillEntry entry))
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

    private SkillCastStartResult TryBeginCast(SkillSlot slot,
        SkillTargetHandle primaryTarget = null, Func<bool> additionalCanProceed = null)
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
            canProceed: () => IsSlotCastStillValid(slot, runtimeSkill) &&
                (additionalCanProceed == null || additionalCanProceed()),
            onStarted: () =>
            {
                if (additionalCanProceed != null)
                    StopWeaponActivityForSkillCast();
                else
                    OnCommandSkillCastStarted(runtimeSkill);
            },
            useAnimationDriver: true,
            allowImmediateFallback: true,
            debugSource: BuildSlotDebugSource(slot),
            primaryTarget: primaryTarget,
            usePlanarRootMotion: false,
            facingSnapshot: CapturePlayerFacingSnapshot(runtimeSkill)));

        pendingSlot = result.Kind == SkillCastStartKind.WaitingForAnimation
            ? slot
            : null;

        return result;
    }

    private void OnCommandSkillCastStarted(SkillInstance runtimeSkill)
    {
        StopWeaponActivityForSkillCast();

        SkillGemDefinition skill = runtimeSkill?.def;
        if (skill == null ||
            skill.HelperFacingMode != SkillHelperFacingMode.FaceDetectedTargetOnCast ||
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

    internal SkillCastStartResult TryStartMeleeStep(SkillComboStep step, MeleeType meleeType)
    {
        CacheReferences();
        if (step.executionSkill == null || step.executionSkill.IsCombo || skillUser == null || animDriver == null ||
            ctx == null || ctx.stateHub == null || !ctx.stateHub.CanStartMelee() ||
            CutsceneDirector.IsCinematicPlaying)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        CharacterSkillEntry entry = GetOrCreateExternalEntry(step.executionSkill);
        EnsureRuntimeSkill(entry);
        if (entry == null || entry.runtimeSkill == null)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);
        EnsureCastOrchestrator();
        return castOrchestrator.TryStartCast(new SkillCastRequest(
            entry.runtimeSkill, skillUser, animDriver,
            canProceed: () => isActiveAndEnabled && ctx != null && ctx.stateHub != null && ctx.stateHub.CanStartMelee(),
            allowImmediateFallback: false, stampCooldown: false,
            costPolicy: SkillCastCostPolicy.IgnoreEnergyAndCharge,
            executionKind: SkillExecutionKind.BasicMelee, meleeType: meleeType,
            meleeChainWindow: step.chainWindowN, debugSource: "BasicMelee"));
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
        int externalAnimationRequestId = 0,
        Action<ActiveSkillCastInfo> onCommitted = null,
        Action<ActiveSkillCastInfo, SkillExecutionResult> onExecutionFailed = null,
        Action<ActiveSkillCastInfo, SkillCastCancelReason> onCancelled = null,
        ulong combatChainId = 0,
        int combatDepth = 0,
        FollowUpExecutionProvenance followUpProvenance = default,
        Func<bool> additionalCanProceed = null)
    {
        CacheReferences();
        EnsureRuntimeSkill(entry);

        if (CutsceneDirector.IsCinematicPlaying)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        if (entry == null || entry.runtimeSkill == null || entry.runtimeSkill.def == null || entry.runtimeSkill.def.IsCombo || skillUser == null)
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
            canProceed: () => IsSkillEntryCastStillValid(entry, runtimeSkill) &&
                (additionalCanProceed == null || additionalCanProceed()),
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
            costPolicy: costPolicy,
            onCommitted: onCommitted,
            onExecutionFailed: onExecutionFailed,
            onCancelled: onCancelled,
            combatChainId: combatChainId,
            combatDepth: combatDepth,
            followUpProvenance: followUpProvenance,
            facingSnapshot: CapturePlayerFacingSnapshot(runtimeSkill, primaryTarget)));
    }

    SkillFacingSnapshot CapturePlayerFacingSnapshot(
        SkillInstance runtimeSkill,
        SkillTargetHandle preferredTarget = null)
    {
        if (ctx is not PlayerContext player ||
            runtimeSkill == null ||
            runtimeSkill.def == null ||
            !runtimeSkill.def.TryFindPayload(out PrefabHitboxSkillPayloadDef _))
        {
            return default;
        }

        return SkillFacingSnapshot.Capture(player, 6f, preferredTarget);
    }

    private void RebuildAllRuntimeSkills()
    {
        RebuildResolvedCommandSlots();
        RefreshHelperLoadout(force: true);
        EnsureRuntimeSkill(chainAttackSkill);
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
        Loadout.HandleActiveSkillTreeChanged(slotId, optionId);
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
        List<CharacterSkillSelectionSaveData> selections = progress != null ? progress.selectedSkillOptions : null;

        MapRunController run = FindFirstObjectByType<MapRunController>();
        return run != null && run.IsLoadoutLocked
            ? run.GetOrCaptureRunLoadout(characterId, selections)
            : selections;
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

    private void EnsureCastOrchestrator()
    {
        if (castOrchestrator != null)
            return;

        castOrchestrator = new SkillCastOrchestrator(this, chargeRegistry);
        castOrchestrator.CastStarted += OnCastStarted;
        castOrchestrator.CastReleased += OnCastReleased;
        castOrchestrator.CastCommitted += OnCastCommitted;
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

    private void OnCastCommitted(ActiveSkillCastInfo castInfo)
    {
        CastCommitted?.Invoke(castInfo);
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

    /// <summary>
    /// Skill currently assigned to a command slot, for HUD icons and tooltips. This is a plain
    /// read of the resolved loadout: it does not build a runtime skill or touch the charge pool,
    /// so a HUD can poll it alongside <see cref="TryGetSlotChargeStatus"/> without paying twice.
    /// </summary>
    public bool TryGetSlotSkillDefinition(int slotIndex, out SkillGemDefinition skillDef)
    {
        return Loadout.TryGetSlotSkillDefinition(slotIndex, out skillDef);
    }

    /// <summary>
    /// Passive assigned to a command slot, for HUD icons. Returns false for an active slot, so a
    /// HUD can ask this first and fall back to the charge readout only when it comes back empty.
    /// A passive slot has no runtime skill and no charge pool, which is why it can never answer
    /// <see cref="TryGetSlotChargeStatus"/>.
    /// </summary>
    public bool TryGetSlotPassiveDefinition(int slotIndex, out PassiveDefinition passiveDef)
    {
        return Loadout.TryGetSlotPassiveDefinition(slotIndex, out passiveDef);
    }

    public bool TrySelectSkillOption(int slotIndex, int optionIndex, bool persist = true)
    {
        CacheReferences();
        return Loadout.TrySelectSkillOption(slotIndex, optionIndex, persist);
    }

    public bool TrySelectSkillOption(string slotId, string optionId, bool persist = true)
    {
        CacheReferences();
        return Loadout.TrySelectSkillOption(slotId, optionId, persist);
    }

    public bool TryGetSkillOptions(int slotIndex, out IReadOnlyList<CharacterSkillLoadoutOption> options)
    {
        CacheReferences();
        return Loadout.TryGetSkillOptions(slotIndex, out options);
    }

    public bool TryGetSkillOptions(string slotId, out IReadOnlyList<CharacterSkillLoadoutOption> options)
    {
        CacheReferences();
        return Loadout.TryGetSkillOptions(slotId, out options);
    }

    public bool TryGetSelectedSkillOption(int slotIndex, out CharacterSkillLoadoutOption option)
    {
        CacheReferences();
        return Loadout.TryGetSelectedSkillOption(slotIndex, out option);
    }

    public bool TryGetSelectedSkillOption(string slotId, out CharacterSkillLoadoutOption option)
    {
        CacheReferences();
        return Loadout.TryGetSelectedSkillOption(slotId, out option);
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
        return Loadout.TrySelectLoadoutOption(slotId, optionId, persist);
    }

    /// <summary>Option id currently equipped in <paramref name="slotId"/>, across both roles.</summary>
    public bool TryGetSelectedLoadoutOptionId(string slotId, out string optionId)
    {
        CacheReferences();
        return Loadout.TryGetSelectedLoadoutOptionId(slotId, out optionId);
    }

    public bool TryGetPartyFollowUpRuntimeEntry(
        PartyFollowUpSkillDef combo,
        out CharacterSkillEntry entry)
    {
        CacheReferences();
        return Loadout.TryGetPartyFollowUpRuntimeEntry(combo, out entry);
    }

    /// <summary>Returns the live runtime entry for the currently selected proc variant.</summary>
    public bool TryGetHelperProcRuntimeSkill(SkillHelperDef helperProc, out SkillInstance runtimeSkill)
    {
        CacheReferences();
        return Loadout.TryGetHelperProcRuntimeSkill(helperProc, out runtimeSkill);
    }

    /// <summary>
    /// One entry per definition, kept for the lifetime of this component. The charge pool itself
    /// lives in the shared registry keyed by definition, so it would survive a rebuilt entry
    /// anyway - caching keeps allocation and cast diagnostics stable.
    /// </summary>
    private CharacterSkillEntry GetOrCreateExternalEntry(SkillGemDefinition skillDef)
    {
        return Loadout.GetOrCreateExternalEntry(skillDef);
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
        CacheReferences();
        Loadout.AppendConfiguredHelperChainDefinitions(buffer, dedupe);
    }

    public void AppendConfiguredPassiveDefinitions(List<EquippedPassive> buffer)
    {
        CacheReferences();
        Loadout.AppendConfiguredPassiveDefinitions(buffer);
    }

    public void ClearSlot(int index)
    {
        Loadout.ClearSlot(index);
    }

    public void AssignSkillToSlot(int index, SkillGemDefinition asset)
    {
        Loadout.AssignSkillToSlot(index, asset);
    }

    private bool TryGetCommandSlot(int slotIndex, out SkillSlot slot)
    {
        return Loadout.TryGetCommandSlot(slotIndex, out slot);
    }

    /// <summary>
    /// Player, AI, party-command, and HUD callers retain their 0/1 input contract while migrated
    /// character assets resolve those inputs by slot meaning. A third cast input is unavailable.
    /// Legacy and non-character loadouts retain their authored indices.
    /// </summary>
    private bool TryResolveCastSlotIndex(int requestedIndex, out int resolvedIndex)
    {
        return Loadout.TryResolveCastSlotIndex(requestedIndex, out resolvedIndex);
    }

    private void EnsureCommandRuntimeSkill(SkillSlot slot)
    {
        Loadout.EnsureCommandRuntimeSkill(slot);
    }

    private void EnsureRuntimeSkill(CharacterSkillEntry entry)
    {
        Loadout.EnsureRuntimeSkill(entry);
    }

    private void RefreshResolvedCommandSlotsIfNeeded()
    {
        Loadout.RefreshResolvedCommandSlotsIfNeeded();
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
        Loadout.RefreshHelperLoadout(force);
    }

    private void RebuildResolvedCommandSlots()
    {
        Loadout.RebuildResolvedCommandSlots();
    }

    private bool IsPassiveSlot(SkillSlot slot)
    {
        return Loadout.IsPassiveSlot(slot);
    }

    private string BuildSlotDebugSource(SkillSlot slot)
    {
        return Loadout.BuildSlotDebugSource(slot);
    }
}
