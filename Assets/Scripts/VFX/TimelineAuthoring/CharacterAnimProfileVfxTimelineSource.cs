#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Animancer;
using UnityEditor;
using UnityEngine;

public sealed class CharacterAnimProfileVfxTimelineSource : IAnimationVfxTimelineSource
{
    static readonly AnimationVfxTimelineLane[] NoLanes = Array.Empty<AnimationVfxTimelineLane>();

    readonly CharacterAnimProfileSO profile;
    readonly string entryId;
    ClipTransition transition;
    AnimationVfxTrack track;
    bool validEntry;

    public CharacterAnimProfileVfxTimelineSource(CharacterAnimProfileSO profile, string entryId)
    {
        this.profile = profile;
        this.entryId = entryId;
        RefreshEntry();
    }

    public ScriptableObject SourceAsset => profile;
    public string EntryId => entryId;
    public string DisplayName => profile != null
        ? $"{profile.name} / {GetEntryLabel(entryId, transition)}"
        : "Character Animation Profile";
    public ClipTransition Transition => transition;
    public int MarkerCount => AnimationVfxTimelineSourceFactory.CountMarkers(transition);
    public IReadOnlyList<AnimationVfxTimelineLane> Lanes => NoLanes;
    public float PointValue => 0f;
    public Vector2 RangeValue => default;
    public int CueCount => track?.CueCount ?? 0;
    public IAnimationVfxCue GetCue(int index) => track?.GetCue(index);

    public void SetPointValue(float value) { }
    public void SetRangeValue(Vector2 value) { }

    public void ReplaceCues(IReadOnlyList<AnimationVfxCue> cues)
    {
        if (profile == null || !validEntry)
            return;

        Undo.RecordObject(profile, "Save Character Animation VFX");
        var replacement = new AnimationVfxTrack();
        replacement.ReplaceCues(cues);
        profile.ReplaceAnimationVfxTrack(entryId, replacement);
        track = replacement;
        EditorUtility.SetDirty(profile);
    }

    public void MoveCueIndex(int oldCueIndex, int newCueIndex)
    {
        if (profile == null || track == null || !validEntry)
            return;

        Undo.RecordObject(profile, "Move Character Animation VFX Cue");
        track.MoveCueIndex(oldCueIndex, newCueIndex);
        profile.ReplaceAnimationVfxTrack(entryId, track);
        EditorUtility.SetDirty(profile);
    }

    public void RemoveCueIndex(int cueIndex)
    {
        if (profile == null || track == null || !validEntry)
            return;

        Undo.RecordObject(profile, "Remove Character Animation VFX Cue");
        track.RemoveCueIndex(cueIndex);
        profile.ReplaceAnimationVfxTrack(entryId, track);
        EditorUtility.SetDirty(profile);
    }

    public void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            return;

        if (!validEntry)
        {
            issues.Add("The selected Character Animation Profile entry no longer exists.");
            return;
        }

        if (transition == null || !transition.IsValid)
            issues.Add($"{GetEntryLabel(entryId, transition)} requires a valid ClipTransition.");
    }

    public void Save()
    {
        if (profile == null)
            return;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
    }

    internal static string GetEntryLabel(string entryId, ClipTransition transition)
    {
        string kind = entryId switch
        {
            CharacterAnimProfileSO.DashForwardVfxEntryId => "Dash Forward",
            CharacterAnimProfileSO.DashBackwardVfxEntryId => "Dash Backward",
            CharacterAnimProfileSO.ReloadVfxEntryId => "Reload",
            _ => "Unknown Entry",
        };
        string clipName = transition != null && transition.Clip != null
            ? transition.Clip.name
            : "No Clip";
        return $"{kind}: {clipName}";
    }

    void RefreshEntry()
    {
        validEntry = profile != null && profile.TryGetAnimationVfxEntry(entryId, out transition, out track);
    }
}
#endif
