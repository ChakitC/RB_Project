using System.Collections.Generic;
using Animancer;
using UnityEngine;

public class PlayerSkillManager : MonoBehaviour
{
    private enum PendingCastCancelReason
    {
        InvalidState,
        AnimationInterrupted,
        SlotInvalidated,
        Disabled,
        Destroyed,
    }

    private sealed class PendingCastContext
    {
        public SkillSlot slot;
        public SkillInstance runtimeSkill;
        public SkillGemDefinition skillDef;
        public int requestId;
        public bool started;
        public bool released;
        public bool cancelled;
        public float castPointNormalized;
        public bool requiresTimelineEvents;
        public readonly List<StringReference> timelineEventNames = new List<StringReference>();
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

    private CharacteContext ctx;
    private CharacterAnimBrain animBrain;
    private WeaponSystem weaponSystem;
    private PendingCastContext pendingCast;
    private int nextCastRequestId = 1;
    private readonly Dictionary<SkillGemDefinition, float> sharedCooldownReadyAt = new Dictionary<SkillGemDefinition, float>();

    public ISkillUser skillUser;
    public SkillSlot[] slots;

    private void Awake()
    {
        ctx = GetComponent<CharacteContext>();
        skillUser = GetComponent<ISkillUser>();
        animBrain = GetComponent<CharacterAnimBrain>();
        weaponSystem = GetComponent<WeaponSystem>();

        if (ctx != null)
        {
            if (ctx.stateHub == null)
                ctx.stateHub = GetComponent<StateHub>();
            if (ctx.HealthSystem == null)
                ctx.HealthSystem = GetComponent<HealthSystem>();
            if (ctx.AnimBrain == null)
                ctx.AnimBrain = animBrain;
            if (ctx.WeaponSystem == null)
                ctx.WeaponSystem = weaponSystem;
            else if (weaponSystem == null)
                weaponSystem = ctx.WeaponSystem;
        }

        if (skillUser == null)
            Debug.LogError("PlayerSkillManager requires an ISkillUser component.");

        foreach (var slot in slots)
            slot.runtimeSkill = BuildRuntimeSkill(slot, slot.skillAsset, slot != null ? slot.skillLevel : 1);
    }

    private void OnEnable()
    {
        if (animBrain == null)
            animBrain = GetComponent<CharacterAnimBrain>();

        if (animBrain != null)
        {
            animBrain.SkillCastMomentReached += OnSkillCastMomentReached;
            animBrain.SkillCastInterrupted += OnSkillCastInterrupted;
        }

        if (ctx == null)
            ctx = GetComponent<CharacteContext>();

        if (ctx != null)
        {
            if (ctx.HealthSystem == null)
                ctx.HealthSystem = GetComponent<HealthSystem>();

            if (ctx.HealthSystem != null)
            {
                ctx.HealthSystem.CharacterDown += OnCharacterDown;
                ctx.HealthSystem.CharacterDead += OnCharacterDead;
            }
        }
    }

    private void OnDisable()
    {
        if (animBrain != null)
        {
            animBrain.SkillCastMomentReached -= OnSkillCastMomentReached;
            animBrain.SkillCastInterrupted -= OnSkillCastInterrupted;
        }

        if (ctx != null && ctx.HealthSystem != null)
        {
            ctx.HealthSystem.CharacterDown -= OnCharacterDown;
            ctx.HealthSystem.CharacterDead -= OnCharacterDead;
        }

        CancelPendingCast(PendingCastCancelReason.Disabled);
    }

    private void OnDestroy()
    {
        CancelPendingCast(PendingCastCancelReason.Destroyed);
    }

    private void Update()
    {
        if (pendingCast != null)
        {
            if (!IsPendingCastStillValid(pendingCast) || IsSkillUseBlocked())
                CancelPendingCast(PendingCastCancelReason.InvalidState);

            return;
        }

        foreach (var slot in slots)
        {
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
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        return TryBeginCast(slots[slotIndex]);
    }

    private SkillCastStartResult TryBeginCast(SkillSlot slot)
    {
        if (slot == null || pendingCast != null)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        var skill = slot.runtimeSkill;
        if (skill == null)
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        if (IsSkillUseBlocked())
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        if (!skill.CanCast(skillUser, out var castStats))
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        if (!IsSharedCooldownReady(skill, castStats))
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);

        var context = new PendingCastContext
        {
            slot = slot,
            runtimeSkill = skill,
            skillDef = skill.def,
            requestId = NextCastRequestId(),
            started = true,
            castPointNormalized = skill.def != null ? skill.def.GetCastPointNormalized() : 0.35f,
            requiresTimelineEvents = skill.def != null &&
                                     skill.def.payload != null &&
                                     skill.def.payload.RequiresSkillTimelineEvents,
        };

        context.timelineEventNames.Clear();
        skill.def?.payload?.CollectTimelineEventNames(context.timelineEventNames);

        pendingCast = context;
        StopWeaponActivityForSkillCast();

        // Skill identity is fixed here, but origin/aim are sampled later by SkillInstance.Cast so
        // the projectile uses the live cast socket and facing at the release frame.
        bool usingAnimationDriver = animBrain != null &&
                                    animBrain.TryPlaySkill(
                                        context.requestId,
                                        context.skillDef,
                                        context.castPointNormalized,
                                        context.timelineEventNames);

        if (usingAnimationDriver)
            return new SkillCastStartResult(SkillCastStartKind.WaitingForAnimation, context.requestId);

        if (context.requiresTimelineEvents)
        {
            Debug.LogWarning(
                $"Skill '{context.skillDef?.name ?? "<unknown>"}' requires Animancer timeline events, but no skill animation playback was available.",
                this);
            CancelPendingCast(PendingCastCancelReason.InvalidState, stopAnimation: false);
            return new SkillCastStartResult(SkillCastStartKind.Rejected, 0);
        }

        return ReleasePendingCast(context.requestId)
            ? new SkillCastStartResult(SkillCastStartKind.ImmediateSuccess, context.requestId)
            : new SkillCastStartResult(SkillCastStartKind.Rejected, 0);
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= slots.Length)
            return;

