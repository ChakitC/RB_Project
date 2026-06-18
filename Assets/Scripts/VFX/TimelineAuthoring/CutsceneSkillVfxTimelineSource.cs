#if UNITY_EDITOR
using System.Collections.Generic;
using Animancer;
using UnityEditor;
using UnityEngine;

public sealed class CutsceneSkillVfxTimelineSource : IAnimationVfxTimelineSource
{
    readonly SkillGemDefinition _skill;
    readonly List<AnimationVfxCue> _cues = new List<AnimationVfxCue>();

    public CutsceneSkillVfxTimelineSource(SkillGemDefinition skill)
    {
        _skill = skill;
        List<AnimationVfxCue> saved = skill?.CutsceneDef?.cutsceneVfxEvents;
        if (saved == null)
            return;
        for (int i = 0; i < saved.Count; i++)
        {
            AnimationVfxCue cue = AnimationVfxTimelineSourceFactory.CopyCue(saved[i]);
            if (cue != null)
                _cues.Add(cue);
        }
    }

    public ScriptableObject SourceAsset => _skill;
    public string EntryId => "cutscene";
    public string DisplayName => _skill != null ? $"{_skill.name} (Cutscene)" : "Cutscene";
    public ClipTransition Transition => _skill?.CutsceneDef?.characterCutsceneClip;
    public int MarkerCount => _skill?.CutsceneDef != null ? _skill.CutsceneDef.GetCutsceneVfxMarkerCount() : 0;
    public IReadOnlyList<AnimationVfxTimelineLane> Lanes => System.Array.Empty<AnimationVfxTimelineLane>();
    public float PointValue => 0f;
    public Vector2 RangeValue => default;
    public int CueCount => _cues.Count;
    public IAnimationVfxCue GetCue(int index) => index >= 0 && index < _cues.Count ? _cues[index] : null;

    public void SetPointValue(float value) { }
    public void SetRangeValue(Vector2 value) { }

    public void ReplaceCues(IReadOnlyList<AnimationVfxCue> cues)
    {
        if (_skill == null)
            return;
        Undo.RecordObject(_skill, "Save Cutscene VFX Data");
        _skill.CutsceneDef.ReplaceCutsceneVfxEvents(cues);
        ReloadCues(cues);
        EditorUtility.SetDirty(_skill);
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
        if (_skill == null)
            return;
        EditorUtility.SetDirty(_skill);
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
