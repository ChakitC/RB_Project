using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using SecretZauce.SecondBrain.Editor;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    /// <summary>
    /// Custom EditorWindow that displays all first-level children of a Container
    /// in a tabbed interface. Each child is shown with its own inspector/editor.
    /// Triggered via the A shortcut when hovering over a Container item in the Browser.
    /// </summary>
    public class ContainerChildrenInspector : EditorWindow
    {
        // OS-level mouse button polling — detects release even when MouseUp wasn't routed here.
#if UNITY_EDITOR_WIN
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        static bool IsLeftMouseButtonHeld() => (GetAsyncKeyState(0x01) & 0x8000) != 0;
#elif UNITY_EDITOR_OSX
        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        static extern bool CGEventSourceButtonState(int stateID, int button);
        static bool IsLeftMouseButtonHeld() { try { return CGEventSourceButtonState(1, 0); } catch { return true; } }
#else
        static bool IsLeftMouseButtonHeld() => true;
#endif

        private List<Object> children;
        private List<UnityEditor.Editor> editors = new List<UnityEditor.Editor>();
        private enum LayoutMode { Tabs = 0, Foldouts = 1 }
        private LayoutMode layoutMode = LayoutMode.Tabs;
        private bool showLayoutToggle = true;
        private List<bool> foldoutStates = new List<bool>();
        // Persisted foldout state helper (shared with QuickPeekWindow via same storage key)
        private FoldoutState persistedFoldoutState;
        private FoldoutAnimationState foldoutAnimState;
        private Vector2 scrollPosition;
        private Vector2 foldoutScrollPosition;
        private Vector2 tabScrollPosition;
        private int selectedTab;
        private IHasChildViewPreference containerPref;
        private Object containerAsset;
        private bool foldoutDirty;

        // Drag-mode state: set when the window is opened via a QuickPeek header drag.
        private bool isDragging;
        private Vector2 dragWindowOffset;   // mouse-screen-pos relative to window top-left at drag start
        private Vector2 lastMouseScreenPos; // updated every OnGUI frame; used by OnDragUpdate

        /// <summary>
        /// Opens a custom editor window displaying all first-level children of <paramref name="container"/>
        /// in a tabbed interface. Does nothing when the container is null or has no children.
        /// </summary>
        public static void Open(IStructure container)
        {
            if (container == null)
                return;

            var children = container.ChildrenObjects;
            if (children == null || children.Count == 0)
                return;

            var window = GetWindow<ContainerChildrenInspector>("Container Children");
            window.containerPref = container as IHasChildViewPreference;
            window.containerAsset = container as Object;
            // Initialize animation state for smooth foldout transitions
            window.foldoutAnimState = new FoldoutAnimationState(() => window.Repaint());
            // Hide the layout toggle for Base containers and force Tabs layout
            window.showLayoutToggle = !(container is Base);
            // Initialize layout mode from container preference if available and allowed
            if (window.showLayoutToggle && window.containerPref != null)
                window.layoutMode = window.containerPref.PreferredChildView == ChildViewMode.Foldouts ? LayoutMode.Foldouts : LayoutMode.Tabs;
            else
                window.layoutMode = BrowserSettings.DefaultQuickPeekLayout == ChildViewMode.Foldouts ? LayoutMode.Foldouts : LayoutMode.Tabs;
            window.children = children.Where(c => c != null).ToList();
            window.CreateEditors();
            // Load persisted foldout states from EditorPrefs (shared key with QuickPeekWindow)
            try
            {
                window.persistedFoldoutState = new FoldoutState();
                var key = window.GetFoldoutPrefsKey();
                if (!string.IsNullOrEmpty(key))
                    window.persistedFoldoutState.LoadFromEditorPrefs(key, new[] { container });

                // Populate the per-child foldout boolean list from the persisted state (if present)
                if (window.persistedFoldoutState != null)
                {
                    window.foldoutStates = new List<bool>(window.children.Count);
                    for (int i = 0; i < window.children.Count; ++i)
                    {
                        var child = window.children[i];
                        var ps = window.persistedFoldoutState;
                        bool defExp = ((window.containerAsset as Container)?.ChildViewExpand ?? DefaultExpandOption.ExpandAsDefault) != DefaultExpandOption.CollapsedAsDefault;
                        window.foldoutStates.Add(ps.HasKey(child) ? ps.Get(child) : defExp);
                    }
                }
                // If using Tabs layout, attempt to load previously selected tab index
                if (window.layoutMode == LayoutMode.Tabs)
                {
                    try
                    {
                        var tabKey = window.GetTabPrefsKey();
                        if (!string.IsNullOrEmpty(tabKey) && EditorPrefs.HasKey(tabKey))
                            window.selectedTab = Mathf.Clamp(EditorPrefs.GetInt(tabKey, 0), 0, window.editors.Count - 1);
                    }
                    catch { }
                }
            }
            catch { }
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        /// <summary>
        /// Opens a ContainerChildrenInspector positioned and sized to match <paramref name="startRect"/>
        /// (the QuickPeek popup that was just closed) and moves it with the mouse until the button
        /// is released, giving a natural tear-off feel.
        /// </summary>
        public static void OpenWithDrag(IStructure container, Rect startRect, Vector2 dragOffset)
        {
            if (container == null) return;
            var children = container.ChildrenObjects?.Where(c => c != null).ToList();
            if (children == null || children.Count == 0) return;

            var window = CreateInstance<ContainerChildrenInspector>();
            window.titleContent = new GUIContent("Container Children");
            window.containerPref = container as IHasChildViewPreference;
            window.containerAsset = container as Object;
            window.foldoutAnimState = new FoldoutAnimationState(() => window.Repaint());
            window.showLayoutToggle = !(container is Base);

            if (window.showLayoutToggle && window.containerPref != null)
                window.layoutMode = window.containerPref.PreferredChildView == ChildViewMode.Foldouts
                    ? LayoutMode.Foldouts : LayoutMode.Tabs;
            else
                window.layoutMode = BrowserSettings.DefaultQuickPeekLayout == ChildViewMode.Foldouts
                    ? LayoutMode.Foldouts : LayoutMode.Tabs;

            window.children = children;
            window.CreateEditors();

            try
            {
                window.persistedFoldoutState = new FoldoutState();
                var key = window.GetFoldoutPrefsKey();
                if (!string.IsNullOrEmpty(key))
                    window.persistedFoldoutState.LoadFromEditorPrefs(key, new[] { container });

                if (window.persistedFoldoutState != null)
                {
                    window.foldoutStates = new List<bool>(window.children.Count);
                    bool defExp = ((window.containerAsset as Container)?.ChildViewExpand ?? DefaultExpandOption.ExpandAsDefault) != DefaultExpandOption.CollapsedAsDefault;
                    foreach (var child in window.children)
                        window.foldoutStates.Add(
                            window.persistedFoldoutState.HasKey(child)
                                ? window.persistedFoldoutState.Get(child)
                                : defExp);
                }

                if (window.layoutMode == LayoutMode.Tabs)
                {
                    var tabKey = window.GetTabPrefsKey();
                    if (!string.IsNullOrEmpty(tabKey) && EditorPrefs.HasKey(tabKey))
                        window.selectedTab = Mathf.Clamp(EditorPrefs.GetInt(tabKey, 0), 0, window.editors.Count - 1);
                }
            }
            catch { }

            // Start in drag mode: window follows the mouse until MouseUp is received.
            window.isDragging = true;
            window.dragWindowOffset  = dragOffset;
            window.lastMouseScreenPos = new Vector2(startRect.x + dragOffset.x, startRect.y + dragOffset.y);
            window.wantsMouseMove = true;

            // Match the QuickPeek popup size exactly so the transition feels seamless.
            window.minSize = startRect.size;
            window.maxSize = new Vector2(startRect.width * 4f, 10000f);

            window.ShowUtility();
            window.position = startRect;

            // Poll every editor frame to reposition the window while dragging.
            EditorApplication.update += window.OnDragUpdate;
        }

        void OnDragUpdate()
        {
            if (!isDragging)
            {
                EditorApplication.update -= OnDragUpdate;
                return;
            }

            // Detect mouse release via OS polling — handles the case where MouseUp was not
            // delivered to this window because the button was pressed before the window existed.
            if (!IsLeftMouseButtonHeld())
            {
                isDragging = false;
                minSize = new Vector2(Mathf.Min(position.width, 300f), 200f);
                maxSize = new Vector2(4000f, 10000f);
                EditorApplication.update -= OnDragUpdate;
                Repaint();
                return;
            }

            // Move the window so the grab point stays under the cursor.
            var target = new Rect(lastMouseScreenPos - dragWindowOffset, position.size);
            if (Mathf.Abs(target.x - position.x) > 0.5f || Mathf.Abs(target.y - position.y) > 0.5f)
                position = target;
            Repaint();
        }

        void CreateEditors()
        {
            ClearEditors();
            if (children == null)
                return;

            foreach (var child in children)
            {
                if (child != null)
                {
                    var editor = UnityEditor.Editor.CreateEditor(child);
                    if (editor != null)
                        editors.Add(editor);
                }
            }

            bool defaultExpanded = ((containerAsset as Container)?.ChildViewExpand ?? DefaultExpandOption.ExpandAsDefault) != DefaultExpandOption.CollapsedAsDefault;
            foldoutStates = new List<bool>(children.Count);
            for (int i = 0; i < children.Count; ++i)
                foldoutStates.Add(defaultExpanded);
        }

        private string GetFoldoutPrefsKey() => QuickPeekSharedUI.GetFoldoutPrefsKey(containerAsset);

        private string GetTabPrefsKey() => QuickPeekSharedUI.GetTabPrefsKey(containerAsset);

        void ClearEditors()
        {
            foreach (var editor in editors)
            {
                if (editor != null)
                    DestroyImmediate(editor);
            }
            editors.Clear();
        }

        void OnDestroy()
        {
            // Ensure the drag-update hook is removed even if the window is closed mid-drag.
            EditorApplication.update -= OnDragUpdate;
            ClearEditors();
            if (foldoutDirty)
                SaveFoldoutPrefs();
        }

        void OnGUI()
        {
            // Track mouse position every GUI frame so OnDragUpdate can reposition the window.
            if (isDragging)
            {
                var e = Event.current;
                if (e != null && e.type != EventType.Layout)
                    lastMouseScreenPos = GUIUtility.GUIToScreenPoint(e.mousePosition);
                if (e != null && e.type == EventType.MouseUp)
                {
                    isDragging = false;
                    // Restore normal size constraints without snapping — keep current width as minimum.
                    minSize = new Vector2(Mathf.Min(position.width, 300f), 200f);
                    maxSize = new Vector2(4000f, 10000f);
                }
            }

            if (children == null || children.Count == 0 || editors.Count == 0)
            {
                EditorGUILayout.HelpBox("No children to display.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Space(4f);
            GUILayout.Label(containerAsset != null ? containerAsset.name : "Container Children", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            var tb = QuickPeekSharedUI.DrawLayoutToolbar((int)layoutMode, showLayoutToggle, foldoutStates);
            EditorGUILayout.EndHorizontal();

            if (tb.LayoutChanged)
            {
                layoutMode = (LayoutMode)tb.NewLayout;
                scrollPosition = Vector2.zero;
                foldoutScrollPosition = Vector2.zero;
                selectedTab = Mathf.Clamp(selectedTab, 0, editors.Count - 1);

                if (containerPref != null && containerAsset != null)
                {
                    Undo.RecordObject(containerAsset, "Change Child View Mode");
                    containerPref.PreferredChildView = layoutMode == LayoutMode.Foldouts ? ChildViewMode.Foldouts : ChildViewMode.Tabs;
                    EditorUtility.SetDirty(containerAsset);
                    AssetDatabase.SaveAssets();
                }

                if (layoutMode == LayoutMode.Tabs)
                {
                    try
                    {
                        var tabKey = GetTabPrefsKey();
                        if (!string.IsNullOrEmpty(tabKey) && EditorPrefs.HasKey(tabKey))
                            selectedTab = Mathf.Clamp(EditorPrefs.GetInt(tabKey, 0), 0, editors.Count - 1);
                    }
                    catch { }
                }
            }

            if (tb.ExpandAllClicked) ExpandAllFoldouts();
            if (tb.CollapseAllClicked) CollapseAllFoldouts();
            if (tb.SettingsClicked)
            {
                try
                {
                    var obj = containerAsset as Object;
                    if (obj != null)
                        EditorUtility.OpenPropertyEditor(obj);
                }
                catch { }
            }

            if (layoutMode == LayoutMode.Tabs)
            {
                var tabContents = children.Select(c => c != null ? ItemUtils.BuildTabContent(c) : new GUIContent("Unnamed")).ToArray();
                int newTab = QuickPeekSharedUI.DrawTabsBar(selectedTab, tabContents, ref tabScrollPosition);
                if (newTab != selectedTab)
                {
                    scrollPosition = Vector2.zero;
                    selectedTab = newTab;
                    try
                    {
                        var tabKey = GetTabPrefsKey();
                        if (!string.IsNullOrEmpty(tabKey))
                            EditorPrefs.SetInt(tabKey, selectedTab);
                    }
                    catch { }
                }
            }

            selectedTab = Mathf.Clamp(selectedTab, 0, editors.Count - 1);

            EditorGUILayout.Space(5);

            if (layoutMode == LayoutMode.Tabs)
            {
                QuickPeekSharedUI.DrawTabContent(editors, selectedTab, ref scrollPosition, padding: 14f, verticalSpacing: 0f);
            }
            else
            {
                bool stateChanged = QuickPeekSharedUI.DrawFoldoutsContent(
                    children, editors, foldoutStates,
                    ref foldoutScrollPosition, QuickPeekSharedUI.FoldoutListOptions.EditorWindow, foldoutAnimState);
                if (stateChanged) foldoutDirty = true;
            }

            if (foldoutDirty)
                SaveFoldoutPrefs();
        }

        // Expand all foldouts in the window and mark prefs dirty so they are saved.
        void ExpandAllFoldouts()
        {
            if (foldoutStates == null) foldoutStates = new List<bool>();
            QuickPeekSharedUI.SetAllFoldouts(foldoutStates, children?.Count ?? 0, true);
            foldoutDirty = true;
            SaveFoldoutPrefs();
            Repaint();
        }

        // Collapse all foldouts in the window and mark prefs dirty so they are saved.
        void CollapseAllFoldouts()
        {
            if (foldoutStates == null) foldoutStates = new List<bool>();
            QuickPeekSharedUI.SetAllFoldouts(foldoutStates, children?.Count ?? 0, false);
            foldoutDirty = true;
            SaveFoldoutPrefs();
            Repaint();
        }

        // Save current foldout states into EditorPrefs using FoldoutState (shared key)
        void SaveFoldoutPrefs()
        {
            if (children == null || foldoutStates == null) return;

            var temp = new FoldoutState();
            for (int i = 0; i < children.Count && i < foldoutStates.Count; ++i)
            {
                var child = children[i];
                if (child == null) continue;
                temp.Set(child, foldoutStates[i]);
            }

            var key = GetFoldoutPrefsKey();
            if (!string.IsNullOrEmpty(key))
            {
                try { temp.SaveToEditorPrefs(key); }
                catch { }
            }

            foldoutDirty = false;
        }
    }
}
