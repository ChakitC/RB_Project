using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct ActiveSkillCastInfo
{
    public readonly SkillExecutionKind ExecutionKind;
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
        string debugSource,
        SkillExecutionKind executionKind = SkillExecutionKind.StandardSkill)
    {
        ExecutionKind = executionKind;
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

