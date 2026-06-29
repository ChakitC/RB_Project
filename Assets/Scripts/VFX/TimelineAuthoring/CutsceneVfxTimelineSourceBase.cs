#if UNITY_EDITOR
using System.Collections.Generic;
using Animancer;
using UnityEditor;
using UnityEngine;

// Shared timeline-source implementation for any asset that stores a CutsceneDef.
// Subclasses only supply the owning asset (to record/dirty) and how to reach its
// CutsceneDef; everything else (cue load/save, markers, transition) is identical.
public abstract class CutsceneVfxTimelineSourceBase : IAnimationVfxTimelineSource
{
    protected readonly List<AnimationVfxCue> _cues = new List<AnimationVfxCue>();

    /// <summary>The ScriptableObject asset that owns the CutsceneDef (recorded for Undo / dirtied on save).</summary>
    protected abstract ScriptableObject Owner { get; }

    /// <summary>The CutsceneDef instance embedded in <see cref="Owner"/>.</summary>
    protected abstract CutsceneDef Cutscene { get; }

    /// <summary>Call from the subclass constructor after its asset field is assigned.</summary>
    protected void LoadSavedCues() => ReloadCues(Cutscene?.cutsceneVfxEvents);

    public ScriptableObject SourceAsset => Owner;
    public string EntryId => "cutscene";
    public string DisplayName => Owner != null ? $"{Owner.name} (Cutscene)" : "Cutscene";
    public ClipTransition Transition => Cutscene?.characterCutsceneClip;
    public int MarkerCount => Cutscene != null ? Cutscene.GetCutsceneVfxMarkerCount() : 0;
    public IReadOnlyList<AnimationVfxTimelineLane> Lanes => System.Array.Empty<AnimationVfxTimelineLane>();
    public float PointValue => 0f;
    public Vector2 RangeValue => default;
    public int CueCount => _cues.Count;
    public IAnimationVfxCue GetCue(int index) => index >= 0 && index < _cues.Count ? _cues[index] : null;

    public void SetPointValue(float value) { }
    public void SetRangeValue(Vector2 value) { }

    public void ReplaceCues(IReadOnlyList<AnimationVfxCue> cues)
    {
        if (Owner == null || Cutscene == null)
            return;
        Undo.RecordObject(Owner, "Save Cutscene VFX Data");
        Cutscene.ReplaceCutsceneVfxEvents(cues);
        ReloadCues(cues);
        EditorUtility.SetDirty(Owner);
    }

    public void MoveCueIndex(int oldCueIndex, int newCueIndex)
    {
        AnimationVfxTimelineSourceFactory.RemapCueIndices(_cues, oldCueIndex, newCueIndex);
        ReplaceCues(_cues);
    }

    public void RemoveCueIndex(int cueIndex)
    {
        var track = new AnimationVfxTrack();
        track.ReplaceCues(_cues);
        track.RemoveCueIndex(cueIndex);
        ReplaceCues(track.Cues);
    }

    public void CollectValidationIssues(List<string> issues) { }

    public void Save()
    {
        if (Owner == null)
            return;
        EditorUtility.SetDirty(Owner);
        AssetDatabase.SaveAssets();
    }

    void ReloadCues(IReadOnlyList<AnimationVfxCue> values)
    {
        _cues.Clear();
        if (values == null)
            return;
        for (int i = 0; i < values.Count; i++)
        {
            AnimationVfxCue cue = AnimationVfxTimelineSourceFactory.CopyCue(values[i]);
            if (cue != null)
                _cues.Add(cue);
        }
    }
}
#endif
