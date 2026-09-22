using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using System;
using SecretZauce.SecondBrain.Editor;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    /// <summary>
    /// Floating inspector popup shown when hovering over a TreeView item.
    /// For IStructure (Container) nodes, shows a ContainerChildren-style tabbed/foldout view
    /// using the container's preferred layout mode.
    /// For leaf nodes, shows the standard property inspector.
    /// Closes when the mouse leaves both the hovered row and this popup.
    /// </summary>
    public class QuickPeekWindow : EditorWindow
    {
        // OS-level mouse button polling — detects release even when MouseUp wasn't routed here.
#if UNITY_EDITOR_WIN
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        static bool IsLeftMouseButtonHeld() => (GetAsyncKeyState(0x01) & 0x8000) != 0;

        // Monitor detection — physical pixel coordinates, DPI-aware process.
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] struct WIN_RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)] struct MONITORINFO
        {
            public int    cbSize;
            public WIN_RECT rcMonitor;
            public WIN_RECT rcWork;
            public uint   dwFlags;
        }
        const uint MONITOR_DEFAULTTONEAREST = 2;
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
        [DllImport("user32.dll")] static extern bool   GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
#elif UNITY_EDITOR_OSX
        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        static extern bool CGEventSourceButtonState(int stateID, int button);
        static bool IsLeftMouseButtonHeld() { try { return CGEventSourceButtonState(1, 0); } catch { return true; } }
#else
        static bool IsLeftMouseButtonHeld() => true;
