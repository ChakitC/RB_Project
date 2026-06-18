#if UNITY_EDITOR
using System.Collections.Generic;
using Animancer;
using UnityEditor;
using UnityEngine;

public static class AnimationVfxTimelineSourceFactory
{
    public static IAnimationVfxTimelineSource Create(ScriptableObject asset, string entryId)
    {
        if (asset is SkillGemDefinition skill)
        {
            return skill.IsCutsceneSkill
                ? (IAnimationVfxTimelineSource)new CombinedCutsceneSkillVfxTimelineSource(skill)
                : new SkillVfxTimelineSource(skill);
        }
        if (asset is MeleeComboSO combo)
            return new MeleeComboVfxTimelineSource(combo, entryId);
        if (asset is CharacterAnimProfileSO profile)
            return new CharacterAnimProfileVfxTimelineSource(profile, entryId);
        return null;
    }

    public static List<AnimationVfxTimelineEntry> GetEntries(ScriptableObject asset)
    {
        var entries = new List<AnimationVfxTimelineEntry>();
        if (asset is SkillGemDefinition skill)
        {
            entries.Add(new AnimationVfxTimelineEntry("main", "Main Skill"));
        }
        else if (asset is MeleeComboSO combo)
        {
            for (int i = 0; i < combo.Count; i++)
            {
                MeleeComboSO.Step step = combo.Steps[i];
                if (string.IsNullOrWhiteSpace(step.EntryId))
                    continue;

                string clipName = step.clip != null && step.clip.Clip != null ? step.clip.Clip.name : "No Clip";
                entries.Add(new AnimationVfxTimelineEntry(step.EntryId, $"Step {i + 1}: {clipName}"));
            }
        }
        else if (asset is CharacterAnimProfileSO profile)
        {
            AddProfileEntry(entries, profile, CharacterAnimProfileSO.DashForwardVfxEntryId);
            AddProfileEntry(entries, profile, CharacterAnimProfileSO.DashBackwardVfxEntryId);
            AddProfileEntry(entries, profile, CharacterAnimProfileSO.ReloadVfxEntryId);
        }
        return entries;
    }

    static void AddProfileEntry(
        List<AnimationVfxTimelineEntry> entries,
        CharacterAnimProfileSO profile,
        string entryId)
    {
        profile.TryGetAnimationVfxEntry(entryId, out ClipTransition transition, out _);
        entries.Add(new AnimationVfxTimelineEntry(
            entryId,
            CharacterAnimProfileVfxTimelineSource.GetEntryLabel(entryId, transition)));
    }

    internal static AnimationVfxCue CopyCue(IAnimationVfxCue cue)
    {
        if (cue == null)
            return null;
        return new AnimationVfxCue
        {
            cueIndex = cue.CueIndex,
            action = cue.Action,
            prefab = cue.Prefab,
            anchor = cue.Anchor,
            anchorMode = cue.AnchorMode,
            customAnchorPath = cue.CustomAnchorPath,
            humanoidBone = cue.HumanoidBone,
            localPosition = cue.LocalPosition,
            localEulerAngles = cue.LocalEulerAngles,
            localScale = cue.LocalScale,
            loopKey = cue.LoopKey,
            extraLife = cue.ExtraLife,
            allowParticlesToFinish = cue.AllowParticlesToFinish,
        };
    }

    internal static int CountMarkers(ClipTransition transition)
    {
        AnimancerEvent.Sequence events = transition != null ? transition.Events : null;
        StringReference vfxName = CombatTimelineEventNames.ToStringReference(CombatTimelineEventName.Vfx);
        if (events == null || vfxName == null)
            return 0;
        int count = 0;
        int index = -1;
        while ((index = events.IndexOf(vfxName, index + 1)) >= 0)
            count++;
        return count;
    }

    internal static void RemapCueIndices(List<AnimationVfxCue> cues, int oldCueIndex, int newCueIndex)
    {
        var track = new AnimationVfxTrack();
        track.ReplaceCues(cues);
        track.MoveCueIndex(oldCueIndex, newCueIndex);
    }
}
#endif
