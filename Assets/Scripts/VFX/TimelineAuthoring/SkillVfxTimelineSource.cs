#if UNITY_EDITOR
using System.Collections.Generic;
using Animancer;
using UnityEditor;
using UnityEngine;

public sealed class SkillVfxTimelineSource : IAnimationVfxTimelineSource
{
    static readonly AnimationVfxTimelineLane[] BaseLanes =
    {
        new AnimationVfxTimelineLane("Cast Point", AnimationVfxTimelineLaneKind.Point),
    };

    readonly SkillGemDefinition skill;
    readonly List<AnimationVfxCue> cues = new List<AnimationVfxCue>();
    readonly List<AnimationVfxTimelineLane> lanes = new List<AnimationVfxTimelineLane>();

    public SkillVfxTimelineSource(SkillGemDefinition skill)
    {
        this.skill = skill;
        IReadOnlyList<SkillVfxEvent> saved = skill != null ? skill.SkillVfxEvents : null;
        if (saved != null)
        {
            for (int i = 0; i < saved.Count; i++)
            {
                AnimationVfxCue cue = AnimationVfxTimelineSourceFactory.CopyCue(saved[i]);
                if (cue != null)
                    cues.Add(cue);
            }
        }

        lanes.AddRange(BaseLanes);
        if (skill != null && skill.BlockablePreCast)
        {
            lanes.Add(new AnimationVfxTimelineLane(
                "Pre-Cast",
                AnimationVfxTimelineLaneKind.Events,
                new[] { skill.PreCastOpenEventName, skill.PreCastCloseEventName }));
        }
        if (skill != null && skill.TryFindPayload(out PrefabHitboxSkillPayloadDef hitbox))
        {
            lanes.Add(new AnimationVfxTimelineLane(
                "Hitbox",
                AnimationVfxTimelineLaneKind.Events,
                new[] { hitbox.HitboxStartEventName, hitbox.HitboxEndEventName },
                allowDuplicateEvents: true));
        }
        if (skill != null && skill.TryFindPayload(out TargetedDeliverySkillPayloadDef _))
        {
            // Without its own lane the release marker lands in the generic "Other Events" row,
            // where it reads as an unrelated stray event rather than the one marker this skill
            // cannot work without.
            lanes.Add(new AnimationVfxTimelineLane(
                "Delivery",
                AnimationVfxTimelineLaneKind.Events,
                new[] { CombatTimelineEventName.DeliveryRelease }));
        }
    }

    public ScriptableObject SourceAsset => skill;
    public string EntryId => "main";
    public string DisplayName => skill != null ? skill.name : "Skill";
    public ClipTransition Transition => skill != null ? skill.skillClip : null;
    public int MarkerCount => skill != null ? skill.GetSkillVfxMarkerCount() : 0;
    public IReadOnlyList<AnimationVfxTimelineLane> Lanes => lanes;
    public float PointValue => skill != null ? skill.GetCastPointNormalized() : 0f;
    public Vector2 RangeValue => default;
    public int CueCount => cues.Count;
    public IAnimationVfxCue GetCue(int index) => index >= 0 && index < cues.Count ? cues[index] : null;

    public void SetPointValue(float value)
    {
        if (skill == null)
            return;
        Undo.RecordObject(skill, "Move Skill Cast Point");
        skill.castPointNormalized = Mathf.Clamp(value, 0f, 0.999f);
        EditorUtility.SetDirty(skill);
    }

    public void SetRangeValue(Vector2 value) { }

    public void ReplaceCues(IReadOnlyList<AnimationVfxCue> values)
    {
        if (skill == null)
            return;
        var converted = new List<SkillVfxEvent>();
        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                AnimationVfxCue cue = values[i];
                if (cue == null)
                    continue;
                converted.Add(new SkillVfxEvent
                {
                    cueIndex = cue.cueIndex,
                    action = (SkillVfxAction)cue.action,
                    prefab = cue.prefab,
                    anchor = (SkillVfxAnchor)cue.anchor,
                    customAnchorPath = cue.customAnchorPath,
                    humanoidBone = cue.humanoidBone,
                    localPosition = cue.localPosition,
                    localEulerAngles = cue.localEulerAngles,
                    localScale = cue.localScale,
                    parentToAnchor = cue.anchorMode == AnimationVfxAnchorMode.FollowAnchor,
                    loopKey = cue.loopKey,
                    extraLife = cue.extraLife,
                    allowParticlesToFinish = cue.allowParticlesToFinish,
                    animClip = cue.animClip,
                });
            }
        }
        Undo.RecordObject(skill, "Save Skill VFX Data");
        skill.ReplaceSkillVfxEvents(converted);
        ReloadCues(values);
        EditorUtility.SetDirty(skill);
    }

    public void MoveCueIndex(int oldCueIndex, int newCueIndex)
    {
        AnimationVfxTimelineSourceFactory.RemapCueIndices(cues, oldCueIndex, newCueIndex);
        ReplaceCues(cues);
    }

    public void RemoveCueIndex(int cueIndex)
    {
        var track = new AnimationVfxTrack();
        track.ReplaceCues(cues);
        track.RemoveCueIndex(cueIndex);
        ReplaceCues(track.Cues);
    }

    public void CollectValidationIssues(List<string> issues) { }

    public void Save()
    {
        if (skill == null)
            return;
        EditorUtility.SetDirty(skill);
        AssetDatabase.SaveAssets();
    }

    void ReloadCues(IReadOnlyList<AnimationVfxCue> values)
    {
        var copies = new List<AnimationVfxCue>();
        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                AnimationVfxCue cue = AnimationVfxTimelineSourceFactory.CopyCue(values[i]);
                if (cue != null)
                    copies.Add(cue);
            }
        }

        cues.Clear();
        cues.AddRange(copies);
    }
}
#endif
