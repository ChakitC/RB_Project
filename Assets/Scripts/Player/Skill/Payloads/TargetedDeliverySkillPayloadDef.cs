using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>Where the carried object sits on the caster before it is released.</summary>
public enum DeliveryLaunchAnchorMode
{
    CastOrigin = 0,
    ChildPath = 1,
    HumanoidBone = 2,
}

/// <summary>How much health the delivery restores when it reaches its target.</summary>
public enum DeliveryHealMode
{
    None = 0,
    Flat = 1,
    PercentMaxHealth = 2,
}

/// <summary>What a delivery does when its target stops existing while it is in the air.</summary>
public enum DeliveryTargetInvalidPolicy
{
    /// <summary>Finish the flight at the last known point and play the impact. No gameplay effect.</summary>
    PresentAtLastKnownPoint = 0,

    /// <summary>Vanish immediately, no impact presentation.</summary>
    DespawnImmediately = 1,
}

/// <summary>
/// Carries an object from the caster's hand to one locked character, then heals and buffs them
/// on arrival.
///
/// Deliberately knows nothing about who is casting. It reads its target from
/// <see cref="SkillCastContext.PrimaryTarget"/>, which any caster - player, companion, or a
/// summoned helper - fills in with its own targeting rules, so the same asset can be reused
/// without a single reference back to the system that aimed it.
/// </summary>
[HideMonoScript]
public sealed class TargetedDeliverySkillPayloadDef : SkillPayloadDef
{
    [Serializable]
    public sealed class StatusApplication
    {
        public StatusApplicationSpec spec = new();
    }

    [SerializeField, BoxGroup("Caster Placement")]
    [LabelText("Target Stand-Off At Cast Point"), MinValue(0.01f), SuffixLabel("m")]
    [Tooltip("Horizontal distance between the caster and locked target at the skill cast point. " +
             "Root motion is preserved; placement derives the animation start pose from this distance.")]
    private float targetStandOffDistanceAtCastPoint = 1.2f;

    [PropertyOrder(-20)]
    [InfoBox("Throws a carried object at the cast's locked target, then heals and buffs them on arrival. " +
             "The clip must raise a DeliveryRelease event after the cast point.")]
    [SerializeField, BoxGroup("Delivery")]
    [LabelText("Delivery Prefab")]
    private GameObject deliveryPrefab;

    [SerializeField, BoxGroup("Delivery")]
    [LabelText("Launch Anchor")]
    private DeliveryLaunchAnchorMode launchAnchorMode = DeliveryLaunchAnchorMode.CastOrigin;

    [SerializeField, BoxGroup("Delivery")]
    [LabelText("Anchor Child Path")]
    [ShowIf(nameof(launchAnchorMode), DeliveryLaunchAnchorMode.ChildPath)]
    private string launchAnchorChildPath;

    [SerializeField, BoxGroup("Delivery")]
    [LabelText("Anchor Bone")]
    [ShowIf(nameof(launchAnchorMode), DeliveryLaunchAnchorMode.HumanoidBone)]
    private HumanBodyBones launchAnchorBone = HumanBodyBones.RightHand;

    [Header("Flight")]
    [SerializeField, BoxGroup("Flight")]
    [LabelText("Speed (units/sec)"), MinValue(0.01f)]
    private float speed = 12f;

    [SerializeField, BoxGroup("Flight")]
    [LabelText("Min Duration"), MinValue(0f)]
    private float minFlightDuration = 0.15f;

    [SerializeField, BoxGroup("Flight")]
    [LabelText("Max Duration"), MinValue(0.01f)]
    private float maxFlightDuration = 1.5f;

    [SerializeField, BoxGroup("Flight")]
    [LabelText("Arc Height"), MinValue(0f)]
    private float arcHeight = 1.5f;

    [SerializeField, BoxGroup("Flight")]
    [LabelText("Arrival Clearance"), MinValue(0f)]
    [Tooltip("Extra height above the target's bounds where the delivery finishes.")]
    private float arrivalClearance = 0.35f;

    [SerializeField, BoxGroup("Flight")]
    [LabelText("Spin (deg/sec)")]
    private Vector3 spinDegreesPerSecond = new Vector3(0f, 360f, 180f);

    [SerializeField, BoxGroup("Flight")]
    [LabelText("If Target Is Lost")]
    private DeliveryTargetInvalidPolicy targetInvalidPolicy = DeliveryTargetInvalidPolicy.PresentAtLastKnownPoint;

