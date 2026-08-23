using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

/// <summary>
/// Reads the combat timeline markers actually authored on a skill's clip.
///
/// Nothing in the project checks that a clip really raises the markers its payload depends on -
/// a payload can declare <c>RequiresSkillTimelineEvents</c>, the cast can be accepted, and the
/// marker can simply never fire. This gives descriptors a way to say so at authoring time instead
/// of leaving it to be discovered in Play Mode.
/// </summary>
public static class SkillTimelineMarkerAudit
{
    public readonly struct Marker
    {
        public Marker(CombatTimelineEventName eventName, string rawName, float normalizedTime)
        {
            EventName = eventName;
            RawName = rawName;
            NormalizedTime = normalizedTime;
        }

        public CombatTimelineEventName EventName { get; }
        public string RawName { get; }
        public float NormalizedTime { get; }
    }

    /// <summary>True when the skill has a clip that can carry markers at all.</summary>
    public static bool HasClip(SkillGemDefinition skill)
    {
        ClipTransition transition = skill != null ? skill.skillClip : null;
        return transition != null && transition.IsValid && transition.Clip != null;
    }

    /// <summary>
    /// Every marker on the skill's clip, in authored order. Empty when there is no clip.
    /// </summary>
    public static List<Marker> ReadMarkers(SkillGemDefinition skill)
    {
        var markers = new List<Marker>();

        ClipTransition transition = skill != null ? skill.skillClip : null;
        AnimancerEvent.Sequence.Serializable events = transition?.SerializedEvents;
        float[] times = events?.NormalizedTimes;

        // Animancer keeps a trailing end-event entry that is not an authored marker, so the last
        // slot is deliberately skipped - the same rule the VFX timeline window uses.
        if (times == null || times.Length <= 1)
            return markers;

        StringAsset[] names = events.Names;
        AnimancerEvent.Sequence runtimeEvents = transition.Events;

        for (int i = 0; i < times.Length - 1; i++)
        {
            if (!float.IsFinite(times[i]))
                continue;

            string rawName = names != null && i < names.Length && names[i] != null
                ? names[i].name
                : runtimeEvents.GetName(i)?.String;

            if (string.IsNullOrWhiteSpace(rawName))
                continue;

            Enum.TryParse(rawName, true, out CombatTimelineEventName eventName);
            markers.Add(new Marker(eventName, rawName, Mathf.Clamp01(times[i])));
        }

        return markers;
    }

    /// <summary>Markers on the clip matching one event name.</summary>
    public static List<Marker> ReadMarkers(SkillGemDefinition skill, CombatTimelineEventName eventName)
    {
        var matches = new List<Marker>();
        foreach (Marker marker in ReadMarkers(skill))
        {
            if (marker.EventName == eventName)
                matches.Add(marker);
        }

        return matches;
    }
}