#endif

        static QuickPeekWindow instance;

        // ── Delayed-show state ────────────────────────────────────────────────
        // How long the mouse must hover before the window pops up (seconds).
        const double SHOW_DELAY = 0.2;
        // How long the fade-in animation lasts (seconds).
        const float  FADE_DURATION = 0.15f;

        static bool   hasPendingShow;
        static double pendingShowTime;          // absolute editor time when we should show
        static Object pendingTargetObject;
        static Rect   pendingItemScreenRect;
        static Rect   pendingBrowserScreenRect;
        static QuickPeekSide pendingPreferredSide;

        // True after the user clicks inside the peek window (e.g. to open a color picker or
        // object selector dialog). While set, HandleQuickPeekCloseCheck suppresses the close
        // countdown so the peek survives while the external dialog is open.
        // Cleared when the mouse returns to the peek or a browser peek-hover zone.
        static bool hasPendingExternalInteraction;
        public static bool HasPendingExternalInteraction => hasPendingExternalInteraction;
        public static void ClearExternalInteraction() => hasPendingExternalInteraction = false;
        public static bool HasPendingShow => hasPendingShow;

        // Set to EditorApplication.timeSinceStartup when the window is actually shown.
        double showTime = double.MinValue;
        // ─────────────────────────────────────────────────────────────────────

        Object targetObject;
        IStructure containerObject;
        IHasChildViewPreference containerPref;
        Rect sourceBrowserScreenRect;

        // Persisted foldout state helper when showing containers in Foldouts mode
        FoldoutState persistedFoldoutState;
        FoldoutAnimationState foldoutAnimState;

        // Single-object inspector state
        UnityEditor.Editor singleEditor;
        // True when singleEditor uses UIElements (CreateInspectorGUI returns non-null).
        // In that case OnInspectorGUI() is a no-op and we fall back to DrawDefaultInspector.
        bool singleEditorIsUIElements;
        // True for any leaf asset that cannot be accurately edited inside a floating popup —
        // either because its inspector is UIElements-based, or because it is a known asset type
        // (Material, Texture, Sprite, Shader) where the IMGUI fallback is incomplete or misleading.
        bool isLimitedPreviewAsset;
        Vector2 singleScrollPos;

        // TextAsset / URL inline editing state
        bool isTextAsset;
        bool isUrlTextAsset;
        string textAssetEditContent;
        string textAssetOriginalContent;
        Vector2 textAssetScrollPos;

        // Container children view state
        List<Object> containerChildren;
        List<UnityEditor.Editor> containerEditors = new List<UnityEditor.Editor>();
        enum LayoutMode { Tabs = 0, Foldouts = 1 }
        LayoutMode layoutMode = LayoutMode.Tabs;
        int selectedTab;
        Vector2 tabScrollPos;
        Vector2 contentScrollPos;
        Vector2 foldoutScrollPos;
        List<bool> foldoutStates = new List<bool>();
        bool isContainer;
        bool isBaseContainer;

        // Header drag-to-popup state
        bool headerDragPending;
        Vector2 headerDragStartPos;
        const float HEADER_DRAG_THRESHOLD = 6f;

        // Detached (tear-off) mode: opened as ShowUtility() so it persists independently.
        bool isDetached;
        bool detachedDragging;
        Vector2 detachedDragOffset;
        Vector2 detachedLastMouseScreenPos;

        // Force position on first frame to work around Unity's ShowPopup() clamping
        bool needsPositionForce;
        Rect desiredPosition;

        // SceneObjectRef / prefab asset component view state
        bool isSceneObjectRef;
        // Set alongside isSceneObjectRef when the target is a prefab asset (persistent GameObject).
        bool isPrefabAsset;
        GameObject resolvedGameObject;
        List<Component> sceneObjectComponents;
        List<UnityEditor.Editor> componentEditors = new List<UnityEditor.Editor>();
        List<bool> componentFoldoutStates = new List<bool>();
        HashSet<string> hiddenComponentTypeNames = new HashSet<string>();
        Vector2 sceneObjectScrollPos;
        bool showComponentFilterDropdown;
        string sceneObjectPrefsKey;
        string sceneObjectTabPrefsKey;
        string sceneObjectFoldoutPrefsKey;
        string sceneObjectLayoutPrefsKey;

        const float PEEK_WIDTH = 360f;
        const float PEEK_HEIGHT = 400f;
        // Compact height for asset types whose inspector is UIElements-based (Material, Sprite, etc.)
        // Content: header (~21) + top gap (2) + preview (120) + spacing (8) + type label (13) + spacing (6) + button (24) + bottom spacing (4) + padding
        const float PEEK_HEIGHT_LIMITED = 220f;

        // EditorPrefs key written by BrowserWindow whenever a base is entered.
        // Each QuickPeek foldout save also writes "{foldoutKey}_session" with the
        // current value so we can detect stale history on the next open.
        internal const string QuickPeekSessionKey = "QuickPeek_SessionToken";

        static bool IsFoldoutHistoryValid(string foldoutKey)
        {
            if (string.IsNullOrEmpty(foldoutKey)) return false;
            int current = EditorPrefs.GetInt(QuickPeekSessionKey, 0);
            int saved   = EditorPrefs.GetInt(foldoutKey + "_session", -1);
            return saved == current;
        }

        static void MarkFoldoutHistoryCurrentSession(string foldoutKey)
        {
            if (string.IsNullOrEmpty(foldoutKey)) return;
            EditorPrefs.SetInt(foldoutKey + "_session", EditorPrefs.GetInt(QuickPeekSessionKey, 0));
        }

        static GUIStyle s_CompLabelStyle;
        static GUIStyle s_CompMinusStyle;
        static Texture  s_TabIcon;
        static Texture  s_FoldoutIcon;
        static Texture  s_SettingsIcon;
        static Texture  s_HideIcon;
        static Texture  s_ExpandIcon;
        static Texture  s_CollapseIcon;
        static bool     s_LayoutIconsProSkin;

        public static QuickPeekWindow Instance => instance;

        /// <summary>
        /// Shows or refreshes the QuickPeekWindow for the given object.
        /// If the window is already open for the same object, only the position is updated.
        /// </summary>
        /// <param name="obj">The object to preview.</param>
        /// <param name="itemScreenRect">Screen-space rect of the hovered row.</param>
        /// <param name="browserScreenRect">Screen-space rect of the BrowserWindow.</param>
        /// <param name="preferredSide">Which side to open on (Left/Right). None = auto-detect from available space.</param>
        public static void Show(Object obj, Rect itemScreenRect, Rect browserScreenRect, QuickPeekSide preferredSide = QuickPeekSide.None)
        {
            if (obj == null)
            {
                CancelPendingShow();
                CloseInstance();
                return;
            }

            // For SceneObjectRef: don't show quick peek when the scene object is not in any opened scene.
            if (obj is SceneObjectRef earlyRef)
            {
                try
                {
                    // Typed overload, not the GlobalId one: it falls back to the hierarchy path, so
                    // a ref whose object survived Play mode in a moved or flattened hierarchy still
                    // opens instead of the peek being cancelled as unresolvable.
                    var earlyGo = SceneObjectMap.Resolve(earlyRef.sceneObject);
                    if (earlyGo == null)
                    {
                        CancelPendingShow();
                        CloseInstance();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    // Resolution failed (e.g. invalid global ID format) — skip the quick peek.
                    Debug.LogWarning($"QuickPeekWindow: failed to resolve SceneObjectRef '{obj.name}': {ex.Message}");
                    CancelPendingShow();
                    CloseInstance();
                    return;
                }
            }

            if (obj is SceneComponentRef earlyComponentRef)
            {
                try
                {
                    var earlyComponent = SceneObjectMap.Resolve(earlyComponentRef.sceneComponent);
                    if (earlyComponent == null)
                    {
                        CancelPendingShow();
                        CloseInstance();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"QuickPeekWindow: failed to resolve SceneComponentRef '{obj.name}': {ex.Message}");
                    CancelPendingShow();
                    CloseInstance();
                    return;
                }
            }

            // If the popup is already open, keep it alive and switch content immediately when the
            // hovered item changes. This avoids reintroducing the hover delay every time the mouse
            // moves from one row to the next.
            if (instance != null)
            {
                CancelPendingShow();

                // If the target changed, rebuild the popup content in-place instead of closing and
                // restarting the delayed show flow. This keeps item-to-item hover transitions crisp.
                if (!ReferenceEquals(instance.targetObject, obj))
                {
                    instance.targetObject = obj;
                    instance.sourceBrowserScreenRect = browserScreenRect;
                    instance.InitializeFor(obj);
                    instance.PositionWindow(itemScreenRect, browserScreenRect, preferredSide);

                    // Skip the fade overlay for target switches so the content update feels instant.
                    instance.showTime = EditorApplication.timeSinceStartup - FADE_DURATION;
                    instance.Repaint();
                    return;
                }

                // Same object: just reposition if the mouse is still on the tree row
                // (not over the peek itself, since the user may be interacting with it).
                if (mouseOverWindow != instance)
                    instance.PositionWindow(itemScreenRect, browserScreenRect, preferredSide);
                return;
            }

            // If a different object is pending, cancel it and reset the timer.
            // If the same object is already pending just update the rects so it appears in
            // the right place when the timer fires.
            if (hasPendingShow && ReferenceEquals(pendingTargetObject, obj))
            {
                pendingItemScreenRect    = itemScreenRect;
                pendingBrowserScreenRect = browserScreenRect;
                pendingPreferredSide     = preferredSide;
                return;
            }

            // New hover target — cancel any in-flight pending show and start the appearance delay.
            CancelPendingShow();

            pendingTargetObject      = obj;
            pendingItemScreenRect    = itemScreenRect;
            pendingBrowserScreenRect = browserScreenRect;
            pendingPreferredSide     = preferredSide;
            pendingShowTime          = EditorApplication.timeSinceStartup + SHOW_DELAY;
            hasPendingShow           = true;
            EditorApplication.update += CheckPendingShow;
        }

        // Called every editor update tick until the delay elapses.
        static void CheckPendingShow()
        {
            if (!hasPendingShow)
            {
                EditorApplication.update -= CheckPendingShow;
                return;
            }

            if (EditorApplication.timeSinceStartup < pendingShowTime)
                return;

            // Delay elapsed — capture locals and clear pending state before creating window
            // so that any exception in InitializeFor doesn't leave the hook dangling.
            var obj         = pendingTargetObject;
            var itemRect    = pendingItemScreenRect;
            var browserRect = pendingBrowserScreenRect;
            var side        = pendingPreferredSide;
            CancelPendingShow();

            if (obj == null)
                return;

            CloseInstance();
            instance = CreateInstance<QuickPeekWindow>();
            instance.targetObject = obj;
            instance.sourceBrowserScreenRect = browserRect;
            instance.InitializeFor(obj);
            instance.PositionWindow(itemRect, browserRect, side);
            instance.showTime = EditorApplication.timeSinceStartup;
            instance.ShowPopup();
            // Force the unclamped position after ShowPopup (Unity clamps it to screen bounds)
            instance.position = instance.desiredPosition;
            // Keep the tree-column info icon in sync with the popup open state immediately.
            instance.TryRepaintOriginBrowser();
        }

        static void CancelPendingShow()
        {
            if (!hasPendingShow)
                return;
            EditorApplication.update -= CheckPendingShow;
            hasPendingShow    = false;
            pendingTargetObject = null;
        }

        /// <summary>Closes the QuickPeekWindow if it is currently open and cancels any pending delayed show.</summary>
        public static void CloseInstance()
        {
            CancelPendingShow();
            hasPendingExternalInteraction = false;
            if (instance == null)
                return;
            try
            {
                // Save any foldout state before disposing editors so we can restore on next open
                try { instance.SaveFoldoutStateIfNeeded(); } catch { }
                instance.ClearEditors();
                instance.Close();
            }
            catch
            {
                // Ignore errors if the window is already destroyed
            }
            instance = null;
        }

        /// <summary>
        /// Opens a persistent (detached) QuickPeekWindow for <paramref name="obj"/> at
        /// <paramref name="startRect"/>, following the mouse until the button is released.
        /// Used when the user drags the header of a hover-peek for leaf or Base nodes.
        /// Unlike the normal hover-peek (ShowPopup), this window survives focus loss.
        /// </summary>
        public static void OpenDetached(Object obj, Rect startRect, Vector2 dragOffset)
        {
            if (obj == null) return;

            var win = CreateInstance<QuickPeekWindow>();
            win.targetObject = obj;
            win.isDetached = true;
            win.detachedDragging = true;
            win.detachedDragOffset = dragOffset;
            win.detachedLastMouseScreenPos = new Vector2(startRect.x + dragOffset.x, startRect.y + dragOffset.y);
            win.sourceBrowserScreenRect = startRect;
            win.InitializeFor(obj);
            win.showTime = EditorApplication.timeSinceStartup - FADE_DURATION; // skip fade-in
            win.wantsMouseMove = true;
            win.titleContent = new GUIContent(obj.name);
            win.minSize = startRect.size;
            win.maxSize = new Vector2(startRect.width * 4f, 10000f);
            win.ShowUtility();
            win.position = startRect;
            EditorApplication.update += win.OnDetachedDragUpdate;
        }

        void OnDetachedDragUpdate()
        {
            if (!detachedDragging || !isDetached)
            {
                EditorApplication.update -= OnDetachedDragUpdate;
                return;
            }

            // Detect mouse release via OS polling — handles the case where MouseUp was not
            // delivered to this window because the button was pressed before the window existed.
            if (!IsLeftMouseButtonHeld())
            {
                detachedDragging = false;
                minSize = new Vector2(PEEK_WIDTH, 100f);
                maxSize = new Vector2(PEEK_WIDTH, 10000f);
                EditorApplication.update -= OnDetachedDragUpdate;
                Repaint();
                return;
            }

            var target = new Rect(detachedLastMouseScreenPos - detachedDragOffset, position.size);
            if (Mathf.Abs(target.x - position.x) > 0.5f || Mathf.Abs(target.y - position.y) > 0.5f)
                position = target;
            Repaint();
        }

        void PositionWindow(Rect itemScreenRect, Rect browserScreenRect, QuickPeekSide preferredSide = QuickPeekSide.None)
        {
            float x;
            float y = itemScreenRect.y;

            // ppp needed both for monitor selection (convert points→px) and edge-space math.
            float ppp = EditorGUIUtility.pixelsPerPoint;

            // Determine monitor bounds for the browser window. Prefer System.Windows.Forms' screen
            // information (better for multi-monitor and DPI setups on Windows). Fall back to
            // Screen.currentResolution if unavailable.
            Rect monitorRect = new Rect(0, 0, Screen.currentResolution.width, Screen.currentResolution.height);

            // Use Win32 MonitorFromPoint/GetMonitorInfo to resolve the physical monitor that
            // contains the browser window. These APIs always return physical-pixel coordinates
            // in a DPI-aware process (which Unity Editor is), making them reliable on any
            // multi-monitor / mixed-DPI setup without assembly-loading concerns.
#if UNITY_EDITOR_WIN
            try
            {
                var browserCenterPhys = new POINT
                {
                    x = (int)((browserScreenRect.x + browserScreenRect.width  * 0.5f) * ppp),
                    y = (int)((browserScreenRect.y + browserScreenRect.height * 0.5f) * ppp),
                };
                IntPtr hMonitor = MonitorFromPoint(browserCenterPhys, MONITOR_DEFAULTTONEAREST);
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    monitorRect = new Rect(
                        mi.rcMonitor.left,
                        mi.rcMonitor.top,
                        mi.rcMonitor.right  - mi.rcMonitor.left,
                        mi.rcMonitor.bottom - mi.rcMonitor.top);
                }
            }
            catch
            {
                // Fall back to Screen.currentResolution (already set)
            }
#endif

            // Use the hovered peek zone side to determine which side to open on.
            // When the user hovers the left zone → open left; right zone → open right.
            // Fall back to available-space logic when no explicit side is given.
            bool preferLeft = false;
            if (preferredSide == QuickPeekSide.Left)
                preferLeft = true;

            if (preferLeft)
            {
                x = browserScreenRect.x - PEEK_WIDTH - 2f;
            }
            else
            {
                x = browserScreenRect.xMax + 2f;
            }

            // monitorRect is in physical pixels (from Screen.Bounds or currentResolution).
            // Divide by ppp to convert to logical points, matching browserScreenRect's space.
            float edgeSpaceLeft  = browserScreenRect.x - monitorRect.xMin / ppp;
            float edgeSpaceRight = monitorRect.xMax / ppp - browserScreenRect.xMax;
            // When a vertical scrollbar is visible, itemScreenRect.xMax sits scrollbarWidth px to
            // the left of browserScreenRect.xMax.  Add that offset so the inset is measured from
            // the content-area right edge, not the window's outer right edge.
            float rightScrollbarW = Mathf.Max(0f, browserScreenRect.xMax - itemScreenRect.xMax - 2f);

            bool fallbackLeft  = edgeSpaceLeft  < PEEK_WIDTH && preferLeft;
            bool fallbackRight = edgeSpaceRight < PEEK_WIDTH && !preferLeft;
            if (fallbackLeft)
                x += PEEK_WIDTH + 23f;
            else if (fallbackRight)
                x -= PEEK_WIDTH + 23f + rightScrollbarW;

            // Store the desired position before Unity's ShowPopup clamps it
            bool isLimitedPreview = isLimitedPreviewAsset;
            float peekHeight = isLimitedPreview ? PEEK_HEIGHT_LIMITED : PEEK_HEIGHT;
            if (!isDetached)
            {
                if (isLimitedPreview)
                {
                    minSize = new Vector2(PEEK_WIDTH, PEEK_HEIGHT_LIMITED);
                    maxSize = new Vector2(PEEK_WIDTH, PEEK_HEIGHT_LIMITED);
                }
                else
                {
                    minSize = new Vector2(PEEK_WIDTH, 100f);
                    maxSize = new Vector2(PEEK_WIDTH, 10000f);
                }
            }
            desiredPosition = new Rect(x, y, PEEK_WIDTH, peekHeight);
            needsPositionForce = true;
            position = desiredPosition;
        }

        void InitializeFor(Object obj)
        {
            // Clear any previous preview state so switching targets in an existing window does not
            // leak stale editors, foldout settings, or scene-object UI state into the next item.
            ClearEditors();
            ResetPreviewState();

            // Ensure animation state is initialized
            if (foldoutAnimState == null)
                foldoutAnimState = new FoldoutAnimationState(() => Repaint());
            
            // Special-case SceneObjectRef: show per-component foldout editors for the resolved GameObject.
            // The Show() method already guarantees resolution succeeded before we get here.
            if (obj is SceneObjectRef sceneRef)
            {
                try
                {
                    var go = SceneObjectMap.Resolve(sceneRef.sceneObject);
                    if (go != null)
                    {
                        isContainer = false;
                        isSceneObjectRef = true;
                        resolvedGameObject = go;

                        // Load persisted component visibility filter
                        sceneObjectPrefsKey = GetComponentFilterPrefsKey(sceneRef);
                        hiddenComponentTypeNames = LoadComponentFilterPrefs(sceneObjectPrefsKey);

                        // Load persisted component foldout states
                        sceneObjectFoldoutPrefsKey = GetComponentFoldoutPrefsKey(sceneRef);
                        // Load persisted selected tab index for Tabs mode
                        sceneObjectTabPrefsKey = GetComponentTabPrefsKey(sceneRef);
                        // Load persisted layout mode (Tabs vs Foldouts) if present
                        sceneObjectLayoutPrefsKey = GetComponentLayoutPrefsKey(sceneRef);

                        // Build component + editor lists.
                        // sceneObjectComponents, componentEditors, and componentFoldoutStates are kept
                        // strictly parallel (same length, nulls at matching positions) so that index i
                        // can be used across all three lists without offset.
                        sceneObjectComponents = new List<Component>(go.GetComponents<Component>());
                        componentEditors = new List<UnityEditor.Editor>();
                        componentFoldoutStates = new List<bool>();

                        // Load saved foldout states only when they belong to the current session
                        // (i.e. the same base-entry as now). Stale history from a previous visit
                        // is discarded so the component default (expand) takes effect instead.
                        bool compHistoryValid = IsFoldoutHistoryValid(sceneObjectFoldoutPrefsKey);
                        var savedFoldoutStates = compHistoryValid
                            ? LoadComponentFoldoutPrefs(sceneObjectFoldoutPrefsKey)
                            : new Dictionary<string, bool>();
                        bool defaultCompExpanded = true;

                        foreach (var comp in sceneObjectComponents)
                        {
                            if (comp == null)
                            {
                                componentEditors.Add(null);
                                componentFoldoutStates.Add(false);
                                continue;
                            }
                            var editor = UnityEditor.Editor.CreateEditor(comp);
                            componentEditors.Add(editor);

                            string typeName = comp.GetType().Name;
                            bool foldoutState = savedFoldoutStates.ContainsKey(typeName)
                                ? savedFoldoutStates[typeName]
                                : defaultCompExpanded;
                            componentFoldoutStates.Add(foldoutState);
                        }
                        // Load persisted layout mode only when it belongs to the current session
                        try
                        {
                            bool layoutHistoryValid = IsFoldoutHistoryValid(sceneObjectLayoutPrefsKey);
                            if (layoutHistoryValid && !string.IsNullOrEmpty(sceneObjectLayoutPrefsKey) && EditorPrefs.HasKey(sceneObjectLayoutPrefsKey))
                            {
                                int savedMode = EditorPrefs.GetInt(sceneObjectLayoutPrefsKey, (int)LayoutMode.Tabs);
                                layoutMode = savedMode == (int)LayoutMode.Foldouts ? LayoutMode.Foldouts : LayoutMode.Tabs;
                            }
                            else
                            {
                                layoutMode = BrowserSettings.DefaultQuickPeekLayout == ChildViewMode.Foldouts ? LayoutMode.Foldouts : LayoutMode.Tabs;
                            }
                        }
                        catch { }

                        if (layoutMode == LayoutMode.Tabs)
                        {
                            try
                            {
                                if (!string.IsNullOrEmpty(sceneObjectTabPrefsKey) && EditorPrefs.HasKey(sceneObjectTabPrefsKey))
                                    selectedTab = Mathf.Clamp(EditorPrefs.GetInt(sceneObjectTabPrefsKey, 0), 0, componentEditors.Count - 1);
                                else
                                    selectedTab = Mathf.Clamp(selectedTab, 0, componentEditors.Count - 1);
                            }
                            catch { selectedTab = Mathf.Clamp(selectedTab, 0, componentEditors.Count - 1); }
                        }
                        return;
                    }
                }
                catch
                {
                    // Ignore resolution failures and fall back to default behaviour
                }
            }

            // Prefab asset: treat like SceneObjectRef and show per-component foldout editors.
            // GameObjectInspector in Unity 6 is UIElements-first and draws nothing when called
            // via OnInspectorGUI() from an IMGUI window, so this component-list approach is
            // the reliable alternative.
            if (obj is GameObject prefabGo && EditorUtility.IsPersistent(obj))
            {
                isSceneObjectRef = true;
                isPrefabAsset = true;
                resolvedGameObject = prefabGo;

                string prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
                sceneObjectPrefsKey        = !string.IsNullOrEmpty(prefabGuid) ? $"QuickPeek_CompFilter_Prefab_{prefabGuid}" : null;
                sceneObjectFoldoutPrefsKey = !string.IsNullOrEmpty(prefabGuid) ? $"QuickPeek_CompFoldout_Prefab_{prefabGuid}" : null;
                sceneObjectTabPrefsKey     = !string.IsNullOrEmpty(prefabGuid) ? $"QuickPeek_CompTabs_Prefab_{prefabGuid}" : null;
                sceneObjectLayoutPrefsKey  = !string.IsNullOrEmpty(prefabGuid) ? $"QuickPeek_CompLayout_Prefab_{prefabGuid}" : null;

                hiddenComponentTypeNames = LoadComponentFilterPrefs(sceneObjectPrefsKey);
                sceneObjectComponents = new List<Component>(prefabGo.GetComponents<Component>());
                componentEditors = new List<UnityEditor.Editor>();
                componentFoldoutStates = new List<bool>();

                bool compHistoryValid = IsFoldoutHistoryValid(sceneObjectFoldoutPrefsKey);
                var savedFoldoutStates = compHistoryValid
                    ? LoadComponentFoldoutPrefs(sceneObjectFoldoutPrefsKey)
                    : new Dictionary<string, bool>();

                foreach (var comp in sceneObjectComponents)
                {
                    if (comp == null) { componentEditors.Add(null); componentFoldoutStates.Add(false); continue; }
                    componentEditors.Add(UnityEditor.Editor.CreateEditor(comp));
                    string typeName = comp.GetType().Name;
                    componentFoldoutStates.Add(savedFoldoutStates.ContainsKey(typeName) ? savedFoldoutStates[typeName] : true);
                }

                try
                {
                    bool layoutHistoryValid = IsFoldoutHistoryValid(sceneObjectLayoutPrefsKey);
                    if (layoutHistoryValid && !string.IsNullOrEmpty(sceneObjectLayoutPrefsKey) && EditorPrefs.HasKey(sceneObjectLayoutPrefsKey))
                        layoutMode = EditorPrefs.GetInt(sceneObjectLayoutPrefsKey, (int)LayoutMode.Tabs) == (int)LayoutMode.Foldouts ? LayoutMode.Foldouts : LayoutMode.Tabs;
                    else
                        layoutMode = BrowserSettings.DefaultQuickPeekLayout == ChildViewMode.Foldouts ? LayoutMode.Foldouts : LayoutMode.Tabs;
                }
                catch { }

                if (layoutMode == LayoutMode.Tabs)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(sceneObjectTabPrefsKey) && EditorPrefs.HasKey(sceneObjectTabPrefsKey))
                            selectedTab = Mathf.Clamp(EditorPrefs.GetInt(sceneObjectTabPrefsKey, 0), 0, componentEditors.Count - 1);
                        else
                            selectedTab = Mathf.Clamp(selectedTab, 0, componentEditors.Count - 1);
                    }
                    catch { selectedTab = Mathf.Clamp(selectedTab, 0, componentEditors.Count - 1); }
                }
                return;
            }

            isContainer = obj is IStructure;

            if (isContainer)
            {
                var container = (IStructure)obj;
                containerObject = container;
                containerPref = container is IHasChildViewPreference p ? p : null;
                var children = container.ChildrenObjects?.Where(c => c != null).ToList() ?? new List<Object>();
                containerChildren = children;

                // If this is a `Base` container type, don't show the layout toggle and force Tabs
                isBaseContainer = obj is Base;

                // Special-case Base: show the Base object's own inspector in the quick peek
                // instead of rendering the container-children UI (and avoid opening the
                // external ContainerGroupEditorWindow). Create a single-object editor and
                // treat this window as a single-object inspector.
                if (isBaseContainer)
                {
                    try
                    {
                        singleEditor = UnityEditor.Editor.CreateEditor(obj);
                    }
                    catch { singleEditor = null; }
                    // Render as a single object in OnGUI
                    isContainer = false;
                    return;
                }

                // Respect the container's preferred child view layout; otherwise use global default
                if (container is IHasChildViewPreference pref)
                    layoutMode = pref.PreferredChildView == ChildViewMode.Foldouts ? LayoutMode.Foldouts : LayoutMode.Tabs;
                else
                    layoutMode = BrowserSettings.DefaultQuickPeekLayout == ChildViewMode.Foldouts ? LayoutMode.Foldouts : LayoutMode.Tabs;

                containerEditors = new List<UnityEditor.Editor>();
                foreach (var child in containerChildren)
                {
                    if (child == null) continue;
                    var editor = UnityEditor.Editor.CreateEditor(child);
                    if (editor != null)
                        containerEditors.Add(editor);
                }

                foldoutStates = new List<bool>(containerChildren.Count);

                // Load persisted foldout state only when it belongs to the current session
                persistedFoldoutState = null;
                bool foldoutHistoryValid = false;
                if (layoutMode == LayoutMode.Foldouts)
                {
                    try
                    {
                        string fkey = GetFoldoutPrefsKey();
                        persistedFoldoutState = new FoldoutState();
                        if (!string.IsNullOrEmpty(fkey))
                        {
                            persistedFoldoutState.LoadFromEditorPrefs(fkey, new[] { container });
                            foldoutHistoryValid = IsFoldoutHistoryValid(fkey);
                        }
                    }
                    catch { persistedFoldoutState = null; }
                }

                bool defaultExpanded = ((containerObject as Container)?.ChildViewExpand ?? DefaultExpandOption.ExpandAsDefault) != DefaultExpandOption.CollapsedAsDefault;
                for (int i = 0; i < containerChildren.Count; i++)
                {
                    var child = containerChildren[i];
                    if (foldoutHistoryValid && persistedFoldoutState != null && persistedFoldoutState.HasKey(child))
                        foldoutStates.Add(persistedFoldoutState.Get(child));
                    else
                        foldoutStates.Add(defaultExpanded);
                }

                // If using Tabs layout, attempt to load previously selected tab index
                if (layoutMode == LayoutMode.Tabs)
                {
                    try
                    {
                        string tabKey = GetTabPrefsKey();
                        if (!string.IsNullOrEmpty(tabKey) && EditorPrefs.HasKey(tabKey))
                            selectedTab = Mathf.Clamp(EditorPrefs.GetInt(tabKey, 0), 0, containerEditors.Count - 1);
                        else
                            selectedTab = Mathf.Clamp(selectedTab, 0, containerEditors.Count - 1);
                    }
                    catch { selectedTab = Mathf.Clamp(selectedTab, 0, containerEditors.Count - 1); }
                }

                // Ensure the foldoutStates list is exactly the right size (guards against
                // any child-count mismatch between containerChildren and containerEditors)
                while (foldoutStates.Count < containerChildren.Count) foldoutStates.Add(false);
            }
            else if (obj is TextAsset textAssetObj)
            {
                isTextAsset = true;
                isUrlTextAsset = LeafNodeActionHelper.IsUrl(textAssetObj.text);
                textAssetEditContent = textAssetObj.text ?? string.Empty;
                textAssetOriginalContent = textAssetEditContent;
            }
            else
            {
                singleEditor = UnityEditor.Editor.CreateEditor(obj);
                singleEditorIsUIElements = DetectIsUIElementsEditor(singleEditor);
                // Unity 6's GenericInspector is UIElements-based, so plain ScriptableObjects
                // are detected as UIElements editors. For SOs we fall back to DrawDefaultInspector()
                // (IMGUI) in DrawSingleObjectContent instead of showing the limited asset preview.
                bool isUIElementsNonSO = singleEditorIsUIElements && !(obj is ScriptableObject);
                isLimitedPreviewAsset = isUIElementsNonSO || IsLimitedPreviewType(obj);
            }
        }

        // Returns true when the editor uses UIElements (CreateInspectorGUI returns a VisualElement).
        // Called once per InitializeFor — never per-frame — because CreateInspectorGUI allocates.
        static bool DetectIsUIElementsEditor(UnityEditor.Editor editor)
        {
            if (editor == null) return false;
            try { return editor.CreateInspectorGUI() != null; }
            catch { return false; }
        }

        // Returns true for asset types whose built-in IMGUI inspector is incomplete or inaccurate
        // when rendered inside a floating IMGUI popup. These are shown with DrawLimitedAssetPreview
        // regardless of whether their editor is UIElements-based.
        static bool IsLimitedPreviewType(Object obj)
        {
            if (obj == null) return false;
            return obj is Material
                || obj is Texture        // Texture2D, RenderTexture, Cubemap, Texture3D …
                || obj is Sprite
                || obj is Shader
                || obj is ComputeShader;
        }

        void ResetPreviewState()
        {
            containerObject = null;
            containerPref = null;
            persistedFoldoutState = null;
            containerChildren = null;
            foldoutStates = null;
            isContainer = false;
            isBaseContainer = false;
            isSceneObjectRef = false;
            isPrefabAsset = false;
            singleEditorIsUIElements = false;
            isLimitedPreviewAsset = false;
            isTextAsset = false;
            isUrlTextAsset = false;
            textAssetEditContent = null;
            textAssetOriginalContent = null;
            textAssetScrollPos = Vector2.zero;
            resolvedGameObject = null;
            sceneObjectComponents = null;
            componentFoldoutStates = null;
            hiddenComponentTypeNames = new HashSet<string>();
            showComponentFilterDropdown = false;
            sceneObjectPrefsKey = null;
            sceneObjectTabPrefsKey = null;
            sceneObjectFoldoutPrefsKey = null;
            sceneObjectLayoutPrefsKey = null;
            layoutMode = LayoutMode.Tabs;
            selectedTab = 0;
            tabScrollPos = Vector2.zero;
            contentScrollPos = Vector2.zero;
            foldoutScrollPos = Vector2.zero;
            singleScrollPos = Vector2.zero;
            sceneObjectScrollPos = Vector2.zero;

            if (foldoutAnimState != null)
                foldoutAnimState.Clear();
        }

        void SaveFoldoutStateIfNeeded()
        {
            // Save container foldout states
            if (containerObject != null)
            {
                // Only persist when currently using Foldouts layout
                if (layoutMode != LayoutMode.Foldouts)
                    return;

                if (containerChildren == null || foldoutStates == null)
                    return;

                try
                {
                    var temp = new FoldoutState();
                    for (int i = 0; i < containerChildren.Count && i < foldoutStates.Count; i++)
                    {
                        var child = containerChildren[i];
                        if (child == null) continue;
                        temp.Set(child, foldoutStates[i]);
                    }

                    string key = GetFoldoutPrefsKey();
                    if (!string.IsNullOrEmpty(key))
                    {
                        temp.SaveToEditorPrefs(key);
                        MarkFoldoutHistoryCurrentSession(key);
                    }
                }
                catch (Exception)
                {
                    // swallow exceptions during save
                }
            }

            // Save SceneObjectRef component foldout states
            if (isSceneObjectRef && sceneObjectComponents != null && componentFoldoutStates != null)
            {
                try
                {
                    SaveComponentFoldoutPrefs();
                }
                catch (Exception)
                {
                    // swallow exceptions during save
                }
            }
        }

        string GetFoldoutPrefsKey() => QuickPeekSharedUI.GetFoldoutPrefsKey(containerObject as Object);

        string GetTabPrefsKey() => QuickPeekSharedUI.GetTabPrefsKey(containerObject as Object);

        void SavePreferredChildView(LayoutMode lm)
        {
            if (containerPref == null)
                return;

            var desired = lm == LayoutMode.Foldouts ? ChildViewMode.Foldouts : ChildViewMode.Tabs;
            if (containerPref.PreferredChildView == desired)
                return;

            Undo.RecordObject((Object)containerPref, "Change Child View Preference");
            containerPref.PreferredChildView = desired;
            EditorUtility.SetDirty((Object)containerPref);
            AssetDatabase.SaveAssets();
        }

        void ClearEditors()
        {
            if (singleEditor != null)
            {
                DestroyImmediate(singleEditor);
                singleEditor = null;
            }

            if (containerEditors != null)
            {
                foreach (var e in containerEditors)
                    if (e != null) DestroyImmediate(e);
                containerEditors.Clear();
            }

            if (componentEditors != null)
            {
                foreach (var e in componentEditors)
                    if (e != null) DestroyImmediate(e);
                componentEditors.Clear();
            }
        }

        // Expand all foldouts in the quick peek and persist the change.
        void ExpandAllFoldouts()
        {
            if (foldoutStates == null) foldoutStates = new List<bool>();
            QuickPeekSharedUI.SetAllFoldouts(foldoutStates, containerChildren?.Count ?? 0, true);
            try { SaveFoldoutStateIfNeeded(); } catch { }
            Repaint();
        }

        // Collapse all foldouts in the quick peek and persist the change.
        void CollapseAllFoldouts()
        {
            if (foldoutStates == null) foldoutStates = new List<bool>();
            QuickPeekSharedUI.SetAllFoldouts(foldoutStates, containerChildren?.Count ?? 0, false);
            try { SaveFoldoutStateIfNeeded(); } catch { }
            Repaint();
        }

        // Expand all component foldouts for SceneObjectRef and persist
        void ExpandAllComponentFoldouts()
        {
            if (componentFoldoutStates == null) componentFoldoutStates = new List<bool>();
            QuickPeekSharedUI.SetAllFoldouts(componentFoldoutStates, sceneObjectComponents?.Count ?? 0, true);
            try { SaveComponentFoldoutPrefs(); } catch { }
            Repaint();
        }

        // Collapse all component foldouts for SceneObjectRef and persist
        void CollapseAllComponentFoldouts()
        {
            if (componentFoldoutStates == null) componentFoldoutStates = new List<bool>();
            QuickPeekSharedUI.SetAllFoldouts(componentFoldoutStates, sceneObjectComponents?.Count ?? 0, false);
            try { SaveComponentFoldoutPrefs(); } catch { }
            Repaint();
        }

        void OnDestroy()
        {
            // Save foldout state before clearing editors so state can be restored on next open
            try { SaveFoldoutStateIfNeeded(); } catch { }
            ClearEditors();
            EditorApplication.update -= OnDetachedDragUpdate;
            if (instance == this)
            {
                instance = null;
                hasPendingExternalInteraction = false;
            }
        }

        void OnEnable()
        {
            wantsMouseMove = true;
            // Initialize animation state for smooth foldout transitions
            if (foldoutAnimState == null)
                foldoutAnimState = new FoldoutAnimationState(() => Repaint());
            // Lock horizontal size so the window never resizes horizontally.
            minSize = new Vector2(PEEK_WIDTH, 100f);
            maxSize = new Vector2(PEEK_WIDTH, 10000f);
        }

        void OnGUI()
        {
            // Force the unclamped position on first frame(s) to override Unity's ShowPopup clamping
            if (needsPositionForce && Event.current.type == EventType.Layout)
            {
                position = desiredPosition;
                needsPositionForce = false;
            }

            // In detached drag mode, track mouse screen position for OnDetachedDragUpdate.
            if (isDetached && detachedDragging)
            {
                var de = Event.current;
                if (de != null && de.type != EventType.Layout)
                    detachedLastMouseScreenPos = GUIUtility.GUIToScreenPoint(de.mousePosition);
                if (de != null && de.type == EventType.MouseUp)
                {
                    detachedDragging = false;
                    minSize = new Vector2(PEEK_WIDTH, 100f);
                    maxSize = new Vector2(PEEK_WIDTH, 10000f);
                }
            }

            // Track clicks so HandleQuickPeekCloseCheck can keep the window alive while
            // external dialogs (color picker, object selector) spawned from here are open.
            // Not needed for detached windows — they are not managed by the close-check loop.
            if (!isDetached && Event.current.type == EventType.MouseDown)
                hasPendingExternalInteraction = true;

            // Draw a solid background so content is readable over other windows
            Color bg = EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.18f, 0.18f)
                : new Color(0.86f, 0.86f, 0.86f);
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), bg);

            // Draw a 1-pixel border
            Color border = EditorGUIUtility.isProSkin
                ? new Color(0.1f, 0.1f, 0.1f)
                : new Color(0.6f, 0.6f, 0.6f);
            EditorGUI.DrawRect(new Rect(0, 0, position.width, 1), border);
            EditorGUI.DrawRect(new Rect(0, position.height - 1, position.width, 1), border);
            EditorGUI.DrawRect(new Rect(0, 0, 1, position.height), border);
            EditorGUI.DrawRect(new Rect(position.width - 1, 0, 1, position.height), border);

            EditorGUILayout.Space(2);

            if (isSceneObjectRef)
                DrawSceneObjectContent();
            else if (isContainer)
                DrawContainerContent();
            else
                DrawSingleObjectContent();

            // ── Info icon at header edge ──────────────────────────────────────
            // Drawn after content so it sits on top of the header toolbar.
            // Position is derived entirely from this window's own rect and the
            // stored browser rect — no tree-row coordinate conversion needed.
            // ─────────────────────────────────────────────────────────────────

            // ── Fade-in overlay ───────────────────────────────────────────────
            // Drawn last so it sits on top of all content.  Alpha goes from 1→0
            // over FADE_DURATION seconds, making the window appear to fade in.
            float fadeElapsed = (float)(EditorApplication.timeSinceStartup - showTime);
            if (fadeElapsed < FADE_DURATION)
            {
                float alpha = 1f - Mathf.Clamp01(fadeElapsed / FADE_DURATION);
                Color fadeColor = EditorGUIUtility.isProSkin
                    ? new Color(0.18f, 0.18f, 0.18f, alpha)
                    : new Color(0.86f, 0.86f, 0.86f, alpha);
                EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), fadeColor);
                Repaint(); // keep repainting until animation finishes
            }
            // ─────────────────────────────────────────────────────────────────
        }

        // Cached info icon texture — loaded once and reused.
        static GUIContent s_PeekInfoIcon;

        void DrawContainerContent()
        {
            if (containerChildren == null || containerChildren.Count == 0 || containerEditors.Count == 0)
            {
                EditorGUILayout.HelpBox("No children to display.", MessageType.Info);
                return;
            }
            // Header: object icon + name on the left, layout toolbar / actions on the right
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            // (No icon for non-SceneObjectRef peeks)
            GUILayout.Space(4);
            GUILayout.Label((containerObject as Object)?.name ?? "Unnamed", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();

            var tb = QuickPeekSharedUI.DrawLayoutToolbar((int)layoutMode, !isBaseContainer, foldoutStates);
            if (tb.LayoutChanged)
            {
                layoutMode = (LayoutMode)tb.NewLayout;
                contentScrollPos = Vector2.zero;
                foldoutScrollPos = Vector2.zero;
                selectedTab = Mathf.Clamp(selectedTab, 0, containerEditors.Count - 1);
                SavePreferredChildView(layoutMode);

                if (layoutMode == LayoutMode.Tabs)
                {
                    try
                    {
                        string tabKey = GetTabPrefsKey();
                        if (!string.IsNullOrEmpty(tabKey) && EditorPrefs.HasKey(tabKey))
                            selectedTab = Mathf.Clamp(EditorPrefs.GetInt(tabKey, 0), 0, containerEditors.Count - 1);
                    }
                    catch { }
                }
                else
                {
                    // Switching to Foldouts: restore session-valid history or use default
                    string fkey = GetFoldoutPrefsKey();
                    bool histValid = IsFoldoutHistoryValid(fkey);
                    bool exp = ((containerObject as Container)?.ChildViewExpand ?? DefaultExpandOption.ExpandAsDefault) != DefaultExpandOption.CollapsedAsDefault;
                    if (histValid && !string.IsNullOrEmpty(fkey) && containerObject != null)
                    {
                        try
                        {
                            persistedFoldoutState = new FoldoutState();
                            persistedFoldoutState.LoadFromEditorPrefs(fkey, new[] { containerObject });
                            if (containerChildren != null)
                                for (int i = 0; i < containerChildren.Count && i < foldoutStates.Count; i++)
                                {
                                    var child = containerChildren[i];
                                    foldoutStates[i] = persistedFoldoutState.HasKey(child)
                                        ? persistedFoldoutState.Get(child) : exp;
                                }
                        }
                        catch { }
                    }
                    else if (foldoutStates != null)
                        for (int i = 0; i < foldoutStates.Count; i++)
                            foldoutStates[i] = exp;
                }
            }

            if (tb.ExpandAllClicked) ExpandAllFoldouts();
            if (tb.CollapseAllClicked) CollapseAllFoldouts();
            if (tb.SettingsClicked)
            {
                try
                {
                    var obj = containerObject as Object;
                    if (obj != null) EditorUtility.OpenPropertyEditor(obj);
                }
                catch { }
            }

            EditorGUILayout.EndHorizontal();
            // Handle double-click on header: convert quick peek to full editor
            var headerRect = GUILayoutUtility.GetLastRect();
            // Closes this window and disposes containerEditors — nothing below may run.
            if (HandleHeaderInteraction(headerRect)) return;

            if (layoutMode == LayoutMode.Tabs)
            {
                var tabContents = containerChildren.Select(c => c != null ? ItemUtils.BuildTabContent(c) : new GUIContent("Unnamed")).ToArray();
                int newTab = QuickPeekSharedUI.DrawTabsBar(selectedTab, tabContents, ref tabScrollPos);
                if (newTab != selectedTab)
                {
                    contentScrollPos = Vector2.zero;
                    selectedTab = newTab;
                    try
                    {
                        string tabKey = GetTabPrefsKey();
                        if (!string.IsNullOrEmpty(tabKey))
                            EditorPrefs.SetInt(tabKey, selectedTab);
                    }
                    catch { }
                }
            }

            selectedTab = Mathf.Clamp(selectedTab, 0, containerEditors.Count - 1);
            EditorGUILayout.Space(3);

            if (layoutMode == LayoutMode.Tabs)
                QuickPeekSharedUI.DrawTabContent(containerEditors, selectedTab, ref contentScrollPos, 14f, suppressHorizontalScroll: true);
            else
                QuickPeekSharedUI.DrawFoldoutsContent(containerChildren, containerEditors, foldoutStates, ref foldoutScrollPos, QuickPeekSharedUI.FoldoutListOptions.QuickPeek, foldoutAnimState);
        }

        void DrawSingleObjectContent()
        {
            if (singleEditor == null && !isTextAsset)
            {
                EditorGUILayout.HelpBox("No inspector available.", MessageType.Info);
                return;
            }

            // Header: show object icon + name and settings button
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            Texture headerIcon = null;
            string headerLabel = targetObject != null ? targetObject.name : "Unnamed";
            Component resolvedComponent = null;
            if (targetObject is SceneComponentRef sceneComponentRef)
            {
                resolvedComponent = SceneObjectMap.Resolve(sceneComponentRef.sceneComponent);
                if (resolvedComponent != null)
                {
                    var iconContent = EditorGUIUtility.ObjectContent(resolvedComponent, resolvedComponent.GetType());
                    headerIcon = iconContent?.image;
                    headerLabel = resolvedComponent.GetType().Name;
                }
                else
                {
                    headerIcon = ItemUtils.GetIconForNode(sceneComponentRef);
                    headerLabel = string.IsNullOrEmpty(sceneComponentRef.sceneComponent?.LastKnownComponentTypeName)
                        ? targetObject.name
                        : sceneComponentRef.sceneComponent.LastKnownComponentTypeName;
                }
            }

            if (headerIcon != null)
            {
                GUILayout.Label(new GUIContent(headerIcon), GUILayout.Width(18), GUILayout.Height(18));
            }
            else
            {
                GUILayout.Space(4);
            }

            if (resolvedComponent is Behaviour behaviour)
            {
                bool wasEnabled = behaviour.enabled;
                bool isEnabled = EditorGUILayout.Toggle(wasEnabled, GUILayout.Width(16));
                if (isEnabled != wasEnabled)
                {
                    Undo.RecordObject(behaviour, "Toggle Component Enabled");
                    behaviour.enabled = isEnabled;
                    EditorUtility.SetDirty(behaviour);
                }
            }

            GUILayout.Space(4);
            GUILayout.Label(TrimParenthesizedSuffix(headerLabel), EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            var settingsBtnContent2 = s_SettingsIcon != null ? new GUIContent(s_SettingsIcon, "Settings") : EditorGUIUtility.IconContent("d_SettingsIcon");
            if (GUILayout.Button(settingsBtnContent2, EditorStyles.toolbarButton, GUILayout.Width(24)))
            {
                try
                {
                    Object toOpen = targetObject;
                    if (targetObject is SceneComponentRef componentRefForSettings)
                    {
                        var component = SceneObjectMap.Resolve(componentRefForSettings.sceneComponent);
                        if (component != null)
                            toOpen = component;
                    }

                    if (toOpen != null)
                        EditorUtility.OpenPropertyEditor(toOpen);
                }
                catch { }
            }
            EditorGUILayout.EndHorizontal();
            // Handle double-click on single-object header
            var singleHeaderRect = GUILayoutUtility.GetLastRect();
            // Dragging the header out detaches the peek, which closes this window and destroys
            // singleEditor. Everything below dereferences it, so stop here.
            if (HandleHeaderInteraction(singleHeaderRect)) return;

            if (isTextAsset)
            {
                DrawTextAssetContent();
                return;
            }

            if (isLimitedPreviewAsset)
            {
                DrawLimitedAssetPreview();
                return;
            }

            singleScrollPos = EditorGUILayout.BeginScrollView(singleScrollPos, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(14f);
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Space(4f);

            // The editor can still have been disposed between the guard at the top of this method
            // and here, by anything above that closes the window mid-draw.
            if (singleEditor == null)
            {
                GUILayout.Space(4f);
                EditorGUILayout.EndVertical();
                GUILayout.Space(14f);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndScrollView();
                return;
            }

            // IMGUI editor: draw the asset preview (Texture, Sprite, AudioClip, etc.)
            // above the standard inspector panel. HasPreviewGUI() is false for plain
            // ScriptableObjects so this is a no-op for most SecondBrain nodes.
            if (singleEditor.HasPreviewGUI())
            {
                float h = Mathf.Min(128f, position.width * 0.5f);
                Rect previewRect = GUILayoutUtility.GetRect(1f, h, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.15f));
                singleEditor.OnPreviewGUI(previewRect, GUIStyle.none);
                EditorGUILayout.Space(4f);
            }
            // UIElements editors return nothing from OnInspectorGUI in IMGUI windows.
            // For ScriptableObjects, fall back to DrawDefaultInspector so serialized fields render.
            if (singleEditorIsUIElements && targetObject is ScriptableObject)
                singleEditor.DrawDefaultInspector();
            else
                singleEditor.OnInspectorGUI();

            GUILayout.Space(4f);
            EditorGUILayout.EndVertical();
            GUILayout.Space(14f);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        static GUIStyle s_TextAreaStyle;

        void DrawTextAssetContent()
        {
            textAssetScrollPos = EditorGUILayout.BeginScrollView(
                textAssetScrollPos, false, false,
                GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(14f);
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Space(4f);

            if (isUrlTextAsset)
            {
                EditorGUILayout.LabelField("URL", EditorStyles.miniLabel);
                string newUrl = EditorGUILayout.TextField(textAssetEditContent ?? string.Empty);
                if (newUrl != textAssetEditContent)
                    textAssetEditContent = newUrl;

                GUILayout.Space(6f);

                bool validUrl = LeafNodeActionHelper.IsUrl(textAssetEditContent);
                using (new EditorGUI.DisabledGroupScope(!validUrl))
                {
                    if (GUILayout.Button("Open URL", GUILayout.Height(24)))
                        Application.OpenURL(textAssetEditContent.Trim());
                }
            }
            else
            {
                if (s_TextAreaStyle == null)
                    s_TextAreaStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };

                string newText = EditorGUILayout.TextArea(
                    textAssetEditContent ?? string.Empty,
                    s_TextAreaStyle,
                    GUILayout.ExpandHeight(true),
                    GUILayout.MinHeight(100f));
                if (newText != textAssetEditContent)
                    textAssetEditContent = newText;
            }

            GUILayout.Space(6f);

            bool hasChanges = textAssetEditContent != textAssetOriginalContent;
            Color prevColor = GUI.color;
            if (!hasChanges) GUI.color = new Color(1f, 1f, 1f, 0.4f);
            if (GUILayout.Button("Save", GUILayout.Height(24)))
            {
                // Capture now; defer the write so the peek survives any repaint/close
                // that the button click may trigger before MouseUp is fully processed.
                string capturedContent = textAssetEditContent;
                var capturedAsset = targetObject as TextAsset;
                EditorApplication.delayCall += () => CommitTextAssetSave(capturedAsset, capturedContent);
                textAssetOriginalContent = textAssetEditContent;
                GUIEventUtils.OnRenameDone(false); 
                Repaint();
            }
            GUI.color = prevColor;

            GUILayout.Space(4f);
            EditorGUILayout.EndVertical();
            GUILayout.Space(14f);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        static void CommitTextAssetSave(TextAsset textAsset, string content)
        {
            if (textAsset == null) return;
            try
            {
                string assetPath = AssetDatabase.GetAssetPath(textAsset);

                // TextAsset was never persisted — created before FinalizeNewChild fix.
                if (string.IsNullOrEmpty(assetPath))
                {
                    Debug.LogWarning("[QuickPeek] TextAsset is not registered as an asset and cannot be saved. Remove and re-add the item to persist it.");
                    return;
                }

                // Standalone .txt file: write directly to disk and reimport.
                string ext = System.IO.Path.GetExtension(assetPath);
                if (!string.IsNullOrEmpty(ext) && string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase))
                {
                    string fullPath = System.IO.Path.GetFullPath(assetPath);
                    System.IO.File.WriteAllText(fullPath, content ?? string.Empty, new System.Text.UTF8Encoding(false));
                    AssetDatabase.ImportAsset(assetPath);
                    return;
                }

                // Sub-asset embedded in a parent .asset file: modify via Unity serialisation.
                // TextAsset.text is read-only; go through SerializedObject to bypass that.
                // Try the known property name first, then fall back to scanning all strings.
                var so = new SerializedObject(textAsset);
                var textProp = so.FindProperty("m_Script");
                if (textProp == null || textProp.propertyType != SerializedPropertyType.String)
                {
                    var iter = so.GetIterator();
                    string currentText = textAsset.text ?? string.Empty;
                    while (iter.Next(true))
                    {
                        if (iter.propertyType == SerializedPropertyType.String
                            && iter.stringValue == currentText)
                        {
                            textProp = iter.Copy();
                            break;
                        }
                    }
                }

                if (textProp != null)
                {
                    textProp.stringValue = content ?? string.Empty;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(textAsset);
                    AssetDatabase.SaveAssets();
                    return;
                }

                Debug.LogError("[QuickPeek] Cannot save TextAsset: no editable string property found.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuickPeek] Save failed: {ex.Message}");
            }
        }

        // Compact view for asset types whose inspector is UIElements-based (Material, Sprite, etc.).
        // OnInspectorGUI() is a no-op for these in IMGUI windows and DrawDefaultInspector() only
        // shows raw serialized fields — neither matches what the user sees in the real inspector.
        // Instead we show a preview thumbnail, the asset type, and a button to open the full editor.
        void DrawLimitedAssetPreview()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(14f);
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Space(4f);

            float previewSize = Mathf.Min(120f, position.width - 32f);
            Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.15f));

            var preview = AssetPreview.GetAssetPreview(targetObject)
                          ?? AssetPreview.GetMiniThumbnail(targetObject);
            if (preview != null)
                GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
            else if (Event.current.type == EventType.Repaint)
                Repaint(); // preview loads asynchronously — poll until ready

            EditorGUILayout.Space(8f);

            string typeName = targetObject != null ? targetObject.GetType().Name : string.Empty;
            EditorGUILayout.LabelField(typeName, EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.Space(6f);

            if (GUILayout.Button("Open Property Editor", GUILayout.ExpandWidth(true), GUILayout.Height(24)))
            {
                var toOpen = targetObject;
                EditorApplication.delayCall += () =>
                {
                    try { EditorUtility.OpenPropertyEditor(toOpen); } catch { }
                };
            }

            GUILayout.Space(4f);
            EditorGUILayout.EndVertical();
            GUILayout.Space(14f);
            EditorGUILayout.EndHorizontal();
        }

        static string TrimParenthesizedSuffix(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Scene Component";

            int idx = value.LastIndexOf(" (", StringComparison.Ordinal);
            if (idx > 0 && value.EndsWith(")"))
                return value.Substring(0, idx);

            return value;
        }

        void DrawSceneObjectContent()
        {
            if (resolvedGameObject == null || sceneObjectComponents == null)
            {
                EditorGUILayout.HelpBox(isPrefabAsset ? "Prefab not available." : "Scene object not available.", MessageType.Info);
                return;
            }

            // Toolbar: icon + (SetActive toggle for scene objects only) + name + filter/layout buttons
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Show Prefab icon for prefab assets, GameObject icon for scene objects
            GUIContent gameObjectIcon = isPrefabAsset
                ? EditorGUIUtility.IconContent("Prefab Icon")
                : EditorGUIUtility.IconContent("GameObject Icon");
            if (gameObjectIcon != null && gameObjectIcon.image != null)
            {
                GUILayout.Label(gameObjectIcon, GUILayout.Width(18), GUILayout.Height(18));
            }

            // SetActive toggle only makes sense for scene GameObjects, not prefab assets
            if (!isPrefabAsset)
            {
                bool wasActive = resolvedGameObject.activeSelf;
                bool isActive = EditorGUILayout.Toggle(wasActive, GUILayout.Width(16));
                if (isActive != wasActive)
                {
                    Undo.RecordObject(resolvedGameObject, "Toggle GameObject Active");
                    resolvedGameObject.SetActive(isActive);
                }
            }

            GUILayout.Space(4);
            GUILayout.Label(resolvedGameObject.name, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();

            var tb = QuickPeekSharedUI.DrawLayoutToolbar((int)layoutMode, true, componentFoldoutStates);
            if (tb.LayoutChanged)
            {
                layoutMode = (LayoutMode)tb.NewLayout;
                contentScrollPos = Vector2.zero;
                sceneObjectScrollPos = Vector2.zero;
                selectedTab = Mathf.Clamp(selectedTab, 0, Math.Max(0, componentEditors.Count - 1));

                try
                {
                    if (!string.IsNullOrEmpty(sceneObjectLayoutPrefsKey))
                        EditorPrefs.SetInt(sceneObjectLayoutPrefsKey, (int)layoutMode);
                }
                catch { }

                if (layoutMode == LayoutMode.Foldouts)
                {
                    bool compHistValid = IsFoldoutHistoryValid(sceneObjectFoldoutPrefsKey);
                    bool exp = true;
                    if (compHistValid && !string.IsNullOrEmpty(sceneObjectFoldoutPrefsKey))
                    {
                        try
                        {
                            var saved = LoadComponentFoldoutPrefs(sceneObjectFoldoutPrefsKey);
                            for (int i = 0; i < sceneObjectComponents.Count && i < componentFoldoutStates.Count; i++)
                            {
                                var comp = sceneObjectComponents[i];
                                if (comp == null) continue;
                                string tn = comp.GetType().Name;
                                if (saved.ContainsKey(tn)) componentFoldoutStates[i] = saved[tn];
                            }
                        }
                        catch { }
                    }
                    else if (componentFoldoutStates != null)
                        for (int i = 0; i < componentFoldoutStates.Count; i++)
                            componentFoldoutStates[i] = exp;
                }
                else
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(sceneObjectTabPrefsKey) && EditorPrefs.HasKey(sceneObjectTabPrefsKey))
                            selectedTab = Mathf.Clamp(EditorPrefs.GetInt(sceneObjectTabPrefsKey, 0), 0, Math.Max(0, componentEditors.Count - 1));
                    }
                    catch { }
                }
                Repaint();
            }

            if (tb.ExpandAllClicked) ExpandAllComponentFoldouts();
            if (tb.CollapseAllClicked) CollapseAllComponentFoldouts();
            if (tb.SettingsClicked)
                showComponentFilterDropdown = !showComponentFilterDropdown;

            EditorGUILayout.EndHorizontal();
            // Handle double-click on SceneObjectRef header (before drawing the filter dropdown)
            var sceneHeaderRect = GUILayoutUtility.GetLastRect();
            // Closes this window and disposes componentEditors — nothing below may run.
            if (HandleHeaderInteraction(sceneHeaderRect)) return;

            // Inline filter dropdown (drawn outside the scroll view so it always appears at the top)
            if (showComponentFilterDropdown)
                DrawComponentFilterDropdown();

            // If in Tabs mode, render components as tabs
            if (layoutMode == LayoutMode.Tabs)
            {
                var visibleComps = sceneObjectComponents.Where(c => c != null && !hiddenComponentTypeNames.Contains(c.GetType().Name)).ToList();
                var visibleEditors = new List<UnityEditor.Editor>();
                foreach (var c in visibleComps)
                {
                    int idx = sceneObjectComponents.IndexOf(c);
                    visibleEditors.Add(idx >= 0 && idx < componentEditors.Count ? componentEditors[idx] : null);
                }

                var tabContents2 = visibleComps.Select(c => {
                    var iconImg = EditorGUIUtility.ObjectContent(c, c.GetType())?.image;
                    return new GUIContent(c.GetType().Name, iconImg);
                }).ToArray();
                int newTab2 = QuickPeekSharedUI.DrawTabsBar(selectedTab, tabContents2, ref tabScrollPos);
                if (newTab2 != selectedTab)
                {
                    selectedTab = newTab2;
                    try { if (!string.IsNullOrEmpty(sceneObjectTabPrefsKey)) EditorPrefs.SetInt(sceneObjectTabPrefsKey, selectedTab); } catch { }
                }


                selectedTab = Mathf.Clamp(selectedTab, 0, Math.Max(0, visibleEditors.Count - 1));

                contentScrollPos = EditorGUILayout.BeginScrollView(contentScrollPos, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(14f);
                EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                GUILayout.Space(4f);
                if (selectedTab >= 0 && selectedTab < visibleEditors.Count && visibleEditors[selectedTab] != null)
                    visibleEditors[selectedTab].OnInspectorGUI();
                GUILayout.Space(4f);
                EditorGUILayout.EndVertical();
                GUILayout.Space(14f);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndScrollView();
                return;
            }

            // Component foldout list (vertical scroll only, no horizontal)
            sceneObjectScrollPos = EditorGUILayout.BeginScrollView(
                sceneObjectScrollPos, false, false,
                GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);

            const float pad = 14f;

            for (int i = 0; i < sceneObjectComponents.Count; i++)
            {
                var comp = sceneObjectComponents[i];
                if (comp == null) continue;

                string typeName = comp.GetType().Name;
                if (hiddenComponentTypeNames.Contains(typeName))
                    continue;

                // All three lists are parallel (same length, same indices); guard defensively.
                if (i >= componentFoldoutStates.Count || i >= componentEditors.Count) break;

                // Foldout header with icon, enable toggle, and minus button
                // Order: Foldout → Icon → Enable Toggle → Label → Minus Button
                float headerHeight = EditorGUIUtility.singleLineHeight + 6f;
                Rect headerRect = GUILayoutUtility.GetRect(0, headerHeight, GUILayout.ExpandWidth(true));
                Color headerBg = EditorGUIUtility.isProSkin
                    ? new Color(0.16f, 0.16f, 0.16f)
                    : new Color(0.83f, 0.83f, 0.83f);
                EditorGUI.DrawRect(headerRect, headerBg);

                float xOffset = headerRect.x + 4;
                float yCenter = headerRect.y + headerRect.height * 0.5f;
                float lineHeight = EditorGUIUtility.singleLineHeight;

                // 1. Foldout (reserve space even if we draw it after icon)
                float foldoutWidth = 12f;
                Rect foldoutRect = new Rect(xOffset, yCenter - lineHeight * 0.5f, foldoutWidth, lineHeight);
                xOffset += foldoutWidth + 2;

                // 2. Component Icon (built-in Unity icon lookup)
                Texture2D componentIcon = null;
                try
                {
                    var iconContent = EditorGUIUtility.ObjectContent(comp, comp.GetType());
                    if (iconContent != null)
                        componentIcon = iconContent.image as Texture2D;
                }
                catch { }
                
                if (componentIcon != null)
                {
                    Rect iconRect = new Rect(xOffset, yCenter - 8, 16, 16);
                    GUI.DrawTexture(iconRect, componentIcon);
                    xOffset += 18;
                }

                // 3. Enable toggle for components that support it (Behaviour-derived)
                bool supportsEnabled = comp is Behaviour;
                if (supportsEnabled)
                {
                    Rect enableRect = new Rect(xOffset, yCenter - lineHeight * 0.5f, 16, lineHeight);
                    bool wasEnabled = (comp as Behaviour).enabled;
                    bool isEnabled = EditorGUI.Toggle(enableRect, wasEnabled);
                    if (isEnabled != wasEnabled)
                    {
                        Undo.RecordObject(comp, "Toggle Component Enabled");
                        (comp as Behaviour).enabled = isEnabled;
                        EditorUtility.SetDirty(comp);
                    }
                    xOffset += 18;
                }

                // 4. Component Label with Foldout
                float labelWidth = headerRect.width - (xOffset - headerRect.x) - 30; // Reserve space for minus button
                Rect labelRect = new Rect(xOffset, yCenter - lineHeight * 0.5f, labelWidth, lineHeight);
                
                // Draw foldout at the reserved position (no label text in the foldout itself)
                bool newFoldout = EditorGUI.Foldout(foldoutRect, componentFoldoutStates[i], GUIContent.none, true);

                // Draw label separately with bold style (not foldout style)
                if (s_CompLabelStyle == null)
                    s_CompLabelStyle = new GUIStyle(EditorStyles.boldLabel);
                GUI.Label(labelRect, typeName, s_CompLabelStyle);

                if (newFoldout != componentFoldoutStates[i])
                {
                    componentFoldoutStates[i] = newFoldout;
                    SaveComponentFoldoutPrefs();
                }

                if (s_HideIcon == null)
                    s_HideIcon = IconUtils.Load("hide");

                // 5. Hide button (hide/filter component)
                // Draw as a bold, background-less minus symbol for a cleaner header look.
                Rect minusRect = new Rect(headerRect.xMax - 24, yCenter - 8, 12, 12);
                if (s_CompMinusStyle == null)
                    s_CompMinusStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        padding = new RectOffset(0, 0, 0, 0)
                    };
                // Use a button so it's clickable, but render with the label style (no background).
                if (GUI.Button(minusRect, new GUIContent(s_HideIcon, "Hide this component"), s_CompMinusStyle))
                {
                    hiddenComponentTypeNames.Add(typeName);
                    SaveComponentFilterPrefs();
                    Repaint();
                }

                // ── Foldout expand / collapse animation ──
                float faded = 1f;
                if (foldoutAnimState != null)
                {
                    var animBool = foldoutAnimState.GetOrCreate(comp, componentFoldoutStates[i]);
                    animBool.target = componentFoldoutStates[i];
                    faded = animBool.faded;
                }

                // Skip content if fully collapsed and not mid-animation
                if (faded <= 0f)
                    continue;

                bool shouldShowContent = componentFoldoutStates[i] && i < componentEditors.Count && componentEditors[i] != null;
                
                // Begin animated fade group when animation is enabled
                bool groupVisible = foldoutAnimState != null
                    ? EditorGUILayout.BeginFadeGroup(faded)
                    : shouldShowContent;

                if (groupVisible && shouldShowContent)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(pad);
                    EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                    GUILayout.Space(4f);
                    componentEditors[i].OnInspectorGUI();
                    GUILayout.Space(4f);
                    EditorGUILayout.EndVertical();
                    GUILayout.Space(pad);
                    EditorGUILayout.EndHorizontal();
                }
                
                // End animated fade group when animation is enabled
                if (foldoutAnimState != null)
                    EditorGUILayout.EndFadeGroup();
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawComponentFilterDropdown()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Show All / Clear All buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Show All", EditorStyles.miniButton))
            {
                hiddenComponentTypeNames.Clear();
                SaveComponentFilterPrefs();
            }
            if (GUILayout.Button("Clear All", EditorStyles.miniButton))
            {
                if (sceneObjectComponents != null)
                {
                    foreach (var comp in sceneObjectComponents)
                        if (comp != null)
                            hiddenComponentTypeNames.Add(comp.GetType().Name);
                }
                SaveComponentFilterPrefs();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2f);

            // Per-component visibility toggles
            if (sceneObjectComponents != null)
            {
                foreach (var comp in sceneObjectComponents)
                {
                    if (comp == null) continue;
                    string typeName = comp.GetType().Name;
                    bool visible = !hiddenComponentTypeNames.Contains(typeName);
                    bool newVisible = EditorGUILayout.ToggleLeft(typeName, visible);
                    if (newVisible != visible)
                    {
                        if (newVisible)
                            hiddenComponentTypeNames.Remove(typeName);
                        else
                            hiddenComponentTypeNames.Add(typeName);
                        SaveComponentFilterPrefs();
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        string GetComponentFilterPrefsKey(SceneObjectRef sceneRef)
        {
            if (sceneRef == null) return null;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sceneRef, out string guid, out long local))
                return !string.IsNullOrEmpty(guid) ? $"QuickPeek_CompFilter_{guid}_{local}" : null;
            var path = AssetDatabase.GetAssetPath(sceneRef);
            if (!string.IsNullOrEmpty(path))
            {
                var pathGuid = AssetDatabase.AssetPathToGUID(path);
                return !string.IsNullOrEmpty(pathGuid) ? $"QuickPeek_CompFilter_{pathGuid}" : null;
            }
            return null;
        }

        string GetComponentFoldoutPrefsKey(SceneObjectRef sceneRef)
        {
            if (sceneRef == null) return null;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sceneRef, out string guid, out long local))
                return !string.IsNullOrEmpty(guid) ? $"QuickPeek_CompFoldout_{guid}_{local}" : null;
            var path = AssetDatabase.GetAssetPath(sceneRef);
            if (!string.IsNullOrEmpty(path))
            {
                var pathGuid = AssetDatabase.AssetPathToGUID(path);
                return !string.IsNullOrEmpty(pathGuid) ? $"QuickPeek_CompFoldout_{pathGuid}" : null;
            }
            return null;
        }

        string GetComponentTabPrefsKey(SceneObjectRef sceneRef)
        {
            if (sceneRef == null) return null;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sceneRef, out string guid, out long local))
                return !string.IsNullOrEmpty(guid) ? $"QuickPeek_CompTabs_{guid}_{local}" : null;
            var path = AssetDatabase.GetAssetPath(sceneRef);
            if (!string.IsNullOrEmpty(path))
            {
                var pathGuid = AssetDatabase.AssetPathToGUID(path);
                return !string.IsNullOrEmpty(pathGuid) ? $"QuickPeek_CompTabs_{pathGuid}" : null;
            }
            return null;
        }

        string GetComponentLayoutPrefsKey(SceneObjectRef sceneRef)
        {
            if (sceneRef == null) return null;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sceneRef, out string guid, out long local))
                return !string.IsNullOrEmpty(guid) ? $"QuickPeek_CompLayout_{guid}_{local}" : null;
            var path = AssetDatabase.GetAssetPath(sceneRef);
            if (!string.IsNullOrEmpty(path))
            {
                var pathGuid = AssetDatabase.AssetPathToGUID(path);
                return !string.IsNullOrEmpty(pathGuid) ? $"QuickPeek_CompLayout_{pathGuid}" : null;
            }
            return null;
        }

        HashSet<string> LoadComponentFilterPrefs(string prefsKey)
        {
            var result = new HashSet<string>();
            if (string.IsNullOrEmpty(prefsKey) || !EditorPrefs.HasKey(prefsKey))
                return result;
            var raw = EditorPrefs.GetString(prefsKey, "");
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (var t in raw.Split('|'))
                    if (!string.IsNullOrEmpty(t))
                        result.Add(t);
            }
            return result;
        }

        Dictionary<string, bool> LoadComponentFoldoutPrefs(string prefsKey)
        {
            var result = new Dictionary<string, bool>();
            if (string.IsNullOrEmpty(prefsKey) || !EditorPrefs.HasKey(prefsKey))
                return result;
            var raw = EditorPrefs.GetString(prefsKey, "");
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (var entry in raw.Split('|'))
                {
                    if (string.IsNullOrEmpty(entry)) continue;
                    var parts = entry.Split(':');
                    if (parts.Length == 2 && bool.TryParse(parts[1], out bool state))
                        result[parts[0]] = state;
                }
            }
            return result;
        }

        void SaveComponentFilterPrefs()
        {
            if (string.IsNullOrEmpty(sceneObjectPrefsKey)) return;
            EditorPrefs.SetString(sceneObjectPrefsKey, string.Join("|", hiddenComponentTypeNames));
        }

        void SaveComponentFoldoutPrefs()
        {
            if (string.IsNullOrEmpty(sceneObjectFoldoutPrefsKey)) return;
            if (sceneObjectComponents == null || componentFoldoutStates == null) return;

            var entries = new List<string>();
            for (int i = 0; i < sceneObjectComponents.Count && i < componentFoldoutStates.Count; i++)
            {
                var comp = sceneObjectComponents[i];
                if (comp == null) continue;
                string typeName = comp.GetType().Name;
                entries.Add($"{typeName}:{componentFoldoutStates[i]}");
            }
            EditorPrefs.SetString(sceneObjectFoldoutPrefsKey, string.Join("|", entries));
            MarkFoldoutHistoryCurrentSession(sceneObjectFoldoutPrefsKey);
            MarkFoldoutHistoryCurrentSession(sceneObjectLayoutPrefsKey);
        }

        /// <summary>
        /// Handles header interactions: double-click or drag converts the peek to a full editor
        /// window.
        ///
        /// Returns true when this window was closed as a result. Both gestures call
        /// <see cref="CloseInstance"/>, which runs <see cref="ClearEditors"/> synchronously and
        /// nulls out the editors the caller is in the middle of drawing with — so a caller that
        /// keeps going dereferences a null editor (the drag-out NRE in DrawSingleObjectContent).
        /// Callers must return immediately when this returns true. Each call site sits directly
        /// after an EndHorizontal, so bailing there leaves the layout groups balanced.
        /// </summary>
        bool HandleHeaderInteraction(Rect headerRect)
        {
            // Detached windows are moved via OS window chrome; don't re-trigger detach logic.
            if (isDetached) return false;

            var e = Event.current;
            if (e == null) return false;

            // Show a pan cursor over the header to indicate it is draggable.
            if (e.type == EventType.Repaint)
                EditorGUIUtility.AddCursorRect(headerRect, MouseCursor.Pan);

            if (e.type == EventType.MouseDown && headerRect.Contains(e.mousePosition))
            {
                if (e.clickCount == 2)
                {
                    headerDragPending = false;
                    try { CloseInstance(); } catch { }
                    OpenSameAsBrowserShortcut();
                    e.Use();
                    return true;
                }
                // Begin tracking a potential drag on first click.
                headerDragPending = true;
                headerDragStartPos = e.mousePosition;
            }

            if (e.type == EventType.MouseUp)
                headerDragPending = false;

            // Once the drag threshold is exceeded, treat it as a detach gesture.
            if (headerDragPending && e.type == EventType.MouseDrag)
            {
                if (Vector2.Distance(e.mousePosition, headerDragStartPos) > HEADER_DRAG_THRESHOLD)
                {
                    headerDragPending = false;

                    // Capture screen-space data while still inside OnGUI — GUIToScreenPoint is only valid here.
                    var mouseScreenPos = GUIUtility.GUIToScreenPoint(e.mousePosition);
                    var peekRect      = position;
                    var detachOffset  = mouseScreenPos - new Vector2(peekRect.x, peekRect.y);
                    var capturedTarget = targetObject;

                    try { CloseInstance(); } catch { }

                    // Container nodes: spawn ContainerChildrenInspector (full container UI).
                    // Leaves and Base: spawn a detached QuickPeekWindow that follows the mouse.
                    if (capturedTarget is IStructure && !(capturedTarget is Base))
                        ContainerChildrenInspector.OpenWithDrag((IStructure)capturedTarget, peekRect, detachOffset);
                    else
                        OpenDetached(capturedTarget, peekRect, detachOffset);

                    e.Use();
                    return true;
                }
            }

            return false;
        }

        void OpenFullEditorForObject()
        {
            // Deprecated: use OpenSameAsBrowserShortcut instead.
            OpenSameAsBrowserShortcut();
        }

        void OpenFullEditorForContainer()
        {
            // Deprecated: use OpenSameAsBrowserShortcut instead.
            OpenSameAsBrowserShortcut();
        }

        void OpenSameAsBrowserShortcut()
        {
            try
            {
                var obj = targetObject;

                if (obj == null)
                    return;

                // Special-case Base: treat as leaf and open property editor
                if (obj is Base)
                {
                    var toOpen = obj as Object;
                    EditorApplication.delayCall += () =>
                    {
                        try { EditorUtility.OpenPropertyEditor(toOpen); } catch (Exception ex) { Debug.LogWarning($"Failed to open property editor: {ex.Message}"); }
                        TryRepaintOriginBrowser();
                    };
                    return;
                }

                if (obj is IStructure container)
                {
                    // CloseInstance already called by handler; directly open the children editor.
                    try { ContainerChildrenInspector.Open(container); } catch (Exception ex) { Debug.LogWarning($"Failed to open container children window: {ex.Message}"); }
                    TryRepaintOriginBrowser();
                    return;
                }

                // SceneObjectRef: handle loaded scene resolution
                if (obj is SceneObjectRef sceneRef)
                {
                    var sceneObj = sceneRef.sceneObject;
                    string sceneName = sceneObj?.LastKnownScene;
                    bool sceneLoaded = !string.IsNullOrEmpty(sceneName) && UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName).isLoaded;
                    if (sceneLoaded)
                    {
                        var go = resolvedGameObject ?? SceneObjectMap.Resolve(sceneObj);
                        var toOpen = (Object)(go != null ? (Object)go : (Object)sceneRef);
                        EditorApplication.delayCall += () =>
                        {
                            try { EditorUtility.OpenPropertyEditor(toOpen); } catch (Exception ex) { Debug.LogWarning($"Failed to open property editor: {ex.Message}"); }
                            TryRepaintOriginBrowser();
                        };
                        return;
                    }
                    else
                    {
                        var toOpen = (Object)sceneRef;
                        EditorApplication.delayCall += () =>
                        {
                            try { EditorUtility.OpenPropertyEditor(toOpen); } catch (Exception ex) { Debug.LogWarning($"Failed to open property editor: {ex.Message}"); }
                            TryRepaintOriginBrowser();
                        };
                        return;
                    }
                }

                if (obj is SceneComponentRef sceneComponentRef)
                {
                    var sceneComponent = sceneComponentRef.sceneComponent;
                    string sceneName = sceneComponent?.LastKnownScene;
                    bool sceneLoaded = !string.IsNullOrEmpty(sceneName) && UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName).isLoaded;
                    if (sceneLoaded)
                    {
                        var component = SceneObjectMap.Resolve(sceneComponent);
                        var toOpen = (Object)(component != null ? (Object)component : (Object)sceneComponentRef);
                        EditorApplication.delayCall += () =>
                        {
                            try { EditorUtility.OpenPropertyEditor(toOpen); } catch (Exception ex) { Debug.LogWarning($"Failed to open property editor: {ex.Message}"); }
                            TryRepaintOriginBrowser();
                        };
                        return;
                    }

                    var unresolvedToOpen = (Object)sceneComponentRef;
                    EditorApplication.delayCall += () =>
                    {
                        try { EditorUtility.OpenPropertyEditor(unresolvedToOpen); } catch (Exception ex) { Debug.LogWarning($"Failed to open property editor: {ex.Message}"); }
                        TryRepaintOriginBrowser();
                    };
                    return;
                }

                // Default: open property editor for the object
                var defaultToOpen = obj as Object;
                EditorApplication.delayCall += () =>
                {
                    try { EditorUtility.OpenPropertyEditor(defaultToOpen); } catch (Exception ex) { Debug.LogWarning($"Failed to open property editor: {ex.Message}"); }
                    TryRepaintOriginBrowser();
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"OpenSameAsBrowserShortcut failed: {ex.Message}");
            }
        }

        void TryRepaintOriginBrowser()
        {
            try
            {
                // Try to find BrowserWindow instances and repaint those whose screen rect matches sourceBrowserScreenRect center.
                var browsers = BrowserWindowRegistry.AllOfType<BrowserWindow>();
                if (browsers.Count == 0)
                {
                    return;
                }

                Vector2 center = new Vector2(sourceBrowserScreenRect.x + sourceBrowserScreenRect.width * 0.5f, sourceBrowserScreenRect.y + sourceBrowserScreenRect.height * 0.5f);
                foreach (var b in browsers)
                {
                    try
                    {
                        // EditorWindow.position is in screen coords; check if center is inside
                        var pos = b.position;
                        if (pos.Contains(center))
                        {
                            b.Repaint();
                            return;
                        }
                    }
                    catch { }
                }

                // Fallback: repaint all browser windows
                foreach (var b in browsers)
                {
                    try { b.Repaint(); } catch { }
                }
            }
            catch { }
        }
    }
}
