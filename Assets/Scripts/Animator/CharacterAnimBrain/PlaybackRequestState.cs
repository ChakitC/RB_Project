using System.Collections.Generic;

internal sealed class PlaybackRequestState
{
    public SkillGemDefinition Definition;
    public int RequestId;
    public float CastPointNormalized = 0.35f;
    public bool ReleaseRequested;
    public bool Released;
    public bool UsesPlanarRootMotion;
    public bool IgnoresCharacterCollisionDuringRootMotion;
    public readonly List<CombatTimelineEventName> TimelineEventNames = new();

    public void Clear()
    {
        Definition = null;
        RequestId = 0;
        CastPointNormalized = 0.35f;
        ReleaseRequested = false;
        Released = false;
        UsesPlanarRootMotion = false;
        IgnoresCharacterCollisionDuringRootMotion = false;
        TimelineEventNames.Clear();
    }
}
