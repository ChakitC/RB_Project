using System;
using Sirenix.OdinInspector;
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

public enum ChainStepContinueMode
{
    OnStepComplete = 0,
    OnAttackCastMoment = 1,
    OnAttackNormalizedTime = 2,
}

public enum ChainStepContinueTimingSource
{
    SkillDefinition = 0,
    StepOverride = 1,
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
    public ChainStepContinueTimingSource continueTimingSource = ChainStepContinueTimingSource.SkillDefinition;
    public ChainStepContinueMode continueMode = ChainStepContinueMode.OnStepComplete;
    [Range(0f, 1f)] public float continueNormalizedTime = 1f;

    [Header("Execution")]
    public ChainStepSkillSource skillSource = ChainStepSkillSource.ExplicitOverride;
    public SkillGemDefinition skillDef;
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
    [Tooltip("เมื่อหาจุด warp-in ที่ปลอดภัยไม่ได้ (เป้ายืนติดกำแพง ฯลฯ) ให้ actor โจมตีจากตำแหน่งที่ยืนอยู่แทน " +
             "ปิดเพื่อให้ step ล้มไปเลยแบบเดิม ซึ่งจะลาก chain ทั้งอันล้มตามถ้า stopWhenAnyStepFails เปิดอยู่")]
    public bool allowInPlaceAttackIfEntryPoseBlocked = true;

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
    public float ClampedContinueNormalizedTime => Mathf.Clamp01(continueNormalizedTime);
    public bool HasSkillConfigured => skillSource == ChainStepSkillSource.ActorDefault || skillDef != null;
    public bool UsesActorDefaultSkill => skillSource == ChainStepSkillSource.ActorDefault;
    public bool UsesStepContinueOverride => continueTimingSource == ChainStepContinueTimingSource.StepOverride;
    public bool UsesEarlyContinueSignal =>
        UsesStepContinueOverride &&
        continueMode != ChainStepContinueMode.OnStepComplete;
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
    [InfoBox(
        "ExplicitTargetOnly ทำให้ปุ่ม ChainReady [F] ใช้ไม่ได้เลย เพราะฝั่ง input ไม่มี explicit target " +
        "จะ resolve ไม่ได้ทุกครั้งแล้วปุ่มตกไปเป็น Interact แทน",
        InfoMessageType.Error,
        nameof(IsExplicitTargetOnly))]
    [InfoBox(
        "AimTargetOnly ทำให้ coordinator เมิน target ที่ปุ่ม [F] ล็อกไว้แล้วไป resolve จาก aim ใหม่ " +
        "chain อาจไปตีคนละตัวกับตัวที่ ChainReady อยู่",
        InfoMessageType.Warning,
        nameof(IsAimTargetOnly))]
    public ChainTargetSource targetSource = ChainTargetSource.ExplicitTargetOrAimTarget;
    [Min(0.1f)] public float aimSearchRadius = 3f;
    [Tooltip("รัศมีรอบตัวผู้เล่นที่ใช้กวาดหาเป้า ChainReady โดยเฉพาะ ใช้เฉพาะตอนกด [F] เท่านั้น " +
             "แยกจาก aimSearchRadius เพราะ capsule ของ aim ยึดกับกล้อง เป้าที่อยู่นอกจอจึงหลุดง่าย")]
    [Min(0f)] public float chainReadySearchRadius = 12f;
    public LayerMask targetLayers = ~0;
    public QueryTriggerInteraction targetTriggerInteraction = QueryTriggerInteraction.Ignore;
    public bool requireAimLineOfSight;
    public LayerMask aimObstacleLayers = 0;
    public bool cancelIfLockedTargetDies = true;

    [Header("Flow")]
    [InfoBox(
        "step ในลิสต์ติ๊ก 'Skip If Actor Unavailable' ไว้ แต่ stopWhenAnyStepFails เปิดอยู่ — สองอย่างนี้ขัดกัน " +
        "actor ที่ไม่พร้อมจะถูกข้าม แต่พอ step ไหนล้มด้วยเหตุอื่น (หาที่ warp ไม่ได้ ฯลฯ) chain จะถูกตัดจบทั้งอัน " +
        "แล้วเป้าได้ Stagger ฟรีโดยไม่มีใครโจมตี ปิด stopWhenAnyStepFails เพื่อให้ chain เดินต่อกับคนที่เหลือ",
        InfoMessageType.Warning,
        nameof(HasSkipAndStopConflict))]
    public bool stopWhenAnyStepFails = true;
    [Min(0f)] public float defaultStepIntervalSeconds = 0f;
    [Min(0.1f)] public float maxSequenceDurationSeconds = 12f;
    public ChainAttackStepDef[] steps;

    [Header("Debug")]
    public bool debugLogging;

    public string RuntimeId => string.IsNullOrWhiteSpace(sequenceId) ? name : sequenceId;
    public bool HasAnySteps => steps != null && steps.Length > 0;

    /// <summary>
    /// Ticking "skip if unavailable" on a step says the chain should survive that actor dropping out,
    /// which stopWhenAnyStepFails then contradicts for every other failure reason.
    /// </summary>
    bool HasSkipAndStopConflict
    {
        get
        {
            if (!stopWhenAnyStepFails || steps == null)
                return false;

            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] != null && steps[i].skipIfActorUnavailable)
                    return true;
            }

            return false;
        }
    }

    bool IsExplicitTargetOnly => targetSource == ChainTargetSource.ExplicitTargetOnly;
    bool IsAimTargetOnly => targetSource == ChainTargetSource.AimTargetOnly;

    /// <summary>The ChainReady sweep never searches a smaller area than the ordinary aim search.</summary>
    public float ResolvedChainReadySearchRadius => Mathf.Max(aimSearchRadius, chainReadySearchRadius);

#if UNITY_EDITOR
    void OnValidate()
    {
        // Only the outright-broken case is worth an error in the console; the AimTargetOnly caveat
        // stays an inspector InfoBox because it is a legitimate choice for proc-driven sequences.
        if (IsExplicitTargetOnly)
        {
            Debug.LogError(
                $"[ChainAttackSequenceDef] '{name}' uses targetSource=ExplicitTargetOnly, which makes " +
                "the ChainReady [F] press impossible: the input path passes no explicit target, so the " +
                "press always falls through to Interact.",
                this);
        }
    }
#endif
}
