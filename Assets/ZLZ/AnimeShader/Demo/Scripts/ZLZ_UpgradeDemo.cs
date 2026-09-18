using UnityEngine;

namespace ZLZ.AnimeShader
{
    public class ZLZ_UpgradeDemo : MonoBehaviour
    {
        [Header("Target")]
        public ZLZ_CharacterVFX target;

        [Header("Input")]
        public KeyCode toggleKey = KeyCode.Space;

        void Update()
        {
            if (target == null) return;
            if (ZLZ_DemoInput.GetKeyDown(toggleKey)) target.Upgrade.ToggleUpgrade();
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            GUILayout.BeginArea(new Rect(24, 24, 420, 60));
            GUILayout.Label($"[{toggleKey}] Toggle Upgrade", style);
            string state = target != null ? target.Upgrade.CurrentState.ToString() : "—";
            GUILayout.Label($"State : {state}", style);
            GUILayout.EndArea();
        }
    }
}
