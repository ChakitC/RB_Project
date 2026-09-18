using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ZLZ.AnimeShader
{
    /// <summary>
    /// Drives a single shader global <c>_ZLZ_RuntimeActive</c> that gates runtime-only
    /// visual effects so they don't intrude on the Scene-view preview in Edit mode.
    ///
    /// Currently gated:
    ///   - Dither Camera Near Fade ([ZLZ_Dither.hlsl] → ZLZ_ComputeCameraDither)
    ///
    /// State machine:
    ///   Editor (default)     → 0 → gated features render as if disabled
    ///   Play mode / Build    → 1 → gated features run normally
    ///
    /// Why a uniform (and not a shader keyword): a single float costs one CBUFFER slot
    /// and zero variants. A keyword would double every variant count it touches and is
    /// overkill for a binary in/out-of-play-mode switch.
    /// </summary>
    public static class ZLZ_RuntimeFlag
    {
        static readonly int s_RuntimeActiveId = Shader.PropertyToID("_ZLZ_RuntimeActive");

        /// <summary>
        /// Runs at game start (Build) AND when entering Play mode (Editor).
        /// SubsystemRegistration is the earliest stage — guarantees the flag is set
        /// before the first frame renders, so character spawn doesn't flash fully
        /// faded during the first Update.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnRuntimeStart()
        {
            Shader.SetGlobalFloat(s_RuntimeActiveId, 1f);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Runs once whenever the editor loads or scripts recompile. Resets the flag
        /// to 0 so a freshly-reloaded editor doesn't show stale "1" from a previous
        /// play session, and registers the play-mode listener.
        /// </summary>
        [InitializeOnLoadMethod]
        static void OnEditorLoad()
        {
            Shader.SetGlobalFloat(s_RuntimeActiveId, 0f);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // EnteredPlayMode is handled by RuntimeInitializeOnLoadMethod above —
            // we only need to set the flag back to 0 on the way out.
            if (state == PlayModeStateChange.EnteredEditMode)
                Shader.SetGlobalFloat(s_RuntimeActiveId, 0f);
        }
#endif
    }
}
