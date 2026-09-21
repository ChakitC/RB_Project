using SecretZauce.SecondBrain.Editor;
using UnityEditor;
using UnityEngine;
using UnityEditor.ShortcutManagement;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    public static class QuickBrowse
    {
        static SecondBrainWindow _activePopup;

        [Shortcut("Toggle Quick Browse (Alt+W)", KeyCode.W, ShortcutModifiers.Alt)]
        public static void Toggle()
        {
            try
            {
                // Check static ref first — it is set by CreateFloatingPopup and survives focus
                // changes that the registry lookup below would otherwise have to cover.
                if (_activePopup != null)
                {
                    try { _activePopup.Close(); } catch { }
                    _activePopup = null;
                    return;
                }

                // Fallback for popups opened outside this Toggle path.
                var openWindows = BrowserWindowRegistry.AllOfType<SecondBrainWindow>();
                foreach (var w in openWindows)
                {
                    try
                    {
                        if (w.IsPopup)
                        {
                            w.Close();
                            return;
                        }
                    }
                    catch { }
                }

                CreateFloatingPopup();
            }
            catch
            {
                BrowserWindow.OpenWindow<SecondBrainWindow>();
            }
        }

        static void CreateFloatingPopup()
        {
            try
            {
                var newWindow = ScriptableObject.CreateInstance<SecondBrainWindow>();

                // Mark as popup mode using reflection
                var isPopupField = typeof(BrowserWindow).GetField("isPopupMode",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (isPopupField != null)
                    isPopupField.SetValue(newWindow, true);

                // Set window properties
                newWindow.minSize = new Vector2(400, 300);

                // Hide the top toolbar for a cleaner popup appearance
                newWindow.TopToolbarVisible = true;

                // Position at center of screen
                var mainRect = EditorGUIUtils.GetMainWindowRect();
                float width = Mathf.Clamp(500f, 500f, Mathf.Max(400f, mainRect.width - 100f));
                float height = Mathf.Clamp(600f, 200f, Mathf.Max(200f, mainRect.height - 100f));
                float x = mainRect.x + (mainRect.width - width) / 2f;
                float y = mainRect.y + (mainRect.height - height) / 2f;
                newWindow.position = new Rect(x, y, width, height);

                // Show as popup window
                newWindow.ShowPopup();
                newWindow.Focus();
                _activePopup = newWindow;

                // Navigate to default base if one is set, then focus search bar
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        // Navigate to default base if assigned
                        var profile = Profile.Active;
                        if (profile != null && profile.DefaultBase != null)
                        {
                            newWindow.SetTarget(profile.DefaultBase);
                        }

                        // After navigation completes: expand all foldouts (if possible) and then focus the search bar
                        EditorApplication.delayCall += () =>
                        {
                            try
                            {
                                // Try to expand the tree/foldouts using reflection so this is robust across versions
                                System.Type bwType = typeof(BrowserWindow);

                                // Attempt to find a TreeView field/property on the BrowserWindow instance
                                object treeObj = null;
                                var treeField = bwType.GetField("treeView",
                                                    System.Reflection.BindingFlags.NonPublic |
                                                    System.Reflection.BindingFlags.Instance)
                                                ?? bwType.GetField("_treeView",
                                                    System.Reflection.BindingFlags.NonPublic |
                                                    System.Reflection.BindingFlags.Instance);
                                if (treeField != null)
                                    treeObj = treeField.GetValue(newWindow);

                                // If we have a tree object, try common expand methods on it
                                if (treeObj != null)
                                {
                                    var tryNames = new[]
                                    {
                                        "ExpandAll", "ExpandAllNodes", "ExpandRecursive", "ExpandAllFoldouts",
                                        "ExpandAllItems", "ExpandToDepth"
                                    };
                                    foreach (var name in tryNames)
                                    {
                                        var m = treeObj.GetType().GetMethod(name,
                                            System.Reflection.BindingFlags.Public |
                                            System.Reflection.BindingFlags.NonPublic |
                                            System.Reflection.BindingFlags.Instance);
                                        if (m != null)
                                        {
                                            var pars = m.GetParameters();
                                            if (pars.Length == 0)
                                            {
                                                m.Invoke(treeObj, null);
                                                break;
                                            }

                                            // If it takes an int depth parameter, pass a large value to expand fully
                                            if (pars.Length == 1 && pars[0].ParameterType == typeof(int))
                                            {
                                                m.Invoke(treeObj, new object[] {int.MaxValue});
                                                break;
                                            }
                                        }
                                    }
                                }

                                // As a fallback, try methods directly on BrowserWindow
                                var bwTryNames = new[] {"ExpandAll", "ExpandAllFoldouts", "ExpandRecursive"};
                                foreach (var name in bwTryNames)
                                {
                                    var m = bwType.GetMethod(name,
                                        System.Reflection.BindingFlags.Public |
                                        System.Reflection.BindingFlags.NonPublic |
                                        System.Reflection.BindingFlags.Instance);
                                    if (m != null)
                                    {
                                        var pars = m.GetParameters();
                                        if (pars.Length == 0)
                                        {
                                            m.Invoke(newWindow, null);
                                            break;
                                        }

                                        if (pars.Length == 1 && pars[0].ParameterType == typeof(int))
                                        {
                                            m.Invoke(newWindow, new object[] {int.MaxValue});
                                            break;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                            }

                            newWindow.FocusSearchBar();
                        };
                    }
                    catch
                    {
                    }
                };
            }
            catch
            {
                // Fallback to regular window
                BrowserWindow.OpenWindow<SecondBrainWindow>();
            }
        }
    }
}
