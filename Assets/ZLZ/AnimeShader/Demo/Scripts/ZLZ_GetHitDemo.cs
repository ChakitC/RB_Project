using UnityEngine;

namespace ZLZ.AnimeShader
{
    public class ZLZ_GetHitDemo : MonoBehaviour
    {
        [Header("Target")]
        public ZLZ_CharacterVFX target;

        [Header("Input")]
        public KeyCode hitKey = KeyCode.H;

        void Update()
        {
            if (target == null) return;
            if (ZLZ_DemoInput.GetKeyDown(hitKey)) target.GetHit.Hit();
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            GUILayout.BeginArea(new Rect(24, 24, 420, 60));
            GUILayout.Label($"[{hitKey}] Trigger Hit", style);
            string state = target != null ? target.GetHit.CurrentState.ToString() : "—";
            GUILayout.Label($"State : {state}", style);
            GUILayout.EndArea();
        }
    }
}
