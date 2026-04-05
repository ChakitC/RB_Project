using System;
using UnityEngine;

public enum ChainActorRole
{
    None = 0,
    PartySlot1 = 1,
    PartySlot2 = 2,
    Helper = 3,
    Player = 4,
}

public enum ChainTargetSource
{
    ExplicitTargetOnly = 0,
    ExplicitTargetOrAimTarget = 1,
    AimTargetOnly = 2,
}

public enum ChainActorMoveMode
{
    KeepCurrentPosition = 0,
    WarpToLockedTargetAnchor = 1,
}

public enum ChainActorEnterMode
{
    None = 0,
    InstantTeleportToTarget = 1,
    UtilityWarpInToTarget = 2,
}

public enum ChainActorExitMode
{
    KeepAtCurrentPosition = 0,
    ReturnToRecordedOrigin = 1,
    ReturnToRecordedOriginOnSequenceEnd = 2,
    ReturnToRecordedOriginViaUtility = 3,
    ReturnToRecordedOriginViaUtilityOnSequenceEnd = 4,
    FadeOutAndDeactivate = 5,
    FadeOutAndDeactivateOnSequenceEnd = 6,
    ReturnToRecordedOriginThenWarpIn = 7,
    ReturnToRecordedOriginThenWarpInOnSequenceEnd = 8,
}

public enum ChainStepSkillSource
{
    ExplicitOverride = 0,
    ActorDefault = 1,
}

[Serializable]
public sealed class ChainAttackStepDef
{
    [Header("Identity")]
    public string stepId;
    public ChainActorRole actorRole = ChainActorRole.PartySlot1;

    [Header("Timing")]
    [Min(0f)] public float delayBefore;
    [Min(0f)] public float delayAfter;

    [Header("Execution")]
    public ChainStepSkillSource skillSource = ChainStepSkillSource.ExplicitOverride;
    public SkillGemDefinition skillDef;
    [Min(1)] public int skillLevel = 1;
    public bool ignoreResourceCosts = true;
    public bool faceLockedTargetOnStart;
    public bool faceLockedTargetOnCast = true;

    [Header("Availability")]
    public bool skipIfActorUnavailable;
    public bool skipIfTargetMissing;
    public bool requireActorAlive = true;

    [Header("Entry")]
    public ChainActorEnterMode enterMode = ChainActorEnterMode.None;
    public ChainAttackTeleportProfileDef teleportProfile;
    public bool allowFallbackToInstantTeleportIfUtilityUnavailable = true;

    [Header("Exit")]
    public ChainActorExitMode exitMode = ChainActorExitMode.KeepAtCurrentPosition;

    [Header("Movement")]
    public ChainActorMoveMode moveMode = ChainActorMoveMode.KeepCurrentPosition;
    public bool useTargetAnchorRotation = true;
    public Vector3 warpOffset = Vector3.zero;
    public float warpYawOffset;
    public bool requireNavMeshAtWarpPoint = true;
    [Min(0.05f)] public float warpNavMeshSampleDistance = 1f;

    [Header("Helper")]
    public HelperChainAttackSequenceDef helperChainAttackSequence;
    public bool helperHideOnComplete = true;

    public string RuntimeId => string.IsNullOrWhiteSpace(stepId) ? actorRole.ToString() : stepId;
    public int ClampedSkillLevel => Mathf.Max(1, skillLevel);
    public bool HasSkillConfigured => skillSource == ChainStepSkillSource.ActorDefault || skillDef != null;
    public bool UsesActorDefaultSkill => skillSource == ChainStepSkillSource.ActorDefault;
    public bool UsesDeferredExit =>
        exitMode == ChainActorExitMode.ReturnToRecordedOriginOnSequenceEnd ||
        exitMode == ChainActorExitMode.ReturnToRecordedOriginViaUtilityOnSequenceEnd ||
        exitMode == ChainActorExitMode.FadeOutAndDeactivateOnSequenceEnd ||
        exitMode == ChainActorExitMode.ReturnToRecordedOriginThenWarpInOnSequenceEnd;
}

[CreateAssetMenu(fileName = "ChainAttackSequence", menuName = "Game/Chain Attack/Sequence")]
public sealed class ChainAttackSequenceDef : ScriptableObject
{
    [Header("Identity")]
    public string sequenceId;
    [TextArea] public string description;

    [Header("Target")]
    public ChainTargetSource targetSource = ChainTargetSource.ExplicitTargetOrAimTarget;
    [Min(0.1f)] public float aimSearchRadius = 3f;
    public LayerMask targetLayers = ~0;
    public QueryTriggerInteraction targetTriggerInteraction = QueryTriggerInteraction.Ignore;
    public bool requireAimLineOfSight;
    public LayerMask aimObstacleLayers = 0;
    public bool cancelIfLockedTargetDies = true;

    [Header("Flow")]
    public bool stopWhenAnyStepFails = true;
    [Min(0f)] public float defaultStepIntervalSeconds = 0f;
    [Min(0.1f)] public float maxSequenceDurationSeconds = 12f;
    public ChainAttackStepDef[] steps;

    [Header("Debug")]
    public bool debugLogging;

    public string RuntimeId => string.IsNullOrWhiteSpace(sequenceId) ? name : sequenceId;
    public bool HasAnySteps => steps != null && steps.Length > 0;
}