        if (pendingCast != null && ReferenceEquals(pendingCast.slot, slots[index]))
            CancelPendingCast(PendingCastCancelReason.SlotInvalidated);

        slots[index].skillAsset = null;
        slots[index].supportAssets = null;
        slots[index].runtimeSkill = null;
    }

    public void AssignSkillToSlot(int index, SkillGemDefinition asset, int level = 1)
    {
        if (index < 0 || index >= slots.Length)
            return;

        if (pendingCast != null && ReferenceEquals(pendingCast.slot, slots[index]))
            CancelPendingCast(PendingCastCancelReason.SlotInvalidated);

        slots[index].skillAsset = asset;
        slots[index].skillLevel = level;
        slots[index].runtimeSkill = BuildRuntimeSkill(slots[index], asset, level);
    }

    private SkillInstance BuildRuntimeSkill(SkillSlot slot, SkillGemDefinition asset, int level)
    {
        if (slot == null || asset == null)
            return null;

        int resolvedSkillLevel = asset.ClampLevel(level);
        slot.skillLevel = resolvedSkillLevel;

        var instance = new SkillInstance
        {
            def = asset,
            level = resolvedSkillLevel,
        };

        if (slot.supportAssets == null)
            return instance;

        foreach (var supportAsset in slot.supportAssets)
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

    private bool ReleasePendingCast(int requestId)
    {
        if (pendingCast == null ||
            pendingCast.requestId != requestId ||
            pendingCast.cancelled ||
            pendingCast.released)
        {
            return false;
        }

        if (!IsPendingCastStillValid(pendingCast) || IsSkillUseBlocked())
        {
            CancelPendingCast(PendingCastCancelReason.InvalidState, stopAnimation: false);
            return false;
        }

        if (!pendingCast.runtimeSkill.CanCast(skillUser, out var castStats))
        {
            CancelPendingCast(PendingCastCancelReason.InvalidState, stopAnimation: false);
            return false;
        }

        if (!IsSharedCooldownReady(pendingCast.runtimeSkill, castStats))
        {
            CancelPendingCast(PendingCastCancelReason.InvalidState, stopAnimation: false);
            return false;
        }

        var context = pendingCast;
        context.released = true;

        StampSharedCooldown(context.runtimeSkill, castStats);
        context.runtimeSkill.Cast(skillUser, animBrain, context.requestId);
        PlayCastCue(context.runtimeSkill);

        pendingCast = null;
        return true;
    }

    private void CancelPendingCast(PendingCastCancelReason reason, bool stopAnimation = true)
    {
        if (pendingCast == null)
            return;

        int requestId = pendingCast.requestId;
        pendingCast.cancelled = true;
        pendingCast = null;

        if (stopAnimation)
            animBrain?.CancelSkillCastRequest(requestId);
    }

    private bool IsPendingCastStillValid(PendingCastContext context)
    {
        if (context == null || context.slot == null || context.runtimeSkill == null)
            return false;

        if (context.slot.runtimeSkill != context.runtimeSkill)
            return false;

        if (context.slot.skillAsset != context.skillDef)
            return false;

        return context.runtimeSkill.def == context.skillDef;
    }

    private bool IsSkillUseBlocked()
    {
        return ctx != null && ctx.stateHub != null && !ctx.stateHub.CanUseSkill();
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

    private void StopWeaponActivityForSkillCast()
    {
        if (weaponSystem == null)
            weaponSystem = ctx != null ? ctx.WeaponSystem : GetComponent<WeaponSystem>();

        weaponSystem?.SetFiring(false);
        ctx?.stateHub?.SetFireHeld(false);

        if (weaponSystem != null && weaponSystem.IsReloading)
            weaponSystem.CancelReload();
    }

    private int NextCastRequestId()
    {
        if (nextCastRequestId == int.MaxValue)
            nextCastRequestId = 1;

        return nextCastRequestId++;
    }

    private void PlayCastCue(SkillInstance skill)
    {
        if (skill == null || skill.def == null || skill.def.castCue == null)
            return;

        Transform castOrigin = skillUser != null && skillUser.CastOrigin != null
            ? skillUser.CastOrigin
            : transform;

        AudioService.Instance.PlayAttached(skill.def.castCue, castOrigin, Vector3.zero);
    }

    private void OnSkillCastMomentReached(int requestId)
    {
        ReleasePendingCast(requestId);
    }

    private void OnSkillCastInterrupted(int requestId)
    {
        if (pendingCast == null || pendingCast.requestId != requestId)
            return;

        CancelPendingCast(PendingCastCancelReason.AnimationInterrupted, stopAnimation: false);
    }

    private void OnCharacterDown()
    {
        CancelPendingCast(PendingCastCancelReason.InvalidState);
    }

    private void OnCharacterDead()
    {
        CancelPendingCast(PendingCastCancelReason.InvalidState);
    }
}
