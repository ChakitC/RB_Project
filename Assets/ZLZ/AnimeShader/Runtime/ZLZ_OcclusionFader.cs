using System.Collections.Generic;
using UnityEngine;

namespace ZLZ.AnimeShader
{
    /// <summary>
    /// Modern anime-style occlusion fade.
    ///
    /// Each LateUpdate, raycasts from <c>SourceCamera</c> to <c>TargetTransform</c>
    /// (typically the player character). Any <c>ZLZ_CharacterVFX</c> hit along the
    /// way with <c>Receive Occlusion Fade</c> enabled gets its <c>_DitherOcclusionAlpha</c>
    /// driven smoothly toward its chosen preset level (Soft or Full). When a character
    /// stops occluding, the alpha eases back to 0 over the configured Fade Out duration.
    ///
    /// Independent from <c>vfx.Dither.Hide() / Show()</c>: the shader combines the
    /// two alphas with <c>max()</c>, so they layer without conflict.
    ///
    /// One instance per scene. Auto-spawned by the Dashboard's Setup pipeline.
    /// </summary>
    [AddComponentMenu("ZLZ/Anime Shader/ZLZ_Occlusion Fader")]
    [DefaultExecutionOrder(100)]
    public class ZLZ_OcclusionFader : MonoBehaviour
    {
        [Tooltip("The transform the rays target — usually the player character. Required.")]
        public Transform TargetTransform;

        [Tooltip("Camera the rays originate from. Leave null to auto-use Camera.main.")]
        public Camera SourceCamera;

        [Tooltip("Layer mask used by the raycast. Restrict to layers that actually contain ZLZ characters to save cost.")]
        public LayerMask RaycastLayerMask = ~0;

        [Tooltip("Detection thickness around the camera-to-target line. " +
                 "0 = razor-thin raycast (occluder must intersect the line exactly). " +
                 "0.5 ≈ one character's width — characters anywhere within this radius of the line trigger the fade.")]
        [Min(0f)] public float OccluderRadius = 0.5f;

        [Tooltip("How the raycast treats trigger colliders.\n" +
                 "Collide (default): triggers count as occluders — set 'Is Trigger' on the occluder's collider " +
                 "to let the player walk through it while still triggering occlusion fade.\n" +
                 "Ignore: skip trigger colliders entirely (use if all occluders are solid colliders).")]
        public QueryTriggerInteraction TriggerInteraction = QueryTriggerInteraction.Collide;

        [Tooltip("Run the raycast every N fixed frames. 1 = every LateUpdate (smoothest). " +
                 "Higher values reduce cost but make fade-in slightly steppy on fast camera motion.")]
        [Min(1)] public int UpdateInterval = 1;

        // ── Internal state per occlusion-eligible character ──────────────────
        class FadeState
        {
            public ZLZ_CharacterVFX vfx;
            public float            current;        // current _DitherOcclusionAlpha being written
            public float            target;         // desired alpha (level-value × overlap ratio when occluding, 0 otherwise)
            public bool             occluding;      // set by this frame's raycast pass
            public float            overlapRatio;   // 0..1 — how centered the occluder sits on the camera→target line.
                                                    // 1 = collider center is exactly on the line (max fade),
                                                    // 0 = collider center is at radius edge (no fade).
        }

        readonly Dictionary<ZLZ_CharacterVFX, FadeState> _states = new Dictionary<ZLZ_CharacterVFX, FadeState>();
        int _frameCounter;

        // Cached Camera.main — re-resolved only when it goes null (scene change / camera
        // destroyed), instead of running a tagged scene search every LateUpdate.
        Camera _mainCamCache;

        // Cached buffer for RaycastNonAlloc — avoids per-frame allocations.
        static readonly RaycastHit[] s_HitBuffer = new RaycastHit[32];

        void OnDisable()
        {
            // Restore all tracked characters to alpha 0 before disabling — leaving them
            // dithered when the manager turns off would surprise users mid-edit.
            foreach (var kv in _states)
            {
                var vfx = kv.Key;
                if (vfx != null)
                    vfx.WriteOcclusionAlpha(0f);
            }
            _states.Clear();
        }

        void LateUpdate()
        {
            // Source camera fallback — let users leave the field null and rely on Camera.main.
            // Camera.main runs a tagged scene search, so cache it and only re-resolve when the
            // cached camera goes null (e.g. a scene change destroyed the previous main camera).
            Camera cam = SourceCamera;
            if (cam == null)
            {
                if (_mainCamCache == null) _mainCamCache = Camera.main;
                cam = _mainCamCache;
            }
            if (cam == null || TargetTransform == null) return;

            // Throttle raycasts; alpha lerps still tick every frame for smoothness.
            if ((_frameCounter++ % UpdateInterval) == 0)
                RecomputeOccludingSet(cam);

            StepAlphas(Time.deltaTime);
        }

