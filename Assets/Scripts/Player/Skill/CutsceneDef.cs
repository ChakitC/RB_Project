using System.Collections.Generic;
using Animancer;
using UnityEngine;

[System.Serializable]
public sealed class CutsceneDef
{
    [Tooltip("Animation played on the cutscene character rig. Add 'Vfx' markers to drive cutscene VFX spawning — author them via SetAnimationVfxData with entry 'Cutscene VFX'.")]
    public ClipTransition characterCutsceneClip;

    [Tooltip("Animation played on the cutscene camera rig during the cutscene phase.")]
    public AnimationClip cameraCutsceneClip;

    [Range(0.05f, 1f), Tooltip("World time scale while cutscene is active. Does not affect the player's own animation.")]
    public float worldSlowScale = 0.05f;

    [Min(0f), Tooltip("Duration of the black overlay fade-in at cutscene start (unscaled seconds).")]
    public float fadeInDuration = 0.08f;

    [Min(0f), Tooltip("Duration of the black overlay fade-out when returning to gameplay (unscaled seconds).")]
    public float fadeOutDuration = 0.12f;

    [Range(0f, 0.45f), Tooltip("Fraction of screen height for each letterbox bar.")]
    public float barThickness = 0.08f;

    [Tooltip("VFX spawned during the cutscene phase. Author these via SetAnimationVfxData — select the SkillGemDefinition and entry 'Cutscene VFX'. Do not edit this list directly.")]
    public List<AnimationVfxCue> cutsceneVfxEvents = new List<AnimationVfxCue>();

    public float GetCutsceneDuration()
    {
        float charLength = characterCutsceneClip != null && characterCutsceneClip.IsValid
            ? characterCutsceneClip.Clip.length
            : 0f;
        float camLength = cameraCutsceneClip != null ? cameraCutsceneClip.length : 0f;
        return Mathf.Max(charLength, camLength);
    }

#if UNITY_EDITOR
    public int GetCutsceneVfxMarkerCount()
    {
        return AnimationVfxTimelineSourceFactory.CountMarkers(characterCutsceneClip);
    }

    public void ReplaceCutsceneVfxEvents(IReadOnlyList<AnimationVfxCue> values)
    {
        cutsceneVfxEvents.Clear();
        if (values == null)
            return;
        for (int i = 0; i < values.Count; i++)
            if (values[i] != null)
                cutsceneVfxEvents.Add(values[i]);
    }
#endif
}
