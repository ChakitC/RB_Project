using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;

public partial class SkillGemDefinition
{
    [FoldoutGroup("Presentation/Target Placement"), LabelText("Placement Timing")]
    [InfoBox("HitStart is missing. Placement will use Placement Point / HitStart Fallback, independently of Cast Point.",
        InfoMessageType.Warning, VisibleIf = nameof(IsPlacementHitStartMissing))]
    [Tooltip("Selects the pose used to align this skill with its target. Does not change payload release, Hitbox events, or Chain continuation. Legacy preserves each caller's original timing.")]
    public SkillPlacementTimingSource placementTimingSource = SkillPlacementTimingSource.Legacy;

    [FoldoutGroup("Presentation/Target Placement"), LabelText("Placement Point / HitStart Fallback")]
    [ShowIf("@placementTimingSource != SkillPlacementTimingSource.Legacy")]
    [Range(0f, 1f), SuffixLabel("normalized")]
    [Tooltip("Custom placement time, or fallback if HitStart is selected but the clip has no valid HitStart marker. Independent of Cast Point.")]
    public float placementPointNormalized = 0.35f;

    bool IsPlacementHitStartMissing =>
        placementTimingSource == SkillPlacementTimingSource.HitStart && !TryGetPlacementHitStart(out _);

    public float GetPlacementPointNormalized(bool preferHitStartForLegacy = false)
    {
        bool useHitStart = placementTimingSource == SkillPlacementTimingSource.HitStart ||
            (placementTimingSource == SkillPlacementTimingSource.Legacy && preferHitStartForLegacy);
        if (useHitStart && TryGetPlacementHitStart(out float hitStart))
            return hitStart;

        if (placementTimingSource == SkillPlacementTimingSource.Legacy)
            return GetCastPointNormalized();

        return float.IsFinite(placementPointNormalized)
            ? Mathf.Clamp(placementPointNormalized, 0f, 0.999f)
            : 0.35f;
    }

    public bool TryGetPlacementHitStart(out float normalizedTime)
    {
        normalizedTime = 0f;
        AnimancerEvent.Sequence events = skillClip != null ? skillClip.Events : null;
        StringReference marker = CombatTimelineEventNames.ToStringReference(CombatTimelineEventName.HitStart);
        int index = events != null && marker != null ? events.IndexOf(marker) : -1;
        if (index < 0 || !float.IsFinite(events[index].normalizedTime))
            return false;

        normalizedTime = Mathf.Clamp(events[index].normalizedTime, 0f, 0.999f);
        return true;
    }
}
