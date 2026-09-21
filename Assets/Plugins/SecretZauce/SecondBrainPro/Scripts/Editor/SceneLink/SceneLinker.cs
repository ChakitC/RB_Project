using System.Collections.Generic;
using System.Linq;
using SecretZauce.SecondBrain.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    /// <summary>
    /// Listens for Unity Editor scene-open events and automatically opens a
    /// <see cref="SecondBrainWindow"/> for every <see cref="Base"/> whose
    /// <see cref="Base.SceneGuid"/> matches the newly-opened scene.
    ///
    /// Behaviour:
    ///  • Bases already visible in an open window are skipped.
    ///  • When an existing, docked <see cref="SecondBrainWindow"/> is found, the
    ///    new windows are added as tabs to its dock group.
    ///  • If no docked anchor exists, the first base opens a fresh window;
    ///    subsequent bases are added to that window's dock area (if supported by
    ///    the Unity version) or fall back to individual floating windows.
    ///  • Null / missing scenes are handled gracefully — no errors are thrown.
    /// </summary>
    [InitializeOnLoad]
    static class SceneLinker
    {
        static SceneLinker()
        {
            // Unsubscribe before subscribing so repeated static-constructor calls
            // (domain-reload disabled, Enter Play Mode options) don't double-register.
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorSceneManager.sceneClosed += OnSceneClosed;
        }

        // ── Scene-opened handler ──────────────────────────────────────────────────

        static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            try
            {
                if (!BrowserSettings.EnableSceneLinking)
                    return;

                var scenePath = scene.path;
                if (string.IsNullOrEmpty(scenePath))
                    return;

                var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                if (string.IsNullOrEmpty(sceneGuid))
                    return;

                var linkedBases = FindLinkedBases(sceneGuid);
                if (linkedBases.Count == 0)
                    return;

                // Exclude bases already displayed in any open window
                var alreadyOpenBases = GetBasesInOpenWindows();
                var basesToOpen = linkedBases
                    .Where(b => !alreadyOpenBases.Contains(b))
                    .ToList();

                if (basesToOpen.Count == 0)
                    return;

                // Defer to avoid GUI state conflicts during the scene-open event
                EditorApplication.delayCall += () => OpenWindowsForBases(basesToOpen);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SceneLinker] Unexpected error while processing scene-open: {ex.Message}");
            }
        }

        // ── Scene-closed handler ──────────────────────────────────────────────────

        static void OnSceneClosed(Scene scene)
        {
            try
            {
                if (!BrowserSettings.EnableSceneLinking || !BrowserSettings.CloseOnSceneClose)
                    return;

                var scenePath = scene.path;
                if (string.IsNullOrEmpty(scenePath))
                    return;

                var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                if (string.IsNullOrEmpty(sceneGuid))
                    return;

                var linkedBases = FindLinkedBases(sceneGuid);
                if (linkedBases.Count == 0)
                    return;

                var linkedBaseSet = new HashSet<Base>(linkedBases);

                // Defer to avoid GUI state conflicts during the scene-close event
                EditorApplication.delayCall += () => CloseWindowsForBases(linkedBaseSet);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SceneLinker] Unexpected error while processing scene-close: {ex.Message}");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the set of <see cref="Base"/> objects currently displayed in any open
        /// <see cref="SecondBrainWindow"/>.
        /// </summary>
        static HashSet<Base> GetBasesInOpenWindows()
        {
            var openWindows = BrowserWindowRegistry.AllOfType<SecondBrainWindow>();
            return new HashSet<Base>(
                openWindows
                    .Select(w => w.Root as Base)
                    .Where(b => b != null));
        }

        /// <summary>
        /// Returns all <see cref="Base"/> objects in <see cref="Profile.Active"/> whose
        /// <see cref="Base.SceneGuid"/> matches <paramref name="sceneGuid"/>.
        /// </summary>
        static List<Base> FindLinkedBases(string sceneGuid)
        {
            var result = new List<Base>();

            var profile = Profile.Active;
            if (profile == null)
                return result;

            foreach (var b in profile.Children)
            {
                if (b == null) continue;
                if (!string.IsNullOrEmpty(b.SceneGuid) && b.SceneGuid == sceneGuid)
                    result.Add(b);
            }

            return result;
        }

        /// <summary>
        /// Opens (or re-uses) <see cref="SecondBrainWindow"/> instances for each
        /// base in <paramref name="bases"/>, batching them as tabs when possible.
        /// </summary>
        static void OpenWindowsForBases(List<Base> bases)
        {
            if (bases == null || bases.Count == 0)
                return;

            // Re-query open windows at execution time (the delayCall may fire later)
            var openWindows = BrowserWindowRegistry.AllOfType<SecondBrainWindow>();

            // Try to find an already-docked window to use as the tab-group anchor.
            // Prefer a docked window so the new tabs appear next to the user's existing layout.
            var anchor = openWindows
                .FirstOrDefault(w => w != null && w.docked);

            if (anchor != null)
            {
                // Add all linked bases as new tabs inside the existing dock group
                foreach (var b in bases)
                {
                    var tab = CreateWindowForBase();
                    bool docked = anchor.AddTabToSameDockArea(tab);
                    if (!docked)
                        tab.Show();
                    tab.SetTarget(b);
                }
                return;
            }

            // No existing docked anchor — open a fresh window for the first base.
            var firstBase = bases[0];
            var firstWindow = ScriptableObject.CreateInstance<SecondBrainWindow>();
            firstWindow.minSize = new Vector2(600, 300);
            firstWindow.Show();
            firstWindow.SetTarget(firstBase);

            if (bases.Count == 1)
                return;

            // For additional bases, defer one frame so the first window can finish
            // setting up its internal DockArea parent before we try to attach tabs.
            var remainingBases = bases.Skip(1).ToList();
            EditorApplication.delayCall += () =>
            {
                foreach (var b in remainingBases)
                {
                    var tab = CreateWindowForBase();
                    bool docked = firstWindow.AddTabToSameDockArea(tab);
                    if (!docked)
                        tab.Show();
                    tab.SetTarget(b);
                }
            };
        }

        /// <summary>
        /// Closes any <see cref="SecondBrainWindow"/> whose current Root is one of
        /// the given bases.
        /// </summary>
        static void CloseWindowsForBases(HashSet<Base> bases)
        {
            if (bases == null || bases.Count == 0)
                return;

            var openWindows = BrowserWindowRegistry.AllOfType<SecondBrainWindow>();
            foreach (var w in openWindows)
            {
                if (w == null) continue;
                var root = w.Root as Base;
                if (root != null && bases.Contains(root))
                    w.Close();
            }
        }

        /// <summary>
        /// Creates a <see cref="SecondBrainWindow"/> instance. The window is NOT shown —
        /// the caller is responsible for either docking it (via
        /// <see cref="BrowserWindow.AddTabToSameDockArea"/>) or calling <c>Show()</c>,
        /// and for calling <see cref="BrowserWindow.SetTarget"/> with the desired base.
        /// </summary>
        static SecondBrainWindow CreateWindowForBase()
        {
            var wnd = ScriptableObject.CreateInstance<SecondBrainWindow>();
            wnd.minSize = new Vector2(600, 300);
            return wnd;
        }
    }
}
