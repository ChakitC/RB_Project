using SecretZauce.SecondBrain.Editor;
using UnityEditor;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    /// <summary>
    /// Pro-only editor menu entries.
    ///
    /// <see cref="MenuItem"/> is a compile-time attribute, so unlike every other Pro capability
    /// these cannot be gated on a runtime <c>ProFeature.Provider</c> check — they have to be
    /// declared by an assembly that only compiles when Pro is active. Free declares the single
    /// "Window/Second Brain Window" entry; these two extra entry points only make sense once
    /// Pro's multiple Profiles/Bases exist.
    /// </summary>
    internal static class ProMenuItems
    {
        // Open Home (no default-base navigation).
        [MenuItem("Window/Second Brain (Home)")]
        static void OpenHomeMenu()
        {
            BrowserWindow.OpenWindow<SecondBrainWindow>();
        }

        // Open the Default Base if one is set; falls back to Home when it is not.
        [MenuItem("Window/Second Brain (Default Base)")]
        static void OpenDefaultBaseMenu()
        {
            SecondBrainWindow.OpenDefaultOrHome();
        }
    }
}
