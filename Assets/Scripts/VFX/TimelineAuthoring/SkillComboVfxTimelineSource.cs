#if UNITY_EDITOR
using System.Collections.Generic;
using Animancer;
using UnityEditor;
using UnityEngine;

public sealed class SkillComboVfxTimelineSource : IAnimationVfxTimelineSource
{
    readonly SkillGemDefinition owner;
    readonly string entryId;
    readonly List<AnimationVfxTimelineLane> lanes = new();
    readonly SkillVfxTimelineSource source;
    SkillComboStep step;
    int index;
    string ownerSnapshot;
    bool chainWindowChanged;
    bool writeRejected;
    public SkillComboVfxTimelineSource(SkillGemDefinition owner, string entryId)
    {
        this.owner = owner; this.entryId = entryId;
        ownerSnapshot = owner != null ? EditorJsonUtility.ToJson(owner) : null;
        if (owner != null && owner.TryGetComboStep(entryId, out step, out index) &&
            step.executionSkill != null && !step.executionSkill.IsCombo)
        {
            source = new SkillVfxTimelineSource(step.executionSkill);
            foreach (var lane in source.Lanes)
                if (lane.Kind != AnimationVfxTimelineLaneKind.Point && lane.Label != "Pre-Cast") lanes.Add(lane);
            lanes.Add(new AnimationVfxTimelineLane("Chain Window", AnimationVfxTimelineLaneKind.Range));
        }
        else index = -1;
    }
    public ScriptableObject SourceAsset => source?.SourceAsset;
    public string EntryId => entryId;
    public string DisplayName => $"{owner?.SkillDefinitionDisplayName} / Step {index + 1}: {Transition?.Clip?.name}";
    public ClipTransition Transition => source?.Transition;
    public int MarkerCount => source?.MarkerCount ?? 0;
    public IReadOnlyList<AnimationVfxTimelineLane> Lanes => lanes;
    public float PointValue => 0f;
    public Vector2 RangeValue => step.chainWindowN;
    public int CueCount => source?.CueCount ?? 0;
    public IAnimationVfxCue GetCue(int i) => source?.GetCue(i);
    public void SetPointValue(float value) { }
    public void SetRangeValue(Vector2 value)
    {
        if (owner == null || index < 0) return;
        if (EditorJsonUtility.ToJson(owner) != ownerSnapshot)
        {
            writeRejected = true;
            Debug.LogWarning("Combo changed in another editor. Reopen the timing command before editing.");
            return;
        }
        Undo.RecordObject(owner, "Move Combo Chain Window");
        owner.SetComboChainWindow(entryId, value);
        owner.TryGetComboStep(entryId, out step, out index);
        EditorUtility.SetDirty(owner);
        chainWindowChanged = true;
        ownerSnapshot = EditorJsonUtility.ToJson(owner);
    }
    public void ReplaceCues(IReadOnlyList<AnimationVfxCue> cues) => source?.ReplaceCues(cues);
    public void MoveCueIndex(int oldIndex, int newIndex) => source?.MoveCueIndex(oldIndex, newIndex);
    public void RemoveCueIndex(int i) => source?.RemoveCueIndex(i);
    public void CollectValidationIssues(List<string> issues)
    {
        if (index < 0) issues?.Add("The selected combo step no longer exists.");
        source?.CollectValidationIssues(issues);
    }
    public void Save()
    {
        if (writeRejected) return;
        if (chainWindowChanged)
        {
            if (owner != null) AssetDatabase.SaveAssetIfDirty(owner);
            chainWindowChanged = false;
        }
        else source?.Save();
    }
}
#endif