    [Header("Arrival")]
    [SerializeField, BoxGroup("Arrival")]
    [LabelText("Heal Mode")]
    private DeliveryHealMode healMode = DeliveryHealMode.None;

    [SerializeField, BoxGroup("Arrival")]
    [LabelText("Heal Amount"), MinValue(0f)]
    [ShowIf(nameof(healMode), DeliveryHealMode.Flat)]
    [Tooltip("Leave at 0 to use the skill's Heal Power stat instead.")]
    private float healFlatAmount;

    [SerializeField, BoxGroup("Arrival")]
    [LabelText("Heal Fraction of Max HP"), Range(0f, 1f)]
    [ShowIf(nameof(healMode), DeliveryHealMode.PercentMaxHealth)]
    private float healMaxHealthFraction = 0.25f;

    [SerializeField, BoxGroup("Arrival")]
    [LabelText("Status Effects")]
    [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
    private List<StatusApplication> statusSpecApplications = new();

    [SerializeField, BoxGroup("Upgrades")]
    [LabelText("Conditional Status Effects")]
    [SkillStatusRouteTarget(nameof(ResolvedConditionalStatusTarget), "Targeted Delivery")]
    private ConditionalStatusRoute conditionalStatuses = new();

    [Header("Impact Feedback")]
    [SerializeField, BoxGroup("Feedback")]
    [LabelText("Impact VFX")]
    private GameObject impactVfxPrefab;

    [SerializeField, BoxGroup("Feedback")]
    [LabelText("Impact VFX Lifetime"), MinValue(0f)]
    private float impactVfxLifetime = 3f;

    [SerializeField, BoxGroup("Feedback")]
    [LabelText("Impact Audio")]
    private AudioClip impactAudio;

    [SerializeField, BoxGroup("Feedback")]
    [LabelText("Impact Audio Volume"), Range(0f, 1f)]
    private float impactAudioVolume = 1f;

    public GameObject DeliveryPrefab => deliveryPrefab;
    public float TargetStandOffDistanceAtCastPoint => targetStandOffDistanceAtCastPoint;
    public DeliveryLaunchAnchorMode LaunchAnchorMode => launchAnchorMode;
    public string LaunchAnchorChildPath => launchAnchorChildPath;
    public HumanBodyBones LaunchAnchorBone => launchAnchorBone;
    public float Speed => speed;
    public float MinFlightDuration => minFlightDuration;
    public float MaxFlightDuration => maxFlightDuration;
    public float ArcHeight => arcHeight;
    public float ArrivalClearance => arrivalClearance;
    public Vector3 SpinDegreesPerSecond => spinDegreesPerSecond;
    public DeliveryTargetInvalidPolicy TargetInvalidPolicy => targetInvalidPolicy;
    public DeliveryHealMode HealMode => healMode;
    public IReadOnlyList<StatusApplication> StatusSpecApplications => statusSpecApplications;
    public ConditionalStatusRoute ConditionalStatuses => conditionalStatuses;
    public GameObject ImpactVfxPrefab => impactVfxPrefab;
    public float ImpactVfxLifetime => impactVfxLifetime;
    public AudioClip ImpactAudio => impactAudio;
    public float ImpactAudioVolume => impactAudioVolume;

    /// <summary>Deliveries always land on a friendly recipient, so routes resolve against allies.</summary>
    private SkillStatusTarget ResolvedConditionalStatusTarget => SkillStatusTarget.Allies;

    // The whole point of this payload is that the object is in hand first and leaves later, so a
    // clip without the release marker would strand it there.
    public override bool RequiresSkillTimelineEvents => true;

    public override bool TryGetTargetPlacement(out SkillTargetPlacementSpec placement)
    {
        placement = new SkillTargetPlacementSpec(targetStandOffDistanceAtCastPoint);
        return targetStandOffDistanceAtCastPoint > 0f;
    }

    public override void CollectTimelineEventNames(List<CombatTimelineEventName> eventNames)
    {
        CombatTimelineEventNames.AddUnique(eventNames, CombatTimelineEventName.DeliveryRelease);
    }

    public override void CollectUpgradeIds(List<string> ids)
    {
        conditionalStatuses?.CollectUpgradeIds(ids);
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            return;

        if (deliveryPrefab == null)
        {
            issues.Add("Targeted Delivery has no Delivery Prefab.");
        }
        else
        {
            // A delivery is presentation only: it is moved by this runtime, never by physics, and
            // it must not push, trigger, or be blocked by anything on the way to its target.
            if (deliveryPrefab.GetComponentInChildren<Rigidbody>(true) != null)
                issues.Add("Delivery Prefab has a Rigidbody. Deliveries are moved by the runtime and must not use physics.");

            if (deliveryPrefab.GetComponentInChildren<Collider>(true) != null)
                issues.Add("Delivery Prefab has a Collider. Deliveries must not collide with anything in flight.");
        }

        if (targetStandOffDistanceAtCastPoint <= 0f)
            issues.Add("Targeted Delivery target stand-off distance at cast point must be greater than zero.");

        if (speed <= 0f)
            issues.Add("Targeted Delivery speed must be greater than zero.");

        if (maxFlightDuration <= 0f)
            issues.Add("Targeted Delivery max flight duration must be greater than zero.");

        if (minFlightDuration < 0f)
            issues.Add("Targeted Delivery min flight duration cannot be negative.");

        if (minFlightDuration > maxFlightDuration)
            issues.Add("Targeted Delivery min flight duration is greater than its max flight duration.");

        if (healMode == DeliveryHealMode.PercentMaxHealth && healMaxHealthFraction <= 0f)
            issues.Add("Targeted Delivery heals a percent of Max HP but the fraction is zero.");

        if (healMode == DeliveryHealMode.Flat && healFlatAmount < 0f)
            issues.Add("Targeted Delivery flat heal amount cannot be negative.");

        if (launchAnchorMode == DeliveryLaunchAnchorMode.ChildPath && string.IsNullOrWhiteSpace(launchAnchorChildPath))
            issues.Add("Targeted Delivery anchor mode is Child Path but no path is set.");

        if (statusSpecApplications != null)
        {
            for (int i = 0; i < statusSpecApplications.Count; i++)
            {
                StatusApplicationSpec spec = statusSpecApplications[i]?.spec;
                if (spec == null)
                    continue;

                if (spec.effect == null)
                    issues.Add($"statusSpecApplications[{i}] has no Status Effect assigned.");
                else
                    spec.CollectValidationIssues(issues, $"statusSpecApplications[{i}]");
            }
        }

        conditionalStatuses?.CollectValidationIssues(issues, "conditionalStatuses");
    }

    /// <summary>Health this delivery restores to <paramref name="target"/>, given the cast's stats.</summary>
    public float ResolveHealAmount(HealthSystem target, FinalSkillStats stats)
    {
        switch (healMode)
        {
            case DeliveryHealMode.Flat:
                // Authoring a flat 0 means "use whatever the skill's stats say", which is how the
                // rest of the skill system treats an unset heal value.
                return healFlatAmount > 0f
                    ? healFlatAmount
                    : (stats != null ? stats.healPower : 0f);

            case DeliveryHealMode.PercentMaxHealth:
                return target != null ? target.maximumHealth * healMaxHealthFraction : 0f;

            default:
                return 0f;
        }
    }

    public override SkillExecutionResult ExecuteWithResult(SkillCastContext context)
    {
        if (context == null || context.CasterObject == null)
        {
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingRuntimeContext,
                "Targeted delivery executed without a caster object.");
        }

        // No target at all is broken wiring, not a miss: whoever started this cast never told the
        // payload who it was for, so the cast must refund rather than burn a cooldown on nothing.
        // A target that was locked and has since died is a different case entirely and is handled
        // by the runtime on arrival.
        if (!context.HasPrimaryTarget)
        {
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingRuntimeContext,
                "Targeted delivery executed without a locked target.");
        }

        if (deliveryPrefab == null)
        {
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingAuthoringData,
                "Targeted delivery has no delivery prefab.");
        }

        if (context.AnimBrain == null)
        {
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingRuntimeContext,
                "Targeted delivery needs an animation brain to receive DeliveryRelease.");
        }

        var host = new GameObject("TargetedDeliveryRuntime");
        host.transform.SetParent(null);

        TargetedDeliveryRuntime runtime = host.AddComponent<TargetedDeliveryRuntime>();
        if (runtime == null)
        {
            Destroy(host);
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingRuntimeContext,
                "Targeted delivery could not create its runtime host.");
        }

        // Only report success once the object exists and every subscription is live. The cast
        // transaction commits on this result, so returning true earlier would stamp a cooldown
        // for a delivery that could never arrive.
        if (!runtime.Initialize(context, this))
        {
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingRuntimeContext,
                "Targeted delivery runtime could not arm.");
        }

        return SkillExecutionResult.Succeeded;
    }
}
