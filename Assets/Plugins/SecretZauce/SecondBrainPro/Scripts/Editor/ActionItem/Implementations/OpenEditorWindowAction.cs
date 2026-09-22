using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    /// <summary>
    /// Specifies which built-in Unity Editor window to open.
    /// </summary>
    public enum EditorWindowTarget
    {
        ProjectBrowser,
        Inspector,
        Hierarchy,
        SceneView,
        GameView,
        Console,
        Animator,
        Animation,
        Lighting,
        ProfilerWindow,
    }

    /// <summary>
    /// Action Item that opens the specified Unity Editor window when executed.
    /// Set <see cref="targetWindow"/> in the Inspector to choose which window to open.
    /// </summary>
    public class OpenEditorWindowAction : ActionItem
    {
        [Tooltip("The Unity Editor window to open when this action is executed.")]
        public EditorWindowTarget targetWindow = EditorWindowTarget.Inspector;

        public override string ActionPath => "Examples";

        public override string GetDetailDisplay()
        {
            // Show the currently selected target window as a small subtitle
            return targetWindow.ToString();
        }

        public override void Execute()
        {
            OpenWindow(targetWindow);
        }

        static void OpenWindow(EditorWindowTarget target)
        {
            switch (target)
            {
                case EditorWindowTarget.ProjectBrowser:
                    EditorApplication.ExecuteMenuItem("Window/General/Project");
                    break;
                case EditorWindowTarget.Inspector:
                    EditorApplication.ExecuteMenuItem("Window/General/Inspector");
                    break;
                case EditorWindowTarget.Hierarchy:
                    EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");
                    break;
                case EditorWindowTarget.SceneView:
                    EditorApplication.ExecuteMenuItem("Window/General/Scene");
                    break;
                case EditorWindowTarget.GameView:
                    EditorApplication.ExecuteMenuItem("Window/General/Game");
                    break;
                case EditorWindowTarget.Console:
                    EditorApplication.ExecuteMenuItem("Window/General/Console");
                    break;
                case EditorWindowTarget.Animator:
                    EditorApplication.ExecuteMenuItem("Window/Animation/Animator");
                    break;
                case EditorWindowTarget.Animation:
                    EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
                    break;
                case EditorWindowTarget.Lighting:
                    EditorApplication.ExecuteMenuItem("Window/Rendering/Lighting");
                    break;
                case EditorWindowTarget.ProfilerWindow:
                    EditorApplication.ExecuteMenuItem("Window/Analysis/Profiler");
                    break;
                default:
                    Debug.LogWarning($"[OpenEditorWindowAction] Unhandled target: {target}");
                    break;
            }
        }
    }
}
