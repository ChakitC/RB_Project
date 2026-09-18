using UnityEngine;

namespace ZLZ.AnimeShader
{
    public class ZLZ_IndicatorDemo : MonoBehaviour
    {
        [Header("Target")]
        public ZLZ_CharacterVFX target;

        [Header("Input")]
        public KeyCode toggleKey = KeyCode.I;

        void Update()
        {
            if (target == null) return;
            if (ZLZ_DemoInput.GetKeyDown(toggleKey)) target.Indicator.ToggleIndicator();
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            GUILayout.BeginArea(new Rect(24, 24, 420, 60));
            GUILayout.Label($"[{toggleKey}] Toggle Indicator", style);
            string state = target != null ? target.Indicator.CurrentState.ToString() : "—";
            GUILayout.Label($"State : {state}", style);
            GUILayout.EndArea();
        }
    }
}
