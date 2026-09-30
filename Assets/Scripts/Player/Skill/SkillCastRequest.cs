using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct SkillCastRequest
{
    public readonly SkillExecutionKind ExecutionKind;
    public readonly MeleeType MeleeType;
    public readonly Vector2 MeleeChainWindow;
    public readonly SkillInstance RuntimeSkill;
    public readonly ISkillUser SkillUser;
    public readonly CharacterAnimDriver AnimationDriver;
    public readonly Func<bool> CanProceed;
    public readonly Action OnStarted;
    public readonly Action<ActiveSkillCastInfo> OnCommitted;
    public readonly Action<ActiveSkillCastInfo, SkillExecutionResult> OnExecutionFailed;
    public readonly Action<ActiveSkillCastInfo, SkillCastCancelReason> OnCancelled;
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
    public readonly ulong CombatChainId;
    public readonly int CombatDepth;
    public readonly ComboExecutionProvenance ComboProvenance;
    public readonly SkillFacingSnapshot FacingSnapshot;

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
        SkillCastCostPolicy costPolicy = SkillCastCostPolicy.Normal,
        Action<ActiveSkillCastInfo> onCommitted = null,
        Action<ActiveSkillCastInfo, SkillExecutionResult> onExecutionFailed = null,
        Action<ActiveSkillCastInfo, SkillCastCancelReason> onCancelled = null,
        ulong combatChainId = 0,
        int combatDepth = 0,
        ComboExecutionProvenance comboProvenance = default,
        SkillFacingSnapshot facingSnapshot = default,
        SkillExecutionKind executionKind = SkillExecutionKind.StandardSkill,
        MeleeType meleeType = MeleeType.Light,
        Vector2 meleeChainWindow = default)
    {
        ExecutionKind = executionKind;
        MeleeType = meleeType;
        MeleeChainWindow = meleeChainWindow;
        RuntimeSkill = runtimeSkill;
        SkillUser = skillUser;
        AnimationDriver = animationDriver;
        CanProceed = canProceed;
        OnStarted = onStarted;
        OnCommitted = onCommitted;
        OnExecutionFailed = onExecutionFailed;
        OnCancelled = onCancelled;
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
        CombatChainId = combatChainId;
        CombatDepth = Mathf.Max(0, combatDepth);
        ComboProvenance = comboProvenance;
        FacingSnapshot = facingSnapshot;
    }
}

