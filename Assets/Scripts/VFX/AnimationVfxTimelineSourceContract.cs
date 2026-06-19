using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

public enum AnimationVfxTimelineLaneKind
{
    Point,
    Events,
    Range,
}

public readonly struct AnimationVfxTimelineEntry
{
    public string Id { get; }
    public string DisplayName { get; }

    public AnimationVfxTimelineEntry(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }
}

public sealed class AnimationVfxTimelineLane
{
    public string Label { get; }
    public AnimationVfxTimelineLaneKind Kind { get; }
    public IReadOnlyList<CombatTimelineEventName> EventNames { get; }
    public bool AllowDuplicateEvents { get; }
    public bool ReadOnly { get; }

    public AnimationVfxTimelineLane(
        string label,
        AnimationVfxTimelineLaneKind kind,
        IReadOnlyList<CombatTimelineEventName> eventNames = null,
        bool allowDuplicateEvents = false,
        bool readOnly = false)
    {
        Label = label;
        Kind = kind;
        EventNames = eventNames ?? Array.Empty<CombatTimelineEventName>();
        AllowDuplicateEvents = allowDuplicateEvents;
        ReadOnly = readOnly;
    }
}

public interface IAnimationVfxTimelineMultiMode
{
    string SecondaryModeLabel { get; }
    bool IsSecondaryMode { get; }
    void SetMode(bool secondary);
    ClipTransition ReferenceTransition { get; }
    IAnimationVfxCueSource ReferenceCueSource { get; }
}

public interface IAnimationVfxTimelineSource : IAnimationVfxCueSource
{
    ScriptableObject SourceAsset { get; }
    string EntryId { get; }
    string DisplayName { get; }
    ClipTransition Transition { get; }
    int MarkerCount { get; }
    IReadOnlyList<AnimationVfxTimelineLane> Lanes { get; }
    float PointValue { get; }
    Vector2 RangeValue { get; }

    void SetPointValue(float value);
    void SetRangeValue(Vector2 value);
    void ReplaceCues(IReadOnlyList<AnimationVfxCue> cues);
    void MoveCueIndex(int oldCueIndex, int newCueIndex);
    void RemoveCueIndex(int cueIndex);
    void CollectValidationIssues(List<string> issues);
    void Save();
}
