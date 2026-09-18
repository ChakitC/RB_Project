using UnityEngine;

namespace ZLZ.AnimeShader
{
    public class ZLZ_DissolveDemo : MonoBehaviour
    {
        [Header("Target")]
        public ZLZ_CharacterVFX target;

        [Header("Input")]
        [Tooltip("Toggle dissolve-out / restore (death + revive workflow).")]
        public KeyCode toggleKey = KeyCode.D;
        [Tooltip("Hide instantly, then fade in (spawn / teleport-in workflow).")]
        public KeyCode spawnKey  = KeyCode.S;

        void Update()
        {
            if (target == null) return;

            if (ZLZ_DemoInput.GetKeyDown(toggleKey))
            {
                if (target.Dissolve.IsActive()) target.Dissolve.Restore();
                else                            target.Dissolve.Dissolve();
            }

            if (ZLZ_DemoInput.GetKeyDown(spawnKey))
            {
                target.Dissolve.SetInstant(1f);   // pre-set fully dissolved
                target.Dissolve.Spawn();           // fade in (1 → 0)
            }
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            GUILayout.BeginArea(new Rect(24, 24, 460, 80));
            GUILayout.Label($"[{toggleKey}] Dissolve / Restore     [{spawnKey}] Spawn In", style);
            string state = target != null ? target.Dissolve.CurrentState.ToString() : "—";
            GUILayout.Label($"State : {state}", style);
            GUILayout.EndArea();
        }
    }
}
