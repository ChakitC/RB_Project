using UnityEngine;

[CreateAssetMenu(fileName = "AITargetingProfile", menuName = "Game/AI/Targeting Profile")]
public sealed class AITargetingProfileDef : ScriptableObject
{
    [Header("Memory")]
    [SerializeField, Min(0f)] private float gracePeriod = 1.25f;
    [SerializeField] private bool keepLastSeenTarget = true;

    [Header("Scoring")]
    [SerializeField, Min(0f)] private float distanceScoreWeight = 12f;
    [SerializeField] private float playerPriorityBonus = 16f;
    [SerializeField] private float companionPriorityBonus = 0f;
    [SerializeField] private float tankPriorityBonus = 6f;
    [SerializeField] private float healerPriorityBonus = 9f;
    [SerializeField] private float supportPriorityBonus = 6f;
    [SerializeField] private float sniperPriorityBonus = 7f;
    [SerializeField] private float assaultPriorityBonus = 0f;
    [SerializeField, Min(0f)] private float currentTargetStickiness = 5f;
    [SerializeField, Min(0f)] private float lineOfSightPenalty = 6f;

    [Header("Retarget")]
    [SerializeField] private Vector2 retargetIntervalRange = new Vector2(0.75f, 1.1f);
    [SerializeField, Min(0f)] private float minTargetLockDuration = 0.9f;
    [SerializeField, Min(0f)] private float switchScoreThreshold = 2.5f;
    [SerializeField, Range(0f, 1f)] private float switchScoreThresholdRatio = 0.2f;
    [SerializeField, Min(0f)] private float lostTargetHoldDuration = 0.35f;

    [Header("Threat")]
    [SerializeField] private bool useThreatMemory = true;
    [SerializeField, Min(0f)] private float damageThreatMultiplier = 1f;
    [SerializeField, Min(0f)] private float threatScoreWeight = 1f;
    [SerializeField, Min(0f)] private float threatDecayPerSecond = 8f;
    [SerializeField, Min(0f)] private float maxThreatPerTarget = 100f;

    [Header("Taunt")]
    [SerializeField, Min(0f)] private float tauntScoreBonus = 1000f;

    public float GracePeriod => Mathf.Max(0f, gracePeriod);
    public bool KeepLastSeenTarget => keepLastSeenTarget;
    public float DistanceScoreWeight => Mathf.Max(0f, distanceScoreWeight);
    public float CurrentTargetStickiness => Mathf.Max(0f, currentTargetStickiness);
    public float LineOfSightPenalty => Mathf.Max(0f, lineOfSightPenalty);
    public Vector2 RetargetIntervalRange => retargetIntervalRange;
    public float MinTargetLockDuration => Mathf.Max(0f, minTargetLockDuration);
    public float SwitchScoreThreshold => Mathf.Max(0f, switchScoreThreshold);
    public float SwitchScoreThresholdRatio => Mathf.Clamp01(switchScoreThresholdRatio);
    public float LostTargetHoldDuration => Mathf.Max(0f, lostTargetHoldDuration);
    public bool UseThreatMemory => useThreatMemory;
    public float DamageThreatMultiplier => Mathf.Max(0f, damageThreatMultiplier);
    public float ThreatScoreWeight => Mathf.Max(0f, threatScoreWeight);
    public float ThreatDecayPerSecond => Mathf.Max(0f, threatDecayPerSecond);
    public float MaxThreatPerTarget => Mathf.Max(0f, maxThreatPerTarget);
    public float TauntScoreBonus => Mathf.Max(0f, tauntScoreBonus);

    public float GetIdentityPriorityBonus(AITargetIdentity identity)
    {
        switch (identity)
        {
            case AITargetIdentity.Player:
                return playerPriorityBonus;
            case AITargetIdentity.Companion:
                return companionPriorityBonus;
            default:
                return 0f;
        }
    }

    public float GetCombatRolePriorityBonus(CharacterCombatRole role)
    {
        switch (role)
        {
            case CharacterCombatRole.Tank:
                return tankPriorityBonus;
            case CharacterCombatRole.Healer:
                return healerPriorityBonus;
            case CharacterCombatRole.Support:
                return supportPriorityBonus;
            case CharacterCombatRole.Sniper:
                return sniperPriorityBonus;
            case CharacterCombatRole.Assault:
                return assaultPriorityBonus;
            default:
                return 0f;
        }
    }
}
