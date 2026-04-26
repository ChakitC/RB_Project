using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.Serialization;

public class CharacterSkillManager : MonoBehaviour
{
    static readonly SkillSlot[] EmptyAutonomousSlots = Array.Empty<SkillSlot>();
    static readonly HelperProcSlot[] EmptyHelperProcSlots = Array.Empty<HelperProcSlot>();
    static readonly CharacterChainSlot[] EmptyLegacyHelperProcSlots = Array.Empty<CharacterChainSlot>();
    static readonly PassiveSkillSlot[] EmptyPassiveSlots = Array.Empty<PassiveSkillSlot>();

    private CharacteContext ctx;
    private CharacterAnimBrain animBrain;
    private WeaponSystem weaponSystem;
    private SkillSlot pendingSlot;
    private SkillCastOrchestrator castOrchestrator;

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

    [FormerlySerializedAs("helperProcSlots")]
    [FormerlySerializedAs("reactiveProcSlots")]
    [FormerlySerializedAs("chainSlots")]
    [SerializeField, HideInInspector] private CharacterChainSlot[] legacyHelperProcSlots = EmptyLegacyHelperProcSlots;

    [Header("Passive Loadout")]
    [FormerlySerializedAs("passiveSlots")]
    [SerializeField] private PassiveSkillSlot[] passiveSlots = EmptyPassiveSlots;

    public IReadOnlyList<SkillSlot> AutonomousSlots => autonomousSlots ?? EmptyAutonomousSlots;
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
        MigrateLegacyHelperProcSlotsIfNeeded();
        CacheReferences();
        castOrchestrator = new SkillCastOrchestrator(this);

        if (skillUser == null)
            Debug.LogError("CharacterSkillManager requires an ISkillUser component.");

