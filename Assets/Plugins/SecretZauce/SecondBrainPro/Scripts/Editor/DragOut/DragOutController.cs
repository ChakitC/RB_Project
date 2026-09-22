using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SecretZauce.SecondBrain.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    /// <summary>
    /// Orchestrates drag-OUT from a BrowserWindow to external Unity targets:
    /// <list type="bullet">
    ///   <item>Another BrowserWindow — cross-window item move (all item types)</item>
    ///   <item>Scene View — prefab instance placement / scene-GO repositioning</item>
    ///   <item>Project Browser — custom dialogs (Create Prefab, Move/Variant/Original, Move/Copy)</item>
    /// </list>
    ///
    /// <para>
    /// THREADING/CONTEXT RULE: every read and write of <see cref="DragAndDrop"/> in this class must
    /// happen inside a live GUI context (an <c>OnGUI</c> call). Unity keeps drag state on the
    /// current <c>GUIState</c>; touching it from <c>EditorApplication.update</c>, <c>delayCall</c>
    /// or an <c>AssetPostprocessor</c> logs "the GUIStateObj is deleted, but is accessed" and
    /// mutates the drag payload owned by a destroyed GUIState. Unity then serializes that payload
    /// at <c>ApplyQueuedStartDrag</c> and crashes the editor when <c>FetchDataFromDrag</c> reads it
    /// back (SIGSEGV in StringStorage::assign, or a 1.8e19-byte vector reserve). All DragAndDrop
    /// writes therefore funnel through <see cref="ApplyPayloadForCurrentTarget"/>, which the source
    /// BrowserWindow calls from its OnGUI; <see cref="OnEditorUpdate"/> does bookkeeping only.
    /// </para>
    /// </summary>
    public static class DragOutController
    {
        const string DragDataKey = "SecondBrain_DragOut_v1";

        // Static drag state so EditorApplication.update can access it without GenericData
        static SecondBrainDragData s_ActiveDrag;
        static bool s_IsMonitoring;

        // Set by non-GUI callers (import guard, leak stop) to have the next OnGUI pass drop the
        // payload. Never write DragAndDrop from those contexts directly — see the class remarks.
        // The window is kept alongside the flag because the request outlives s_ActiveDrag: it is
        // the window whose OnGUI still has to run the clear.
        static bool s_ClearPayloadRequested;
        static BrowserWindow s_ClearPayloadWindow;

        // Leak stop: the OLE drag loop can end without delivering DragExited to the source window,
        // which used to leave monitoring running for the rest of the session.
        static double s_LastPointerDownTime;
        const double PointerUpGraceSeconds = 0.35;
        const double MaxDragSeconds = 120.0;

        // True between DragAndDrop.StartDrag() and the startup DragExited Unity fires
        // immediately after it. Consumed once via ConsumeStartupDragExited (Windows-only path).
        static bool s_StartupDragExitedPending;
        // When StartDrag() was called — used to distinguish the startup DragExited echo
        // (arrives within milliseconds) from a real session end that happens to be the
        // first DragExited seen (e.g. if the platform skips the startup echo).
        static double s_StartDragTime;

        // ── Initiation ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Transitions an active internal browser drag to Unity's DragAndDrop system.
        /// Call this when the mouse leaves the source BrowserWindow during a drag
        /// (typically from the <c>EventType.MouseLeaveWindow</c> handler in TreeView).
        /// </summary>
        public static void BeginExternalDrag(
            BrowserWindow sourceWindow,
            List<Object> originalItems,
            List<int[]> sourcePaths)
        {
            if (originalItems == null || originalItems.Count == 0) return;

            // Clear any stale state from a previous drag that ended without DragExited
            StopMonitoring();

            // Only items with a valid asset identity may reach objectReferences. An add/delete in
            // the browser destroys and rebuilds sub-assets, and the tree can still be holding the
            // pre-mutation wrappers; serializing one of those is what crashes the editor at the
            // first DragEnter. See DragPayloadUtils.
            var safeItems = DragPayloadUtils.FilterSafe(originalItems, "Drag out");
            if (safeItems.Length == 0) return;

            // Resolve items for Scene View (SceneObjectRef → live GO; others stay as-is)
            var sceneViewList = new List<Object>(safeItems.Length);
            foreach (var item in safeItems)
            {
                var resolved = ResolveForSceneView(item);
                if (DragPayloadUtils.IsDragSafe(resolved)) sceneViewList.Add(resolved);
            }

            var data = new SecondBrainDragData
            {
                SourceWindow      = sourceWindow,
                OriginalItems     = new List<Object>(safeItems),
                OriginalItemsArray= safeItems,
                SourcePaths       = sourcePaths,
                SceneViewObjects  = sceneViewList.Count > 0
                                    ? sceneViewList.ToArray()
                                    : Array.Empty<Object>(),
            };
            s_ActiveDrag = data;
            s_ClearPayloadRequested = false;
            s_ClearPayloadWindow = null;

            DragAndDrop.PrepareStartDrag();
            // Start with scene-view objects; monitoring will update per target window
            DragAndDrop.objectReferences = data.SceneViewObjects;
            DragAndDrop.paths = Array.Empty<string>();
            DragAndDrop.SetGenericData(DragDataKey, data);

            string title = safeItems.Length == 1
                ? safeItems[0]?.name ?? "Item"
                : $"{safeItems.Length} Items";
            DragAndDrop.StartDrag(title);
            s_StartupDragExitedPending = true;
            s_StartDragTime = EditorApplication.timeSinceStartup;
            s_LastPointerDownTime = s_StartDragTime;

            // Monitor mouse position each editor frame to adapt objectReferences per target
            StartMonitoring();
        }

        /// <summary>
        /// Returns true exactly once per drag-out session: for the startup DragExited that
        /// Unity fires the instant StartDrag() is called. Later DragExited events (real
        /// session end) return false so callers can clean up. Only consulted on Windows,
        /// where the drag-out handoff is deferred to the window edge.
        /// </summary>
        public static bool ConsumeStartupDragExited(BrowserWindow thisWindow)
        {
            if (s_ActiveDrag == null || s_ActiveDrag.SourceWindow != thisWindow) return false;
            if (!s_StartupDragExitedPending) return false;
            s_StartupDragExitedPending = false;
            // The startup echo arrives within the same GUI pump as StartDrag(). A first
            // DragExited that shows up later is a real session end and must not be eaten.
            return EditorApplication.timeSinceStartup - s_StartDragTime < 0.2;
        }

        // ── Per-frame monitoring ──────────────────────────────────────────────────────

        static void StartMonitoring()
        {
            if (s_IsMonitoring) return;
            s_IsMonitoring = true;
            EditorApplication.update += OnEditorUpdate;
        }

        internal static void StopMonitoring()
        {
            if (!s_IsMonitoring) return;
            s_IsMonitoring = false;
            try { EditorApplication.update -= OnEditorUpdate; } catch { }
            s_ActiveDrag = null;
            s_StartupDragExitedPending = false;
        }

        /// <summary>
        /// True while a drag-out started from <paramref name="window"/> is being monitored.
        /// Unlike <see cref="GetActiveDragData"/> this never falls back to
        /// <c>DragAndDrop.GetGenericData</c>, so it is safe to call from non-GUI contexts
        /// (EditorApplication.update, delayCall) — see the class remarks.
        /// </summary>
        public static bool HasActiveDragOutFrom(BrowserWindow window)
        {
            if (s_ActiveDrag != null && s_ActiveDrag.SourceWindow == window) return true;
            // Also true while a clear is still owed to this window's OnGUI — the drag is already
            // over, but the payload it left behind has not been dropped yet.
            return s_ClearPayloadRequested && s_ClearPayloadWindow == window;
        }

        /// <summary>
        /// Abandons the active drag-out payload. Safe to call from any context: the payload itself
        /// is dropped by the next <see cref="ApplyPayloadForCurrentTarget"/> pass, which runs inside
        /// the source window's OnGUI.
        /// </summary>
        public static void CancelActiveDragOut()
        {
            if (s_ActiveDrag == null) return;
            var source = s_ActiveDrag.SourceWindow;
            StopMonitoring();
            if (source == null) return;

            s_ClearPayloadRequested = true;
            s_ClearPayloadWindow = source;
            source.Repaint();
        }

        /// <summary>
        /// Per-frame bookkeeping for an active drag-out. Deliberately touches NO DragAndDrop state:
        /// this runs from <c>EditorApplication.update</c>, outside any GUI context. It tracks which
        /// window the cursor is over, detects a drag session that ended without delivering
        /// DragExited, and repaints the source window so
        /// <see cref="ApplyPayloadForCurrentTarget"/> gets a GUI context to publish the payload in.
        /// </summary>
        static void OnEditorUpdate()
        {
            if (s_ActiveDrag == null) { StopMonitoring(); return; }

            // LEAK GUARD (root cause of the transfer→undo→drag crash): once the cross-window
            // drop has been handled (ExecuteCrossWindowTransfer sets WasHandledByBrowserWindow at
            // its very first line), the drag-out's job is done. Stop republishing the payload NOW
            // instead of waiting for a DragExited that may never reach the source window — an OLE
            // drag can end without delivering it. The diagnostic log proved monitoring kept pushing
            // Profile sub-assets into objectReferences for seconds after the transfer, straddling
            // the subsequent undo + Profile reimport; one of those pushes handed a stale native
            // pointer to FetchDataFromDrag and crashed. HandleDragExited's own StopMonitoring becomes
            // an idempotent no-op after this.
            if (s_ActiveDrag.WasHandledByBrowserWindow)
            {
                StopMonitoring();
                return;
            }

            // CRASH GUARD: if any dragged sub-asset lost its asset identity — e.g. an undo between
            // StartDrag and ApplyQueuedStartDrag reimported the owning asset and destroyed every
            // sub-asset — serializing it would hand Unity a dangling PPtr, and the next
            // FetchDataFromDrag would read garbage out of it and take the editor down.
            // Abandon the session; the payload is dropped by the next GUI pass.
            if (DragPayloadUtils.AnyUnsafe(s_ActiveDrag.OriginalItemsArray) ||
                DragPayloadUtils.AnyUnsafe(s_ActiveDrag.SceneViewObjects))
            {
                CancelActiveDragOut();
                return;
            }

            // LEAK STOP: the OLE drag loop can end without delivering DragExited to the source
            // window (release over another application, over the toolbar, Esc mid-drag). Monitoring
            // then ran — and, before the payload move, republished the payload — for the rest of
            // the session. The mouse button state is the one signal available outside a GUI context.
            double now = EditorApplication.timeSinceStartup;
            if (IsPrimaryPointerDown())
                s_LastPointerDownTime = now;

            if (now - s_LastPointerDownTime > PointerUpGraceSeconds || now - s_StartDragTime > MaxDragSeconds)
            {
                CancelActiveDragOut();
                return;
            }

            var target = EditorWindow.mouseOverWindow;

            // Track the first time the drag moves to a window other than the source.
#if UNITY_EDITOR_WIN
            // Windows only: mouseOverWindow can momentarily report null while the OS drag loop
            // runs; treating null as "left" would make BrowserWindow reject in-window drops
            // (the re-entry guard keys off this flag), so only count real windows there.
            if (target != null && target != s_ActiveDrag.SourceWindow)
                s_ActiveDrag.HasLeftSourceWindow = true;
#else
            if (target != s_ActiveDrag.SourceWindow)
                s_ActiveDrag.HasLeftSourceWindow = true;
#endif

            // The payload itself is published from the source window's OnGUI — request the repaint
            // that gives us that GUI context.
            if (s_ActiveDrag.SourceWindow != null)
                s_ActiveDrag.SourceWindow.Repaint();
        }

        /// <summary>
        /// Publishes the drag payload for the window currently under the cursor.
        /// <para>
        /// MUST be called from inside an <c>OnGUI</c> pass (BrowserWindow does this while it is the
        /// drag source). Every DragAndDrop write in this class goes through here so no drag state is
        /// ever touched from <c>EditorApplication.update</c> — see the class remarks for what that
        /// used to cost.
        /// </para>
        /// </summary>
        public static void ApplyPayloadForCurrentTarget()
        {
            if (Event.current == null) return;

            if (s_ClearPayloadRequested)
            {
                s_ClearPayloadRequested = false;
                s_ClearPayloadWindow = null;
                DragAndDrop.objectReferences = Array.Empty<Object>();
                DragAndDrop.paths = Array.Empty<string>();
                DragAndDrop.SetGenericData(DragDataKey, null);
                return;
            }

            if (s_ActiveDrag == null) return;

            var target = EditorWindow.mouseOverWindow;

            Object[] desired;
            if (IsProjectBrowser(target))
            {
                // Clear refs so the Project Browser cannot natively move/copy assets.
                // Our DragExited handler will show the appropriate dialog instead.
                desired = Array.Empty<Object>();
            }
            else if (target is SceneView)
            {
                // Resolved GOs and prefab assets — enables native Scene View placement
                desired = s_ActiveDrag.SceneViewObjects;
            }
            else
            {
                // BrowserWindow or unknown: original items for cross-window hover detection
                desired = s_ActiveDrag.OriginalItemsArray;
            }

            if (!ObjectArraysEqual(DragAndDrop.objectReferences, desired))
            {
                DragAndDrop.objectReferences = desired;
                DragAndDrop.paths = Array.Empty<string>();
            }
        }

#if UNITY_EDITOR_WIN
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        const int VK_LBUTTON = 0x01;
        const int VK_RBUTTON = 0x02;

        // Either button counts: VK_LBUTTON is the physical left button, so a user with swapped
        // mouse buttons drags with VK_RBUTTON and would otherwise have the drag stopped from under
        // them. Being permissive here only delays the stop; it never lets a stale payload through,
        // because the identity guard above runs first.
        static bool IsPrimaryPointerDown()
            => ((GetAsyncKeyState(VK_LBUTTON) | GetAsyncKeyState(VK_RBUTTON)) & 0x8000) != 0;
#else
        // Only the timeout applies on platforms without a cheap non-GUI button query; the
        // DragExited path remains the primary session end there.
        static bool IsPrimaryPointerDown() => true;
#endif

        /// <summary>
        /// Synchronously aborts the active drag payload the instant a reimport destroys one of the
        /// dragged objects' native instances.
        ///
        /// OnEditorUpdate (above) only runs on the next EditorApplication.update tick, but the
        /// Windows OS drag loop (DoDragDrop) pumps its own message loop independent of that tick —
        /// it can call back into FetchDataFromDrag one or more times before Unity's update fires
        /// again. A reimport triggered by our own code (e.g. undo relocating a sub-asset) happens
        /// on the same thread, in the same call stack that led here, so abandoning the session from
        /// this callback closes that window instead of racing the next poll.
        ///
        /// The payload is not cleared here: an AssetPostprocessor has no GUI context, and writing
        /// DragAndDrop from one is the very corruption this class exists to avoid. CancelActiveDragOut
        /// flags the clear and repaints the source window, so it lands in the next OnGUI pass.
        /// </summary>
        class ActiveDragImportGuard : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(
                string[] importedAssets, string[] deletedAssets,
                string[] movedAssets, string[] movedFromAssetPaths)
            {
                if (s_ActiveDrag == null) return;
                if (!DragPayloadUtils.AnyUnsafe(s_ActiveDrag.OriginalItemsArray) &&
                    !DragPayloadUtils.AnyUnsafe(s_ActiveDrag.SceneViewObjects))
                    return;

                CancelActiveDragOut();
            }
        }

        static bool ObjectArraysEqual(Object[] a, Object[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        // ── Payload access ────────────────────────────────────────────────────────────

        /// <summary>Returns the active drag payload or null when no drag is in progress.</summary>
        public static SecondBrainDragData GetActiveDragData()
            => s_ActiveDrag ?? DragAndDrop.GetGenericData(DragDataKey) as SecondBrainDragData;

        // ── DragExited handling (source window) ───────────────────────────────────────

        /// <summary>
        /// Call this from the source BrowserWindow when <c>EventType.DragExited</c> fires.
        /// Detects the drop target and triggers the appropriate post-drop action:
        /// Project Browser → custom dialog; Scene View → Unity handled natively.
        /// </summary>
        public static void HandleDragExited(BrowserWindow sourceWindow)
        {
            var data = s_ActiveDrag;
            if (data == null || data.SourceWindow != sourceWindow) return;

            var targetWindow          = EditorWindow.mouseOverWindow;
            bool handledByCrossWindow = data.WasHandledByBrowserWindow;

            StopMonitoring(); // clears s_ActiveDrag

            if (handledByCrossWindow) return;

            if (IsProjectBrowser(targetWindow))
            {
                // Defer one frame so DragExited fully completes before showing dialogs
                EditorApplication.delayCall += () => HandleDropOnProjectBrowser(data);
            }
            // Scene View: Unity handles prefab instance creation / GO repositioning natively
        }

        // ── Project Browser drop handling ─────────────────────────────────────────────

        static void HandleDropOnProjectBrowser(SecondBrainDragData data)
        {
            foreach (var item in data.OriginalItems)
            {
                if (item == null) continue;

                if (item is SceneObjectRef sceneRef)
                {
                    HandlePB_SceneObjectRef(sceneRef);
                }
                else if (item is GameObject go && EditorUtility.IsPersistent(go))
                {
                    // Persistent GameObject == prefab asset
                    HandlePB_Prefab(go);
                }
                else if (EditorUtility.IsPersistent(item) && AssetDatabase.IsMainAsset(item)
                         && !(item is SceneObjectRef))
                {
                    // SceneAsset, TextAsset, AudioClip, Texture, custom SO, etc.
                    HandlePB_GenericAsset(item);
                }
                // Containers, ActionItems, embedded sub-assets: no Project Browser action
            }
        }

        /// <summary>SceneObjectRef → offer to create a prefab from the live GameObject.</summary>
        static void HandlePB_SceneObjectRef(SceneObjectRef sceneRef)
        {
            var go = SceneObjectMap.Resolve(sceneRef.sceneObject);
            if (go == null)
            {
                Debug.LogWarning(
                    $"[SecondBrain] Cannot create prefab: '{sceneRef.name}' scene object is not loaded.");
                return;
            }

            string folder = EditorUtility.OpenFolderPanel("Save Prefab", "Assets", "");
            if (string.IsNullOrEmpty(folder)) return;

            folder = ToProjectRelative(folder);
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/{SanitizeName(go.name)}.prefab");

            try
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.UserAction);
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecondBrain] Create prefab failed at '{prefabPath}': {ex.Message}");
            }
        }

        /// <summary>Prefab → Move / Create Variant / Create Original Prefab.</summary>
        static void HandlePB_Prefab(GameObject prefab)
        {
            string originalPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(originalPath)) return;

            // Step 1: Move or create?
            bool createNew = !EditorUtility.DisplayDialog(
                "Drop Prefab",
                $"What would you like to do with \"{prefab.name}\"?",
                "Move Prefab",
                "Create From Prefab…");

            string folder = EditorUtility.OpenFolderPanel(
                createNew ? "Choose Destination Folder" : "Move Prefab To",
                Path.GetDirectoryName(originalPath) ?? "Assets", "");
            if (string.IsNullOrEmpty(folder)) return;
            folder = ToProjectRelative(folder);

            if (!createNew)
            {
                // Move
                string newPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{folder}/{Path.GetFileName(originalPath)}");
                string err = AssetDatabase.MoveAsset(originalPath, newPath);
                if (!string.IsNullOrEmpty(err))
                    Debug.LogError($"[SecondBrain] Move prefab failed: {err}");
                return;
            }

            // Step 2: Variant or original (independent copy)?
            bool createVariant = EditorUtility.DisplayDialog(
                "Create From Prefab",
                $"Create from \"{prefab.name}\":",
                "Prefab Variant",
                "Original Prefab");

            if (createVariant)
            {
                string variantPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{folder}/{prefab.name} Variant.prefab");
                try
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
                    Object.DestroyImmediate(instance);
                    AssetDatabase.Refresh();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SecondBrain] Create variant failed: {ex.Message}");
                }
            }
            else
            {
                // Original — a new, independent copy of the prefab
                string copyPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{folder}/{Path.GetFileName(originalPath)}");
                if (!AssetDatabase.CopyAsset(originalPath, copyPath))
                    Debug.LogError($"[SecondBrain] Copy prefab failed for '{originalPath}'.");
                else
                    AssetDatabase.Refresh();
            }
        }

        /// <summary>Scene, TextAsset, and other project assets → Move or Copy dialog.</summary>
        static void HandlePB_GenericAsset(Object asset)
        {
            string originalPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(originalPath)) return;

            bool move = EditorUtility.DisplayDialog(
                "Drop Asset",
                $"Move or copy \"{asset.name}\" to another folder?",
                "Move",
                "Copy");

            // Moving the file behind a currently-loaded scene invalidates every live reference
            // Unity holds to that path (including its own SceneManager bookkeeping) while the
            // scene is still open — refuse rather than risk corrupting editor state. Copying is
            // unaffected since it never touches the original file, so only gate the Move path.
            if (move && asset is SceneAsset &&
                UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(originalPath).isLoaded)
            {
                EditorUtility.DisplayDialog("Cannot Move Scene",
                    $"\"{asset.name}\" is currently open. Close it before moving its file.", "OK");
                return;
            }

            string folder = EditorUtility.OpenFolderPanel(
                move ? "Move Asset To" : "Copy Asset To",
                Path.GetDirectoryName(originalPath) ?? "Assets", "");
            if (string.IsNullOrEmpty(folder)) return;
            folder = ToProjectRelative(folder);

            string newPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/{Path.GetFileName(originalPath)}");

            if (move)
            {
                string err = AssetDatabase.MoveAsset(originalPath, newPath);
                if (!string.IsNullOrEmpty(err))
                    Debug.LogError($"[SecondBrain] Move asset failed: {err}");
            }
            else
            {
                if (!AssetDatabase.CopyAsset(originalPath, newPath))
                    Debug.LogError($"[SecondBrain] Copy asset failed for '{originalPath}'.");
                else
                    AssetDatabase.Refresh();
            }
        }

        // ── Cross-window transfer (destination side) ──────────────────────────────────

        /// <summary>
        /// Executes a cross-window item move: migrates ScriptableObject sub-assets into
        /// the destination .asset file (<c>MoveItemsByPaths</c>), then optionally reparents
        /// items to the exact drop location within the destination tree.
        /// </summary>
        public static void ExecuteCrossWindowTransfer(
            SecondBrainDragData data,
            BrowserWindow destWindow,
            int[] dropTargetPath,
            DragAndDropManager.DropPosition dropPosition,
            TreeView destTreeView,
            SelectionStateSO destSelectionState,
            List<IStructure> destCollections)
        {
            var sourceWindow = data.SourceWindow;
            if (sourceWindow == null || destWindow == null) return;
            if (data.OriginalItems == null || data.OriginalItems.Count == 0) return;
            if (data.SourcePaths  == null || data.SourcePaths.Count == 0) return;

            // One drop must transfer once. ProFeatureResolver already screens for this, but the
            // guard is repeated here so any other caller inherits it — running twice duplicates
            // leaf items and leaves behind an empty "Transferred Items" container.
            if (data.WasHandledByBrowserWindow) return;

            var destBase = destWindow.Root as Base;
            if (destBase == null) return;

            var originalItems  = new List<Object>(data.OriginalItems);
            bool hasSpecificTarget = dropTargetPath != null && dropTargetPath.Length > 0
                                     && dropPosition != DragAndDropManager.DropPosition.None;

            // Mark as handled immediately so HandleDragExited skips Project-Browser dialogs.
            data.WasHandledByBrowserWindow = true;

            // Anchor the undo group BEFORE any migration so we can collapse migration +
            // reparent into a single Ctrl-Z later.
            Undo.IncrementCurrentGroup();
            int transferUndoGroup = Undo.GetCurrentGroup();

            // Split source paths: Container items can live at Base root; leaf items (SceneObjectRef,
            // ActionItem, etc.) must live inside a Container — Base.CanAcceptChild rejects them.
            var sourceCollections = sourceWindow.Controller.Collections;
            var destBaseAsStructure = destBase as IStructure;
            var containerPaths = new List<int[]>();
            var leafPaths      = new List<int[]>();
            for (int i = 0; i < data.SourcePaths.Count; i++)
            {
                var item = StructureUtils.GetNodeAtPath(data.SourcePaths[i], sourceCollections) as Object;
                if (item == null) continue;
                if (destBaseAsStructure.CanAcceptChild(item))
                    containerPaths.Add(data.SourcePaths[i]);
                else
                    leafPaths.Add(data.SourcePaths[i]);
            }

            // Step 1a: Migrate leaf items FIRST — before any container is removed from the source.
            // MoveItemsByPaths removes root-level containers from the source Base, shifting all
            // subsequent root indices. leafPaths were captured from the original (unmodified) source
            // tree, so MoveItemsToContainer must run before that shift occurs. Moving leaves first
            // is safe for containers: removing a sub-item from inside a container does not change
            // that container's own index in Root.Children.
            if (leafPaths.Count > 0)
            {
                var leafTarget = ResolveLeafTargetContainer(
                    dropTargetPath, dropPosition, destCollections, destBase);
                if (leafTarget != null)
                    sourceWindow.Controller.MoveItemsToContainer(leafPaths, leafTarget, sourceWindow.TreeView);
                else
                    Debug.LogWarning("[SecondBrain] Cross-window transfer: could not resolve a target Container for leaf items.");
            }

            // Step 1b: Migrate Container items → land at destBase root.
            if (containerPaths.Count > 0)
                sourceWindow.Controller.MoveItemsByPaths(containerPaths, destBase, sourceWindow.TreeView);

            // Step 2: Refresh both windows; reparent to the exact drop location if specified.
            EditorApplication.delayCall += () =>
            {
                try
                {
                    if (sourceWindow != null) sourceWindow.RefreshTree();
                    if (destWindow == null) return;
                    destWindow.RefreshTree();

                    if (hasSpecificTarget)
                    {
                        // Build fresh collections directly from destBase.ChildrenObjects — the dest
                        // controller's cached Collections are stale because MoveItemsByPaths ran on the
                        // source controller and never triggered destController.RefreshFromRoot().
                        // (destWindow.RefreshTree() also calls ForceRefreshFromRoot now, so this is
                        // belt-and-suspenders for cases where the controller root changed.)
                        var freshCollections = destBase.Children
                            .OfType<IStructure>()
                            .ToList();

                        if (freshCollections.Count > 0)
                        {
                            // Items are now at destBase root (containers) or inside a Container (leaves);
                            // find their new paths so ReparentItems can move them to the drop location.
                            // Resolve items FROM their found paths rather than using originalItems directly:
                            // ImportAsset (called by MoveItemsByPaths) can recreate in-memory wrappers,
                            // making ReferenceEquals comparisons stale for some items, so newPaths.Count
                            // may be less than originalItems.Count — passing both to ReparentItems would
                            // cause an ArgumentOutOfRangeException at the parallel-array sort (line 941).
                            var newPaths = FindItemPaths(originalItems, freshCollections);
                            if (newPaths.Count > 0)
                            {
                                var foundItems = ResolveItemsFromPaths(newPaths, freshCollections);
                                if (foundItems.Count != newPaths.Count) return;

                                // Filter to only items the drop-target parent can accept.
                                // TypedContainer<T> is IScriptableStructure<T>, not a Container subclass,
                                // so Base.CanAcceptChild returns false for it. It was placed correctly in
                                // the leafTarget Container by MoveItemsToContainer and should stay there;
                                // passing it to ReparentItems for a Base-level drop would trigger a
                                // "Type mismatch" notification and abort the position refinement.
                                // When the drop is Inside a Container, TypedContainers are included because
                                // Container.CanAcceptChild accepts anything that isn't a Base.
                                var reparentParent = DetermineReparentTargetParent(
                                    dropTargetPath, dropPosition, freshCollections, destBase);
                                var reparentPaths = new List<int[]>(newPaths.Count);
                                var reparentItems = new List<Object>(foundItems.Count);
                                for (int i = 0; i < foundItems.Count; i++)
                                {
                                    if (reparentParent == null || reparentParent.CanAcceptChild(foundItems[i]))
                                    {
                                        reparentPaths.Add(newPaths[i]);
                                        reparentItems.Add(foundItems[i]);
                                    }
                                }
                                if (reparentPaths.Count == 0) return;

                                destWindow.Controller.ReparentItems(
                                    reparentPaths, reparentItems, dropTargetPath, dropPosition,
                                    freshCollections, destWindow.TreeView, destSelectionState);

                                // Do NOT call RefreshTree here — it would rebuild the TreeView from
                                // disk-saved foldout state, wiping the expansion that ReparentItems
                                // just set via OnFoldoutStateRestoreRequested. A bare Repaint() is
                                // enough since ReparentItems → OnStructureChanged already rebuilt
                                // the TreeView and applied the correct foldout state.
                                destWindow.Repaint();
                            }
                        }
                    }
                }
                catch (Exception ex) { Debug.LogException(ex); }
                finally
                {
                    // Collapse migration(s) + reparent into one undo entry so Ctrl-Z reverts
                    // the whole cross-window transfer in a single step.
                    Undo.CollapseUndoOperations(transferUndoGroup);
                }
            };
        }

        /// <summary>
        /// Returns the Container into which leaf items should land after cross-window transfer.
        /// If the drop target is (or has a Container parent), that Container is returned.
        /// Otherwise a new Container is created at destBase root so the transfer always succeeds.
        /// </summary>
        static Container ResolveLeafTargetContainer(
            int[] dropTargetPath,
            DragAndDropManager.DropPosition dropPosition,
            List<IStructure> destCollections,
            Base destBase)
        {
            if (dropTargetPath != null && destCollections != null)
            {
                if (dropPosition == DragAndDropManager.DropPosition.Inside)
                {
                    if (StructureUtils.GetNodeAtPath(dropTargetPath, destCollections) is Container c)
                        return c;
                }
                else if (dropTargetPath.Length > 1)
                {
                    var parentPath = dropTargetPath.Take(dropTargetPath.Length - 1).ToArray();
                    if (StructureUtils.GetNodeAtPath(parentPath, destCollections) is Container c)
                        return c;
                }
            }

            // No Container at drop location — create a new one at destBase root.
            return CreateTransferContainer(destBase);
        }

        static Container CreateTransferContainer(Base destBase)
        {
            string destBasePath = AssetDatabase.GetAssetPath(destBase);
            if (string.IsNullOrEmpty(destBasePath)) return null;

            var container = ScriptableObject.CreateInstance<Container>();
            container.name = "Transferred Items";

            Undo.RegisterCreatedObjectUndo(container, "Cross-Window Transfer");
            AssetDatabase.AddObjectToAsset(container, destBasePath);
            Undo.RegisterCompleteObjectUndo(destBase, "Cross-Window Transfer");
            (destBase as IStructure).AddChild(container, -1);
            EditorUtility.SetDirty(destBase);
            AssetDatabase.SaveAssets();

            return container;
        }

        // ── Utilities ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves a SecondBrain item to the best Object for Scene View dragging.
        /// SceneObjectRef → resolved live GameObject (enables GO repositioning in scene).
        /// All others → the item itself (prefab assets enable native instance placement).
        /// </summary>
        static Object ResolveForSceneView(Object item)
        {
            if (item is SceneObjectRef sceneRef)
                return SceneObjectMap.Resolve(sceneRef.sceneObject);
            return item;
        }

        static bool IsProjectBrowser(EditorWindow w)
            => w != null &&
               (w.GetType().Name    == "ProjectBrowser" ||
                w.GetType().FullName == "UnityEditor.ProjectBrowser");

        static string ToProjectRelative(string path)
        {
            if (path.StartsWith(Application.dataPath))
                return "Assets" + path[Application.dataPath.Length..];
            return path;
        }

        static string SanitizeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        /// <summary>
        /// Mirrors the targetParent resolution logic inside ReparentItems so the caller can
        /// pre-filter items by type compatibility before handing off to that method.
        /// </summary>
        static IStructure DetermineReparentTargetParent(
            int[] dropTargetPath,
            DragAndDropManager.DropPosition dropPosition,
            List<IStructure> collections,
            Base destBase)
        {
            if (dropTargetPath == null || dropTargetPath.Length == 0)
                return destBase as IStructure;

            if (dropPosition == DragAndDropManager.DropPosition.Inside)
                return (StructureUtils.GetNodeAtPath(dropTargetPath, collections) as IStructure)
                       ?? destBase as IStructure;

            if (dropTargetPath.Length > 1)
            {
                var parentPath = dropTargetPath.Take(dropTargetPath.Length - 1).ToArray();
                return (StructureUtils.GetNodeAtPath(parentPath, collections) as IStructure)
                       ?? destBase as IStructure;
            }

            return destBase as IStructure;
        }

        /// <summary>
        /// Searches <paramref name="collections"/> for paths matching the given items
        /// by reference equality (the same Object instances, just re-embedded in dest .asset).
        /// </summary>
        static List<int[]> FindItemPaths(List<Object> items, List<IStructure> collections)
        {
            var results = new List<int[]>();
            for (int i = 0; i < collections.Count; i++)
            {
                var col = collections[i] as Object;
                if (col == null) continue;
                if (items.Any(x => ReferenceEquals(x, col)))
                {
                    results.Add(new[] { i });
                    continue;
                }
                SearchInStructure(items, collections[i], new[] { i }, results);
            }
            return results;
        }

        static void SearchInStructure(
            List<Object> items, IStructure structure,
            int[] parentPath, List<int[]> results)
        {
            if (structure?.ChildrenObjects == null) return;
            for (int i = 0; i < structure.ChildrenObjects.Count; i++)
            {
                var child = structure.ChildrenObjects[i];
                if (child == null) continue;
                int[] childPath = parentPath.Concat(new[] { i }).ToArray();
                if (items.Any(x => ReferenceEquals(x, child)))
                {
                    results.Add(childPath);
                    continue;
                }
                if (child is IStructure childStruct)
                    SearchInStructure(items, childStruct, childPath, results);
            }
        }

        /// <summary>
        /// Resolves the live Object at each path in <paramref name="collections"/>.
        /// Always produces a list of the same length as <paramref name="paths"/>, so the
        /// result can be passed as the parallel <c>items</c> argument to ReparentItems.
        /// </summary>
        static List<Object> ResolveItemsFromPaths(List<int[]> paths, List<IStructure> collections)
        {
            var result = new List<Object>(paths.Count);
            foreach (var path in paths)
            {
                var item = StructureUtils.GetNodeAtPath(path, collections);
                if (item != null) result.Add(item);
            }
            return result;
        }
    }
}
