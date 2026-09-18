using UnityEngine;

namespace ZLZ.AnimeShader
{
    public class ZLZ_SelectionOutlineDemo : MonoBehaviour
    {
        [Header("Targets")]
        public ZLZ_SelectionController[] targets;

        [Header("Input")]
        public KeyCode nextKey     = KeyCode.Tab;
        public KeyCode prevKey     = KeyCode.Q;
        public KeyCode deselectKey = KeyCode.Escape;

        int _index = -1;

        ZLZ_SelectionController Current =>
            _index >= 0 && _index < targets.Length ? targets[_index] : null;

        void Update()
        {
            if (ZLZ_DemoInput.GetKeyDown(nextKey))     Cycle(+1);
            if (ZLZ_DemoInput.GetKeyDown(prevKey))     Cycle(-1);
            if (ZLZ_DemoInput.GetKeyDown(deselectKey)) DeselectCurrent();
        }

        void Cycle(int dir)
        {
            if (targets == null || targets.Length == 0) return;

            int next = _index + dir;
            if (next >= targets.Length) next = 0;
            if (next < 0)              next = targets.Length - 1;
            if (next == _index)        return;

            // Old target plays Outro, new target plays Intro simultaneously.
            Current?.Deselect();

            _index = next;
            targets[_index]?.Select();
        }

        void DeselectCurrent()
        {
            Current?.Deselect();
            _index = -1;
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            GUILayout.BeginArea(new Rect(24, 24, 420, 80));
            GUILayout.Label($"[{nextKey}] Next   [{prevKey}] Prev   [{deselectKey}] Deselect", style);
            string status = Current != null
                ? $"Target : {Current.name}  |  State : {Current.CurrentState}"
                : "No target selected";
            GUILayout.Label(status, style);
            GUILayout.EndArea();
        }
    }
}
