using SecretZauce.SecondBrain.Editor;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    /// <summary>
    /// Global keyboard shortcut handler for opening property editor.
    /// This works even when the Browser window is not focused.
    /// </summary>
    public static class PropertyEditorHelper
    {
        [Shortcut("Browser/Open Property Editor", KeyCode.A)]
        static void OpenPropertyEditorForHoveredItem()
        {
            // Find the browser window that the mouse is currently over
            var mouseOverWindow = EditorWindow.mouseOverWindow;
            if (mouseOverWindow is BrowserWindow browserWindow)
            {
                try
                {
                    // Don't open property editor during text editing, renaming, or dragging
                    var treeView = browserWindow.TreeView;
                    if (treeView == null)
                        return;

                    if (EditorGUIUtility.editingTextField || 
                        treeView.Renamer?.IsRenamingAny == true || 
                        treeView.DragDropManager?.IsDragging == true)
                        return;

                    // Check if we have a hovered item
                    if (treeView.DragInput?.HasHover != true)
                        return;

                    var hoveredPath = treeView.DragInput.HoveredPath;
                    if (hoveredPath == null)
                        return;

                    OpenPropertyEditorFor(hoveredPath, browserWindow);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Failed to open property editor via shortcut: {ex.Message}");
                }
            }
        }

        public static void OpenPropertyEditorFor(int[] hoveredPath, BrowserWindow browserWindow)
        {
            var obj = browserWindow.TreeView.GetObjectAtPath(hoveredPath);
            if (obj == null)
                return;

            OpenPropertyEditorFor(obj, browserWindow);
        }

        static void OpenPropertyEditorFor(Object obj, BrowserWindow browserWindow)
        {
            // Special-case Base: although it implements IStructure, treat it as a leaf
            // and open its property editor instead of the container children window.
            // For containers (IStructure and not Base), open a window showing all first-level children.
            // For SceneObjectRef with loaded scene, open locked inspector for the actual scene object.
            // For other leaf items, open the standard property editor.
            if (obj is Base)
            {
                var toOpen = obj;
                EditorApplication.delayCall += () =>
                {
                    try { EditorUtility.OpenPropertyEditor(toOpen); }
                    catch (System.Exception ex) { Debug.LogWarning($"Failed to open property editor via shortcut: {ex.Message}"); }
                    try { browserWindow.Repaint(); } catch { }
                };
            }
            else if (obj is IStructure container)
            {
                ContainerChildrenInspector.Open(container);
            }
            else if (obj is SceneObjectRef sceneRef)
            {
                var sceneObj = sceneRef.sceneObject;
                string sceneName = sceneObj?.LastKnownScene;
                string sceneGuid  = sceneObj?.LastKnownSceneGuid;
                bool sceneLoaded = IsSceneLoaded(sceneGuid, sceneName);
                if (sceneLoaded)
                {
                    var go = SceneObjectMap.Resolve(sceneObj);
                    var toOpen = go != null ? (Object)go : sceneRef;
                    EditorApplication.delayCall += () =>
                    {
                        try { EditorUtility.OpenPropertyEditor(toOpen); }
                        catch (System.Exception ex) { Debug.LogWarning($"Failed to open property editor via shortcut: {ex.Message}"); }
                        try { browserWindow.Repaint(); } catch { }
                    };
                }
                else
                {
                    var toOpen = sceneRef;
                    EditorApplication.delayCall += () =>
                    {
                        try { EditorUtility.OpenPropertyEditor(toOpen); }
                        catch (System.Exception ex) { Debug.LogWarning($"Failed to open property editor via shortcut: {ex.Message}"); }
                        try { browserWindow.Repaint(); } catch { }
                    };
                }
            }
            else if (obj is SceneComponentRef sceneComponentRef)
            {
                var sceneComponent = sceneComponentRef.sceneComponent;
                string sceneName = sceneComponent?.LastKnownScene;
                string sceneGuid  = sceneComponent?.LastKnownSceneGuid;
                bool sceneLoaded = IsSceneLoaded(sceneGuid, sceneName);
                if (sceneLoaded)
                {
                    var component = SceneObjectMap.Resolve(sceneComponent);
                    var toOpen = component != null ? (Object)component : sceneComponentRef;
                    EditorApplication.delayCall += () =>
                    {
                        try { EditorUtility.OpenPropertyEditor(toOpen); }
                        catch (System.Exception ex) { Debug.LogWarning($"Failed to open property editor via shortcut: {ex.Message}"); }
                        try { browserWindow.Repaint(); } catch { }
                    };
                }
                else
                {
                    var toOpen = sceneComponentRef;
                    EditorApplication.delayCall += () =>
                    {
                        try { EditorUtility.OpenPropertyEditor(toOpen); }
                        catch (System.Exception ex) { Debug.LogWarning($"Failed to open property editor via shortcut: {ex.Message}"); }
                        try { browserWindow.Repaint(); } catch { }
                    };
                }
            }
            else
            {
                var toOpen = obj;
                EditorApplication.delayCall += () =>
                {
                    try { EditorUtility.OpenPropertyEditor(toOpen); }
                    catch (System.Exception ex) { Debug.LogWarning($"Failed to open property editor via shortcut: {ex.Message}"); }
                    try { browserWindow.Repaint(); } catch { }
                };
            }
        }

        static bool IsSceneLoaded(string sceneGuid, string sceneName)
            => SceneLoadUtils.IsSceneLoaded(sceneGuid, sceneName);
    }
}