        RebuildAllRuntimeSkills();
    }

    private void OnValidate()
    {
        MigrateLegacyHelperProcSlotsIfNeeded();
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
        castOrchestrator?.CancelPendingCast();
    }

    private void OnDestroy()
    {
        pendingSlot = null;
        castOrchestrator?.CancelPendingCast();
    }

    private void Update()
    {
        castOrchestrator?.Tick();
        if (castOrchestrator != null && castOrchestrator.HasPendingCast)
            return;

        pendingSlot = null;
        if (autonomousSlots == null)
            return;

        foreach (SkillSlot slot in autonomousSlots)
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
               !IsSkillUseBlocked() &&
               slot.runtimeSkill.CanCast(skillUser);
    }

    public bool CanStartPlayerCommandSkill()
    {
        CacheReferences();
        EnsureRuntimeSkill(playerCommandSkill);
        return playerCommandSkill != null &&
               playerCommandSkill.runtimeSkill != null &&
               skillUser != null &&
               !IsSkillUseBlocked() &&
               playerCommandSkill.runtimeSkill.CanCast(skillUser);
    }

    public SkillCastStartResult TryStartPlayerCommandSkill()
    {
        CacheReferences();
        return TryBeginEntryCast(playerCommandSkill, "player-command");
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
        MigrateLegacyHelperProcSlotsIfNeeded();

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

        if (ReferenceEquals(pendingSlot, slot))
        {
            pendingSlot = null;
            castOrchestrator?.CancelPendingCast();
        }

        slot.skillAsset = null;
        slot.supportAssets = null;
        slot.runtimeSkill = null;
    }

    public void AssignSkillToSlot(int index, SkillGemDefinition asset, int level = 1)
    {
        if (!TryGetCommandSlot(index, out SkillSlot slot))
            return;

        if (ReferenceEquals(pendingSlot, slot))
        {
            pendingSlot = null;
            castOrchestrator?.CancelPendingCast();
        }

        slot.skillAsset = asset;
        slot.skillLevel = level;
        slot.runtimeSkill = BuildRuntimeSkill(slot, asset, level);
    }

    private SkillCastStartResult TryBeginCast(SkillSlot slot)
    {
        CacheReferences();
        EnsureCommandRuntimeSkill(slot);

        if (slot == null || slot.runtimeSkill == null || skillUser == null)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        castOrchestrator ??= new SkillCastOrchestrator(this);
        SkillInstance runtimeSkill = slot.runtimeSkill;
        SkillCastStartResult result = castOrchestrator.TryStartCast(new SkillCastRequest(
            runtimeSkill,
            skillUser,
            animationDriver: animBrain,
            canProceed: () => IsSlotCastStillValid(slot, runtimeSkill),
            onStarted: StopWeaponActivityForSkillCast,
            useAnimationDriver: true,
            allowImmediateFallback: true,
            debugSource: $"slot:{slot.hotkey}"));

        pendingSlot = result.Kind == SkillCastStartKind.WaitingForAnimation
            ? slot
            : null;

        return result;
    }

    private SkillCastStartResult TryBeginEntryCast(CharacterSkillEntry entry, string debugSource)
    {
        CacheReferences();
        EnsureRuntimeSkill(entry);

        if (entry == null || entry.runtimeSkill == null || skillUser == null)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        castOrchestrator ??= new SkillCastOrchestrator(this);
        SkillInstance runtimeSkill = entry.runtimeSkill;
        return castOrchestrator.TryStartCast(new SkillCastRequest(
            runtimeSkill,
            skillUser,
            animationDriver: animBrain,
            canProceed: () => IsSkillEntryCastStillValid(entry, runtimeSkill),
            onStarted: StopWeaponActivityForSkillCast,
            useAnimationDriver: true,
            allowImmediateFallback: true,
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

        if (autonomousSlots == null || slotIndex < 0 || slotIndex >= autonomousSlots.Length)
            return false;

        slot = autonomousSlots[slotIndex];
        return slot != null;
    }

    private void RebuildAllRuntimeSkills()
    {
        RebuildAllAutonomousRuntimeSkills();
        EnsureRuntimeSkill(playerCommandSkill);
        EnsureRuntimeSkill(chainAttackSkill);
    }

    private void RebuildAllAutonomousRuntimeSkills()
    {
        if (autonomousSlots == null)
            return;

        for (int i = 0; i < autonomousSlots.Length; i++)
            EnsureCommandRuntimeSkill(autonomousSlots[i]);
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
        ctx = GetComponent<CharacteContext>();
        if (ctx == null)
            ctx = GetComponentInParent<CharacteContext>();

        skillUser = GetComponent<ISkillUser>();
        if (skillUser == null && ctx != null && ctx.EnegySystem != null)
            skillUser = ctx.EnegySystem;

        animBrain = ResolveAnimBrainReference();
        weaponSystem = GetComponent<WeaponSystem>();

        if (ctx == null)
            return;

        if (ctx.stateHub == null)
            ctx.stateHub = GetComponent<StateHub>();

        if (ctx.HealthSystem == null)
            ctx.HealthSystem = GetComponent<HealthSystem>();

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

    private void MigrateLegacyHelperProcSlotsIfNeeded()
    {
        if (helperProcLoadout != null && helperProcLoadout.Length > 0)
            return;

        if (legacyHelperProcSlots == null || legacyHelperProcSlots.Length == 0)
            return;

        List<HelperProcSlot> migrated = null;
        for (int i = 0; i < legacyHelperProcSlots.Length; i++)
        {
            HelperProcSlot migratedSlot = legacyHelperProcSlots[i]?.ToHelperProcSlot();
            if (migratedSlot == null || !migratedSlot.IsConfigured)
                continue;

            migrated ??= new List<HelperProcSlot>();
            migrated.Add(migratedSlot);
        }

        if (migrated == null || migrated.Count == 0)
            return;

        helperProcLoadout = migrated.ToArray();
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
        castOrchestrator?.CancelPendingCast();
    }

    private void OnCharacterDead()
    {
        pendingSlot = null;
        castOrchestrator?.CancelPendingCast();
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
        DebugSource = debugSource;
    }
}

public sealed class SkillCastOrchestrator
{
    private enum PendingCastCancelReason
    {
        InvalidState,
        AnimationInterrupted,
        Disabled,
    }

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
        public readonly List<StringReference> TimelineEventNames = new List<StringReference>();
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

    public void Tick()
    {
        if (pendingCast == null)
            return;

        if (!CanProceed(pendingCast.Request))
            CancelPendingCast(PendingCastCancelReason.InvalidState);
    }

    public void CancelPendingCast()
    {
        CancelPendingCast(PendingCastCancelReason.Disabled);
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
        bool requiresTimelineEvents = skillDef.payload != null && skillDef.payload.RequiresSkillTimelineEvents;

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

            skillDef.payload?.CollectTimelineEventNames(context.TimelineEventNames);

            bool started = executionAnimBrain.TryPlaySkill(
                context.RequestId,
                context.SkillDef,
                context.CastPointNormalized,
                context.TimelineEventNames);

            if (started)
            {
                Subscribe(context.AnimationDriver);
                pendingCast = context;
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

        return TryReleaseCast(
                request,
                requestId,
                runtimeSkill,
                skillUser,
                executionAnimBrain)
            ? new SkillCastStartResult(SkillCastStartKind.ImmediateSuccess, requestId)
            : Rejected();
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
            return false;
        }

        context.Released = true;
        return TryReleaseCast(
            context.Request,
            context.RequestId,
            context.RuntimeSkill,
            context.SkillUser,
            context.AnimationDriver);
    }

    private void CancelPendingCast(PendingCastCancelReason reason)
    {
        if (pendingCast == null)
            return;

        PendingCastContext context = pendingCast;
        pendingCast = null;
        Unsubscribe(context.AnimationDriver);
        CancelPendingCastRequest(context, stopAnimation: reason != PendingCastCancelReason.AnimationInterrupted);
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

        CancelPendingCast(PendingCastCancelReason.AnimationInterrupted);
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
