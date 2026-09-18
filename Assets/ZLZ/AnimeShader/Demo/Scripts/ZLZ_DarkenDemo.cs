using UnityEngine;

namespace ZLZ.AnimeShader
{
    public class ZLZ_DarkenDemo : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Character whose Exclude state will be toggled by [excludeKey].")]
        public ZLZ_CharacterVFX excludedTarget;

        [Header("Input")]
        [Tooltip("Toggle global darken on / off (animates 0 ↔ 1).")]
        public KeyCode globalKey  = KeyCode.K;
        [Tooltip("Toggle the Exclude flag on the target character.")]
        public KeyCode excludeKey = KeyCode.E;

        void Update()
        {
            if (ZLZ_DemoInput.GetKeyDown(globalKey))
            {
                var mgr = ZLZ_DarkenManager.Instance;
                if (mgr != null) mgr.ToggleDarken();
            }

            if (ZLZ_DemoInput.GetKeyDown(excludeKey) && excludedTarget != null)
                excludedTarget.Darken.SetExcluded(!excludedTarget.Darken.IsExcluded);
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            GUILayout.BeginArea(new Rect(24, 24, 520, 100));
            GUILayout.Label($"[{globalKey}] Toggle Global Darken     [{excludeKey}] Toggle Exclude (on Target)", style);

            var mgr = ZLZ_DarkenManager.Instance;
            string globalState = mgr != null ? mgr.CurrentState.ToString() : "(no Manager in scene)";
            string excludeState = excludedTarget != null
                ? (excludedTarget.Darken.IsExcluded ? "EXCLUDED  (stays bright)" : "INCLUDED  (follows global)")
                : "—";

            GUILayout.Label($"Global  : {globalState}", style);
            GUILayout.Label($"Target  : {excludeState}", style);
            GUILayout.EndArea();
        }
    }
}
