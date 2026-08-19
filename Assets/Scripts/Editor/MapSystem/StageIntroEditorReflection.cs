using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Small reflective shims the stage intro preview needs because Unity 6 keeps the equivalent public
/// API internal. Every entry point degrades to a no-op rather than throwing, so a future Unity
/// version that renames or removes one costs a cosmetic regression, never a compile break.
/// </summary>
internal static class StageIntroEditorReflection
{
    static MethodInfo clearSceneDirtinessMethod;
    static bool clearSceneDirtinessResolved;

    /// <summary>Clears a scene's dirty flag so previewing never leaves unsaved-changes state behind.</summary>
    public static void ClearSceneDirtiness(Scene scene)
    {
        if (!clearSceneDirtinessResolved)
        {
            clearSceneDirtinessResolved = true;
            clearSceneDirtinessMethod = typeof(EditorSceneManager).GetMethod(
                "ClearSceneDirtiness",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(Scene) },
                null);
        }

        clearSceneDirtinessMethod?.Invoke(null, new object[] { scene });
    }
}
