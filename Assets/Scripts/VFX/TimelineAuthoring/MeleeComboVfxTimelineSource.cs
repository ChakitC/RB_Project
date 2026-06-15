#if UNITY_EDITOR
using System.Collections.Generic;
using Animancer;
using UnityEditor;
using UnityEngine;

public sealed class MeleeComboVfxTimelineSource : IAnimationVfxTimelineSource
{
    static readonly AnimationVfxTimelineLane[] MeleeLanes =
    {
        new AnimationVfxTimelineLane(
            "Hitbox",
            AnimationVfxTimelineLaneKind.Events,
            new[] { CombatTimelineEventName.HitStart, CombatTimelineEventName.HitEnd },
            allowDuplicateEvents: true),
        new AnimationVfxTimelineLane("Chain Window", AnimationVfxTimelineLaneKind.Range),
    };

    readonly MeleeComboSO combo;
    readonly string entryId;
    MeleeComboSO.Step step;
    int stepIndex;
    AnimationVfxTrack track;

    public MeleeComboVfxTimelineSource(MeleeComboSO combo, string entryId)
    {
        this.combo = combo;
        this.entryId = entryId;
        if (combo != null && combo.TryGetStep(entryId, out step, out stepIndex))
            track = step.AnimationVfxTrack ?? new AnimationVfxTrack();
        else
            stepIndex = -1;
    }

    public ScriptableObject SourceAsset => combo;
    public string EntryId => entryId;
    public string DisplayName
    {
        get
        {
            string clipName = step.clip != null && step.clip.Clip != null ? step.clip.Clip.name : "No Clip";
            return stepIndex >= 0 ? $"{combo.name} / Step {stepIndex + 1}: {clipName}" : combo != null ? combo.name : "Melee Combo";
        }
    }
    public ClipTransition Transition => step.clip;
    public int MarkerCount => AnimationVfxTimelineSourceFactory.CountMarkers(step.clip);
    public IReadOnlyList<AnimationVfxTimelineLane> Lanes => MeleeLanes;
    public float PointValue => 0f;
    public Vector2 RangeValue => step.chainWindowN;
    public int CueCount => track?.CueCount ?? 0;
    public IAnimationVfxCue GetCue(int index) => track?.GetCue(index);

    public void SetPointValue(float value) { }

    public void SetRangeValue(Vector2 value)
    {
        if (combo == null || stepIndex < 0)
            return;
        Undo.RecordObject(combo, "Move Melee Chain Window");
        combo.SetStepChainWindow(entryId, value);
        combo.TryGetStep(entryId, out step, out stepIndex);
        EditorUtility.SetDirty(combo);
    }

    public void ReplaceCues(IReadOnlyList<AnimationVfxCue> cues)
    {
        if (combo == null || stepIndex < 0)
            return;
        Undo.RecordObject(combo, "Save Melee Animation VFX");
        var replacement = new AnimationVfxTrack();
        replacement.ReplaceCues(cues);
        combo.ReplaceStepVfxTrack(entryId, replacement);
        track = replacement;
        combo.TryGetStep(entryId, out step, out stepIndex);
        EditorUtility.SetDirty(combo);
    }

    public void MoveCueIndex(int oldCueIndex, int newCueIndex)
    {
        if (track == null)
            return;
        Undo.RecordObject(combo, "Move Melee VFX Cue");
        track.MoveCueIndex(oldCueIndex, newCueIndex);
        combo.ReplaceStepVfxTrack(entryId, track);
        EditorUtility.SetDirty(combo);
    }

    public void RemoveCueIndex(int cueIndex)
    {
        if (track == null)
            return;
        Undo.RecordObject(combo, "Remove Melee VFX Cue");
        track.RemoveCueIndex(cueIndex);
        combo.ReplaceStepVfxTrack(entryId, track);
        EditorUtility.SetDirty(combo);
    }

    public void CollectValidationIssues(List<string> issues)
    {
        if (stepIndex < 0)
            issues?.Add("The selected Melee Combo step no longer exists.");
    }

    public void Save()
    {
        if (combo == null)
            return;
        EditorUtility.SetDirty(combo);
        AssetDatabase.SaveAssets();
    }
}
#endif
