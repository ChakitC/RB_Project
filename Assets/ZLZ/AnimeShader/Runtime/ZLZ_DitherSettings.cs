using UnityEngine;

namespace ZLZ.AnimeShader
{
    /// <summary>
    /// 2-level alpha presets used by the Occlusion Fader. Each character picks one
    /// level via its <c>ZLZ_DitherFX</c>; the OcclusionFader looks up the value here
    /// when raycast finds the character is blocking the camera-to-player line.
    /// </summary>
    public enum ZLZ_OcclusionLevel
    {
        Soft,   // ghost-through (default 0.9) — still see character's silhouette
        Full,   // hide-through (default 1.0) — completely transparent
    }

    [CreateAssetMenu(fileName = "ZLZ_DitherSettings", menuName = "ZLZ/FX Settings/Dither Settings")]
    public class ZLZ_DitherSettings : ScriptableObject
    {
        // ── Animation timing (shared by Hide/Show/Spawn AND Occlusion Fade) ─
        //
        // Single source of truth for fade timing — both the runtime Hide/Show
        // API and the scene-level ZLZ_OcclusionFader read introDuration /
        // outroDuration from here.
        //
        // Hide()  → Intro (0 → 1)              Occlusion start → Intro duration
        // Show()  → Outro (1 → 0)              Occlusion end   → Outro duration
        //
        // Defaults bias slightly toward responsiveness (0.4 / 0.4) so both
        // dramatic Hide-style effects and snappy Occlusion fades feel right.
        public ZLZ_FXAnimationSettings animation = new ZLZ_FXAnimationSettings
        {
            loopCount     = 0,
            introDuration = 0.4f,
            loopPeriod    = 0f,
            outroDuration = 0.4f,
            loopCurve     = AnimationCurve.Constant(0f, 1f, 1f),
        };

        // ── Occlusion Fade levels ───────────────────────────────────────────
        // Driven by ZLZ_OcclusionFader (scene manager) when a raycast from camera
        // to player finds this character blocking the line of sight.
        [Header("Occlusion Levels")]
        [Tooltip("Soft level — a \"ghost-through\" look. Character is mostly clipped but still visible as a faint silhouette.")]
        [Range(0f, 1f)] public float occlusionSoft = 0.9f;

        [Tooltip("Full level — character is fully clipped when occluding (no silhouette).")]
        [Range(0f, 1f)] public float occlusionFull = 1.0f;

        /// <summary>Convenience lookup — returns the alpha value for a given level.</summary>
        public float GetOcclusionAlpha(ZLZ_OcclusionLevel level)
            => level == ZLZ_OcclusionLevel.Full ? occlusionFull : occlusionSoft;
    }
}
