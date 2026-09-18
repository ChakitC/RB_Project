using UnityEngine;

namespace ZLZ.AnimeShader
{
    public class ZLZ_DitherCameraNearDemo : MonoBehaviour
    {
        [Header("Target")]
        public ZLZ_CharacterVFX target;

        [Header("Hide / Show Input")]
        [Tooltip("Toggle hide / show (stealth / reveal workflow).")]
        public KeyCode toggleKey = KeyCode.H;
        [Tooltip("Dither out instantly, then fade in (spawn / teleport-in workflow).")]
        public KeyCode spawnKey  = KeyCode.S;

        [Header("Camera Near Fade Test")]
        [Tooltip("Camera to dolly. Leave empty to use Camera.main.")]
        public Camera     dollyCamera;
        [Tooltip("Point the camera dollies around. Leave empty to use the target's transform.")]
        public Transform  dollyPivot;
        [Tooltip("Tap to toggle camera between far and near distance.")]
        public KeyCode    dollyKey   = KeyCode.C;
        [Tooltip("Far distance — character fully visible.")]
        public float      dollyFar   = 3f;
        [Tooltip("Near distance — well within the character's Camera Near Fade range.")]
        public float      dollyNear  = 0.15f;
        [Tooltip("Dolly speed (meters per second).")]
        public float      dollySpeed = 3f;

        // ── Dolly state — initialized in OnEnable from the camera's current pose ─
        Vector3 _dollyDir  = Vector3.back;
        float   _dollyDist = 0f;
        bool    _atNear    = false;

        void OnEnable()
        {
            // Capture the camera's initial direction-from-pivot so the dolly moves
            // along the existing line of sight — preserves the framing the user
            // already set up in the scene instead of snapping to a fixed angle.
            var cam   = ResolveCamera();
            var pivot = ResolvePivot();
            if (cam != null && pivot != null)
            {
                Vector3 toCam = cam.transform.position - pivot.position;
                _dollyDist = toCam.magnitude;
                _dollyDir  = _dollyDist > 1e-4f ? toCam.normalized : -pivot.forward;
            }
        }

        void Update()
        {
            if (target == null) return;

            // ── Manual Hide / Show / Spawn ────────────────────────────────
            if (ZLZ_DemoInput.GetKeyDown(toggleKey))
            {
                if (target.Dither.IsActive()) target.Dither.Show();
                else                          target.Dither.Hide();
            }

            if (ZLZ_DemoInput.GetKeyDown(spawnKey))
            {
                target.Dither.SetInstant(1f);
                target.Dither.Spawn();
            }

            // ── Camera Near Fade dolly ────────────────────────────────────
            UpdateCameraDolly();
        }

        void UpdateCameraDolly()
        {
            var cam   = ResolveCamera();
            var pivot = ResolvePivot();
            if (cam == null || pivot == null) return;

            if (ZLZ_DemoInput.GetKeyDown(dollyKey)) _atNear = !_atNear;

            float goalDist = _atNear ? dollyNear : dollyFar;
            _dollyDist = Mathf.MoveTowards(_dollyDist, goalDist, dollySpeed * Time.deltaTime);

            cam.transform.position = pivot.position + _dollyDir * _dollyDist;
            cam.transform.LookAt(pivot);
        }

        Camera ResolveCamera()
        {
            return dollyCamera != null ? dollyCamera : Camera.main;
        }

        Transform ResolvePivot()
        {
            if (dollyPivot != null) return dollyPivot;
            return target != null ? target.transform : null;
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            GUILayout.BeginArea(new Rect(24, 24, 540, 140));
            GUILayout.Label($"[{toggleKey}] Hide / Show     [{spawnKey}] Spawn In", style);
            string state = target != null ? target.Dither.CurrentState.ToString() : "—";
            GUILayout.Label($"State : {state}", style);
            GUILayout.Space(6f);
            GUILayout.Label($"[{dollyKey}] Dolly camera near / far  ({(_atNear ? "Near" : "Far")})", style);
            var cam   = ResolveCamera();
            var pivot = ResolvePivot();
            if (cam != null && pivot != null)
            {
                float d = Vector3.Distance(cam.transform.position, pivot.position);
                GUILayout.Label($"Distance : {d:F2}", style);
            }
            GUILayout.EndArea();
        }
    }
}
