using UnityEngine;

namespace ZLZ.AnimeShader
{
    /// <summary>
    /// Demo driver for ZLZ Dither's Occlusion Fade.
    ///
    /// Moves a "player" Transform with WASD / Arrow keys — as it walks across the scene,
    /// the camera-to-target line changes direction. Any character between the camera and
    /// this target that has Dither.ReceiveOcclusionFade enabled will fade out, with the
    /// strength scaled by how centered it sits on the line (see ZLZ_OcclusionFader's
    /// SphereCast + OccluderRadius).
    ///
    /// Scene setup:
    ///   1. Add a ZLZ_OcclusionFader to the scene and assign its TargetTransform to the
    ///      same Transform referenced by playerTarget below.
    ///   2. Configure occluder characters with ZLZ_CharacterVFX (Dither.Enabled +
    ///      ReceiveOcclusionFade).
    ///   3. Drop this component on any GameObject — commonly the playerTarget itself.
    /// </summary>
    public class ZLZ_DitherOcclusionDemo : MonoBehaviour
    {
        [Header("Player Target")]
        [Tooltip("Transform that moves with input. Assign the SAME Transform to " +
                 "ZLZ_OcclusionFader.TargetTransform so the raycast follows it.")]
        public Transform playerTarget;

        [Header("Movement")]
        [Tooltip("Meters per second when holding a movement key.")]
        public float moveSpeed = 2f;

        [Tooltip("If on, movement uses world axes (X = horizontal, Z = vertical). " +
                 "If off, uses the playerTarget's own local axes — useful when the " +
                 "demo is rotated to an angle in the scene.")]
        public bool moveInWorldSpace = true;

        [Header("Input")]
        [Tooltip("Snap the player target back to its starting position.")]
        public KeyCode resetKey = KeyCode.R;

        [Header("Visualization")]
        [Tooltip("Draw the camera-to-target ray + SphereCast radius corridor in the " +
                 "Scene view when this GameObject is selected.")]
        public bool drawGizmos = true;

        // ── Initial position captured at OnEnable for Reset ──
        Vector3 _initialPosition;
        bool    _hasInitial;

        void OnEnable()
        {
            if (playerTarget != null)
            {
                _initialPosition = playerTarget.position;
                _hasInitial      = true;
            }
        }

        void Update()
        {
            if (playerTarget == null) return;

            Vector2 move = ZLZ_DemoInput.GetMoveAxis();
            float h = move.x;
            float v = move.y;

            if (h != 0f || v != 0f)
            {
                Vector3 right   = moveInWorldSpace ? Vector3.right   : playerTarget.right;
                Vector3 forward = moveInWorldSpace ? Vector3.forward : playerTarget.forward;
                Vector3 delta   = (right * h + forward * v) * (moveSpeed * Time.deltaTime);
                playerTarget.position += delta;
            }

            if (ZLZ_DemoInput.GetKeyDown(resetKey) && _hasInitial)
                playerTarget.position = _initialPosition;
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            GUILayout.BeginArea(new Rect(24, 24, 540, 160));
            GUILayout.Label("[WASD / Arrows] Move player target", style);
            GUILayout.Label($"[{resetKey}] Reset position", style);
            GUILayout.Space(6);

            if (playerTarget != null)
            {
                Vector3 p = playerTarget.position;
                GUILayout.Label($"Target : ({p.x:F2}, {p.y:F2}, {p.z:F2})", style);
            }
            else
            {
                GUILayout.Label("⚠ playerTarget is not assigned", style);
            }

#if UNITY_2022_2_OR_NEWER
            if (FindAnyObjectByType<ZLZ_OcclusionFader>() == null)
#else
            if (FindObjectOfType<ZLZ_OcclusionFader>() == null)
#endif
                GUILayout.Label("⚠ No ZLZ_OcclusionFader in scene", style);

            GUILayout.EndArea();
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos || playerTarget == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 from = cam.transform.position;
            Vector3 to   = playerTarget.position;
            float   dist = Vector3.Distance(from, to);
            if (dist < 1e-4f) return;

            // Solid cyan line — actual camera→target raycast direction.
            Gizmos.color = new Color(0f, 1f, 1f, 0.85f);
            Gizmos.DrawLine(from, to);

            // Transparent wire-spheres along the line — visualize the SphereCast corridor
            // so it's obvious which characters are "inside the detection tube".
#if UNITY_2022_2_OR_NEWER
            var fader = FindAnyObjectByType<ZLZ_OcclusionFader>();
#else
            var fader = FindObjectOfType<ZLZ_OcclusionFader>();
#endif
            if (fader != null && fader.OccluderRadius > 0.01f)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.22f);
                Vector3 dir = (to - from) / dist;
                int     steps = Mathf.Max(2, Mathf.RoundToInt(dist / 0.8f));
                for (int i = 1; i < steps; i++)
                {
                    Vector3 p = from + dir * (dist * i / steps);
                    Gizmos.DrawWireSphere(p, fader.OccluderRadius);
                }
            }
        }
    }
}
