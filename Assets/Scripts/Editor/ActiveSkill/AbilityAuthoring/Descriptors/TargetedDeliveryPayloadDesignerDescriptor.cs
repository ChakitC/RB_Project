using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Descriptor for TargetedDeliverySkillPayloadDef. See PayloadDesignerDescriptorBase for the contract.
internal sealed class TargetedDeliveryPayloadDesignerDescriptor
    : PayloadDesignerDescriptorBase<TargetedDeliverySkillPayloadDef>
{
    public override string DisplayName => "Targeted Delivery";

    public override string Description =>
        "Carries an object from the caster's hand to the character this cast was aimed at, then heals " +
        "and buffs them on arrival. The clip must raise DeliveryRelease after its cast point.";

    public override string Category => "Support";

    protected override void ApplySafeDefaults(
        TargetedDeliverySkillPayloadDef payload, PayloadDesignerContext context)
    {
        // Field initializers already give speed, durations, and arc sensible values. The delivery
        // prefab is the one thing that cannot be fabricated, so it stays blank and is reported as
        // an error rather than being filled with something that would silently look wrong in game.
    }

    protected override void DrawWizard(
        TargetedDeliverySkillPayloadDef payload, PayloadDesignerContext context)
    {
        var serialized = new SerializedObject(payload);
        serialized.Update();

        EditorGUILayout.HelpBox(
            "This payload needs a target. Whoever starts the cast supplies it as a SkillTargetHandle - " +
            "a cast with no target fails and refunds instead of burning a cooldown.",
            MessageType.Info);

        EditorGUILayout.LabelField("Caster Placement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("targetStandOffDistanceAtCastPoint"));
        EditorGUILayout.HelpBox(
            "This is the caster-to-target distance at the skill cast point. Root motion determines " +
            "where the animation must start; this value does not scale the clip.",
            MessageType.None);

        EditorGUILayout.LabelField("Delivery Object", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("deliveryPrefab"));
        EditorGUILayout.HelpBox(
            "Presentation only. A Rigidbody or Collider on the prefab is an error - the runtime moves " +
            "the object itself and it must not collide with anything in flight.",
            MessageType.None);

        SerializedProperty anchorMode = serialized.FindProperty("launchAnchorMode");
        EditorGUILayout.PropertyField(anchorMode);

        switch ((DeliveryLaunchAnchorMode)anchorMode.enumValueIndex)
        {
            case DeliveryLaunchAnchorMode.ChildPath:
                EditorGUILayout.PropertyField(serialized.FindProperty("launchAnchorChildPath"));
                break;

            case DeliveryLaunchAnchorMode.HumanoidBone:
                EditorGUILayout.PropertyField(serialized.FindProperty("launchAnchorBone"));
                break;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Flight", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("speed"));
        EditorGUILayout.PropertyField(serialized.FindProperty("minFlightDuration"));
        EditorGUILayout.PropertyField(serialized.FindProperty("maxFlightDuration"));
        EditorGUILayout.PropertyField(serialized.FindProperty("arcHeight"));
        EditorGUILayout.PropertyField(serialized.FindProperty("arrivalClearance"));
        EditorGUILayout.PropertyField(serialized.FindProperty("spinDegreesPerSecond"));
        EditorGUILayout.PropertyField(serialized.FindProperty("targetInvalidPolicy"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Arrival", EditorStyles.boldLabel);

        SerializedProperty healMode = serialized.FindProperty("healMode");
        EditorGUILayout.PropertyField(healMode);

        switch ((DeliveryHealMode)healMode.enumValueIndex)
        {
            case DeliveryHealMode.Flat:
                EditorGUILayout.PropertyField(serialized.FindProperty("healFlatAmount"));
                break;

            case DeliveryHealMode.PercentMaxHealth:
                EditorGUILayout.PropertyField(serialized.FindProperty("healMaxHealthFraction"));
                break;
        }

        EditorGUILayout.PropertyField(serialized.FindProperty("statusSpecApplications"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Conditional Status Effects", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("conditionalStatuses"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Impact Feedback", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("impactVfxPrefab"));
        EditorGUILayout.PropertyField(serialized.FindProperty("impactVfxLifetime"));
        EditorGUILayout.PropertyField(serialized.FindProperty("impactAudio"));
        EditorGUILayout.PropertyField(serialized.FindProperty("impactAudioVolume"));

        serialized.ApplyModifiedProperties();
    }

    protected override PayloadGameplaySummary BuildSummary(
        TargetedDeliverySkillPayloadDef payload, PayloadDesignerContext context)
    {
        string heal = payload.HealMode switch
        {
            DeliveryHealMode.PercentMaxHealth => "heals a percent of their Max HP",
            DeliveryHealMode.Flat => "heals a flat amount",
            _ => "does not heal",
        };

        var summary = PayloadGameplaySummary.Of(
            $"Throws an object at the cast's locked target and {heal} on arrival.");

        summary.AddDetail(
            $"Flight time is clamped to {payload.MinFlightDuration:0.##}s - {payload.MaxFlightDuration:0.##}s " +
            $"at {payload.Speed:0.#} units/sec.");
        summary.AddDetail(
            $"Caster stands {payload.TargetStandOffDistanceAtCastPoint:0.##}m from the target at the cast point.");

        if (payload.DeliveryPrefab == null)
            summary.AddWarning("No delivery prefab assigned yet -- nothing will be thrown.");

        int statusCount = payload.StatusSpecApplications?.Count ?? 0;
        if (statusCount > 0)
            summary.AddDetail($"Applies {statusCount} status effect(s) when it lands.");

        summary.AddDetail(
            payload.TargetInvalidPolicy == DeliveryTargetInvalidPolicy.PresentAtLastKnownPoint
                ? "If the target dies mid-flight the object still lands, but nothing is applied."
                : "If the target dies mid-flight the object vanishes immediately.");

        int conditionalCount = payload.ConditionalStatuses?.Applications.Count ?? 0;
        if (conditionalCount > 0)
            summary.AddDetail($"{conditionalCount} additional status effect(s) unlock via upgrades.");

        return summary;
    }

    protected override void CollectAuthoringIssues(
        TargetedDeliverySkillPayloadDef payload,
        PayloadDesignerContext context,
        List<PayloadAuthoringIssue> issues)
    {
        CollectRuntimeValidationIssuesAsErrors(payload, issues);
        CollectReleaseMarkerIssues(context?.Owner, issues);
    }

    /// <summary>
    /// The clip contract. Without these checks a delivery can be authored, saved, and cast, and the
    /// only symptom is an object that appears in the caster's hand and is quietly destroyed - after
    /// the cooldown has already been spent.
    /// </summary>
    static void CollectReleaseMarkerIssues(SkillGemDefinition owner, List<PayloadAuthoringIssue> issues)
    {
        if (owner == null)
        {
            issues.Add(PayloadAuthoringIssue.Info(
                "Assign this payload to a skill to check its DeliveryRelease marker."));
            return;
        }

        if (!SkillTimelineMarkerAudit.HasClip(owner))
        {
            issues.Add(PayloadAuthoringIssue.Error(
                $"'{owner.name}' has no animation clip, so it can never raise DeliveryRelease. " +
                "The delivery would be created at the cast point and then destroyed without being thrown."));
            return;
        }

        List<SkillTimelineMarkerAudit.Marker> markers =
            SkillTimelineMarkerAudit.ReadMarkers(owner, CombatTimelineEventName.DeliveryRelease);

        if (markers.Count == 0)
        {
            issues.Add(PayloadAuthoringIssue.Error(
                $"The clip on '{owner.name}' has no DeliveryRelease marker. Add one after the cast point " +
                $"({owner.castPointNormalized:0.###}) so the object is in hand before it detaches."));
            return;
        }

        if (markers.Count > 1)
        {
            issues.Add(PayloadAuthoringIssue.Error(
                $"The clip on '{owner.name}' has {markers.Count} DeliveryRelease markers. " +
                "Only the first is ever acted on; the rest are silently ignored."));
        }

        // Releasing at or before the cast point means throwing the object before the payload has
        // created it, so the marker would fire into an unarmed runtime.
        const float Epsilon = 0.0001f;
        SkillTimelineMarkerAudit.Marker first = markers[0];

        if (first.NormalizedTime <= owner.castPointNormalized + Epsilon)
        {
            issues.Add(PayloadAuthoringIssue.Error(
                $"DeliveryRelease is at {first.NormalizedTime:0.###}, which is not after the cast point " +
                $"({owner.castPointNormalized:0.###}). The object does not exist yet at that moment."));
        }
    }
}
