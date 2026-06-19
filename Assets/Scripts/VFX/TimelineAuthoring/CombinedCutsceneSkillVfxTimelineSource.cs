#if UNITY_EDITOR
using System.Collections.Generic;
using Animancer;
using UnityEngine;

public sealed class CombinedCutsceneSkillVfxTimelineSource
    : IAnimationVfxTimelineSource, IAnimationVfxTimelineMultiMode
{
    readonly SkillVfxTimelineSource _main;
    readonly CutsceneSkillVfxTimelineSource _cutscene;
    bool _secondary;

    IAnimationVfxTimelineSource Active => _secondary
        ? (IAnimationVfxTimelineSource)_cutscene
        : _main;

    public CombinedCutsceneSkillVfxTimelineSource(SkillGemDefinition skill)
    {
        _main     = new SkillVfxTimelineSource(skill);
        _cutscene = new CutsceneSkillVfxTimelineSource(skill);
    }

    // IAnimationVfxTimelineMultiMode
    public string SecondaryModeLabel => "Cutscene VFX";
    public bool IsSecondaryMode => _secondary;
    public void SetMode(bool secondary) => _secondary = secondary;
    public ClipTransition ReferenceTransition => _secondary ? null : _cutscene.Transition;
    public IAnimationVfxCueSource ReferenceCueSource => _secondary ? null : _cutscene;

    // IAnimationVfxTimelineSource — delegate to Active
    public ScriptableObject SourceAsset                      => Active.SourceAsset;
    public string EntryId                                    => Active.EntryId;
    public string DisplayName                                => Active.DisplayName;
    public ClipTransition Transition                         => Active.Transition;
    public int MarkerCount                                   => Active.MarkerCount;
    public IReadOnlyList<AnimationVfxTimelineLane> Lanes     => Active.Lanes;
    public float PointValue                                  => Active.PointValue;
    public Vector2 RangeValue                                => Active.RangeValue;
    public int CueCount                                      => Active.CueCount;
    public IAnimationVfxCue GetCue(int index)                => Active.GetCue(index);
    public void SetPointValue(float value)                   => Active.SetPointValue(value);
    public void SetRangeValue(Vector2 value)                 => Active.SetRangeValue(value);
    public void ReplaceCues(IReadOnlyList<AnimationVfxCue> cues) => Active.ReplaceCues(cues);
    public void MoveCueIndex(int oldIdx, int newIdx)         => Active.MoveCueIndex(oldIdx, newIdx);
    public void RemoveCueIndex(int idx)                      => Active.RemoveCueIndex(idx);
    public void CollectValidationIssues(List<string> issues) => Active.CollectValidationIssues(issues);
    public void Save()                                       => Active.Save();
}
#endif
