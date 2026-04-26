using UnityEngine;

internal enum SequenceExecutionPhase
{
    None = 0,
    WaitingForEnterCastMoment = 1,
    WaitingForEnterComplete = 2,
    WaitingForAttackStart = 3,
    WaitingForAttackCastMoment = 4,
    WaitingForAttackComplete = 5,
    WaitingForExitStart = 6,
    WaitingForExitCastMoment = 7,
    WaitingForExitComplete = 8,
}

internal sealed class PendingSequenceExecution
{
    public int executionId;
    public object owner;
    public ChainAttackStepDef step;
    public Transform lockedTarget;
    public Transform lockedTargetAnchor;
    public ISkillUser attackSkillUser;
    public SkillInstance attackRuntimeSkill;
    public SkillGemDefinition attackSkillDef;
    public int attackSkillLevel;
    public bool ignoreResourceCosts;
    public bool entrySnapApplied;
    public bool attackPayloadReleased;
    public bool continueReleased;
    public bool releaseReservationOnComplete;
    public int enterRequestId;
    public int attackRequestId;
    public int exitRequestId;
    public SequenceExecutionPhase phase;
    public float phaseStartedAt;
    public Vector3 recordedOriginPosition;
    public Quaternion recordedOriginRotation;
}

internal sealed class DeferredSequenceCleanup
{
    public object owner;
    public ChainActorExitMode exitMode;
    public Vector3 returnPosition;
    public Quaternion returnRotation;
}
