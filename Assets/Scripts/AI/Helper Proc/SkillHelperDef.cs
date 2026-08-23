using UnityEngine;

[CreateAssetMenu(fileName = "SkillHelperDef", menuName = "Game/Helper Proc/Skill Helper")]
public class SkillHelperDef : ScriptableObject
{
    [Header("Identity")]
    public string helperId;
    public string displayName;
    [TextArea]
    public string description;

    [Header("Trigger")]
    public SkillHelperTriggerMode triggerMode = SkillHelperTriggerMode.CombatEventProc;
    public PassiveEventType triggerEvent = PassiveEventType.Hit;
    public PassiveOriginFilter originFilter = PassiveOriginFilter.ExternalOnly;
    [Range(0f, 1f)] public float procChance = 0.005f;
    [Min(0f)] public float internalCooldownSeconds = 0f;
    public bool requireTarget = true;
    public bool requireAttackId;
    public bool oncePerAttackId = true;
    public bool requireOwnerAlive = true;

    [Header("Party Health Threshold")]
    [Tooltip("Fires when an eligible party member's health ratio drops to or below this value.")]
    [Range(0f, 1f)] public float partyHealthThreshold = 0.35f;

    [Tooltip("Hold the request and re-check eligibility once the helper is free, instead of dropping it.")]
    public bool queueWhileHelperBusy = true;

    [Tooltip("Roles this trigger may target. The Helper itself is never eligible.")]
    public ChainActorRole[] eligibleRoles = DefaultEligibleRoles;

    static readonly ChainActorRole[] DefaultEligibleRoles =
    {
        ChainActorRole.Player,
        ChainActorRole.PartySlot1,
        ChainActorRole.PartySlot2,
    };

    /// <summary>
    /// True when this proc is driven by party health rather than by a combat event. Threshold
    /// procs are deterministic - <see cref="procChance"/> is not rolled - because an assist the
    /// player is relying on to survive must not be a coin flip.
    /// </summary>
    public bool IsPartyHealthTrigger => triggerMode == SkillHelperTriggerMode.PartyHealthThreshold;

    public bool IsRoleEligible(ChainActorRole role)
    {
        // The helper is the one performing the assist, so it can never also receive it.
        if (role == ChainActorRole.Helper || role == ChainActorRole.None)
            return false;

        ChainActorRole[] roles = eligibleRoles != null && eligibleRoles.Length > 0
            ? eligibleRoles
            : DefaultEligibleRoles;

        for (int i = 0; i < roles.Length; i++)
        {
            if (roles[i] == role)
                return true;
        }

        return false;
    }

    [Header("Execution")]
    public SkillGemDefinition executionSkill;
    public HelperChainAttackSequenceDef chainAttackSequence;
    public bool hideHelperOnSkillComplete = true;
    public bool blockWhileHelperBusy = true;

    [Header("Debug")]
    public bool debugLogging;

    public string RuntimeId => string.IsNullOrWhiteSpace(helperId) ? name : helperId;
    public bool HasExecutionConfigured => executionSkill != null;
}