        void RecomputeOccludingSet(Camera cam)
        {
            // Reset per-frame flags — any character not refreshed below will fade out.
            foreach (var s in _states.Values) { s.occluding = false; s.overlapRatio = 0f; }

            Vector3 origin = cam.transform.position;
            Vector3 toTarget = TargetTransform.position - origin;
            float   dist     = toTarget.magnitude;
            if (dist < 1e-4f) return;
            Vector3 dir = toTarget / dist;

            // SphereCast (instead of plain Raycast) gives the detection ray a configurable
            // thickness — characters within OccluderRadius of the line trigger the fade,
            // not just the ones it intersects exactly. radius=0 degenerates back to a thin
            // raycast for users who want the old behavior.
            int hitCount = Physics.SphereCastNonAlloc(origin, OccluderRadius, dir, s_HitBuffer, dist, RaycastLayerMask, TriggerInteraction);
            for (int i = 0; i < hitCount; i++)
            {
                // Walk up the hierarchy — collider often sits on a child, the VFX
                // component lives on the character root.
                var vfx = s_HitBuffer[i].collider.GetComponentInParent<ZLZ_CharacterVFX>();
                if (vfx == null || !vfx.Dither.Enabled || !vfx.Dither.ReceiveOcclusionFade) continue;

                // Skip the player itself — its own collider shouldn't dither out the player.
                if (vfx.transform == TargetTransform || vfx.transform.IsChildOf(TargetTransform)) continue;

                // Overlap ratio — measures how centered the collider sits on the
                // camera→target line. Closer to the line = stronger fade. A graduated
                // dither: barely clipping the line gives a gentle fade, fully
                // on the line gives the configured Soft/Full alpha.
                //
                // Uses collider.bounds.center (not vfx.transform.position) because ZLZ
                // characters typically have transform at feet — bounds center tracks the
                // visual body better.
                Vector3 occluderCenter = s_HitBuffer[i].collider.bounds.center;
                Vector3 toOccluder     = occluderCenter - origin;
                float   proj           = Mathf.Clamp(Vector3.Dot(toOccluder, dir), 0f, dist);
                Vector3 closest        = origin + dir * proj;
                float   perpDist       = Vector3.Distance(occluderCenter, closest);
                float   ratio          = OccluderRadius > 1e-4f
                    ? Mathf.Clamp01(1f - perpDist / OccluderRadius)
                    : 1f;   // radius 0 = thin raycast, no gradient possible — keep binary behavior

                if (!_states.TryGetValue(vfx, out var st))
                {
                    st = new FadeState { vfx = vfx };
                    _states[vfx] = st;
                }
                st.occluding = true;
                // A character may have multiple colliders (capsule + hand etc.) — keep the
                // hit whose center sits closest to the line.
                if (ratio > st.overlapRatio) st.overlapRatio = ratio;
            }
        }

        void StepAlphas(float dt)
        {
            // Build a list of dead entries to remove after iteration (mutating a
            // dictionary mid-loop throws).
            List<ZLZ_CharacterVFX> dead = null;

            foreach (var kv in _states)
            {
                var vfx = kv.Key;
                var st  = kv.Value;

                if (vfx == null)
                {
                    (dead ??= new List<ZLZ_CharacterVFX>()).Add(kv.Key);
                    continue;
                }

                var settings = vfx.Dither.SettingsAsset;
                float levelAlpha = settings != null
                    ? settings.GetOcclusionAlpha(vfx.Dither.OcclusionLevel)
                    : (vfx.Dither.OcclusionLevel == ZLZ_OcclusionLevel.Full ? 1f : 0.9f);

                // Scale the configured Soft/Full level by overlap ratio so partial overlap
                // produces partial fade. A graduated, stylized occlusion.
                st.target = st.occluding ? levelAlpha * st.overlapRatio : 0f;

                // Reuse the same Intro / Outro durations that drive Hide/Show animations
                // — a single timing source per ZLZ_DitherSettings asset, so users tune
                // one set of values instead of juggling two parallel systems.
                float dur = st.current < st.target
                    ? (settings != null ? settings.animation.introDuration  : 0.4f)
                    : (settings != null ? settings.animation.outroDuration : 0.4f);

                float step = dur > 1e-4f ? dt / dur : 1f;
                st.current = Mathf.MoveTowards(st.current, st.target, step);

                vfx.WriteOcclusionAlpha(st.current);

                // Garbage-collect entries that have fully eased back to 0 and aren't
                // currently being occluded — keeps _states small over long sessions.
                if (!st.occluding && st.current <= 0f)
                    (dead ??= new List<ZLZ_CharacterVFX>()).Add(kv.Key);
            }

            if (dead != null)
                foreach (var k in dead) _states.Remove(k);
        }

    }
}
