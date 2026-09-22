using System;
using System.Collections.Generic;
using SecretZauce.SecondBrain.Editor;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    public class QuickPeekHandler : QuickPeekHandlerBase
    {
        // Tracks the path of the object currently shown in the QuickPeekWindow so
        // we only recreate the window when the hovered item actually changes.
        int[] peekWindowPath;

        // Timer for the grace period before closing the popup when the mouse leaves
        // both the hovered tree row and the peek window itself.
        double peekCloseStartTime = -1;
        const double PEEK_CLOSE_GRACE_SECONDS = 0.15;
        static BrowserWindow lastPeekOwner;
        
        // Tracks whether the left mouse button is currently held down so QuickPeek
        // is suppressed during mouse drags (e.g. resizing the window).
        bool isLeftMouseButtonDown;

        readonly BrowserWindow caller; 
        TreeView TreeView => caller.TreeView;
        Rect Position => caller.position; 
        List<IStructure> Collections => caller.Collections;

        public QuickPeekHandler(BrowserWindow caller) : base(caller)
        {
            this.caller = caller;
        }

        /// <summary>
        /// Checks whether the QuickPeekWindow should be closed because the mouse has left
        /// both the hovered tree row and the peek popup.  Uses a short grace period so the
        /// user can comfortably move the mouse from a tree row into the popup.
        /// </summary>
        public override void HandleQuickPeekCloseCheck()
        {
            if (lastPeekOwner != caller)
                return;

            if (QuickPeekWindow.Instance == null)
            {
                peekCloseStartTime = -1;
                return;
            }

            var mouseOver = EditorWindow.mouseOverWindow;
            bool overPeek = mouseOver == QuickPeekWindow.Instance;

            if (overPeek)
            {
                // Mouse returned to the peek — clear the external-interaction flag so the
                // normal close behaviour resumes once the user moves away again.
                QuickPeekWindow.ClearExternalInteraction();
                peekCloseStartTime = -1;
                return;
            }

            // When the mouse is over the BrowserWindow and hovering a peek zone, keep it open.
            // Hovering the center of a row (outside peek zones) starts the close countdown.
            bool overBrowserWithHover = mouseOver == caller && TreeView != null && TreeView.DragInput.QuickPeekHoveredPath != null;
            if (overBrowserWithHover)
            {
                // Mouse returned to a browser peek zone — clear the external-interaction flag.
                QuickPeekWindow.ClearExternalInteraction();
                peekCloseStartTime = -1;
                return;
            }

            // The user previously clicked inside the peek window (e.g. to open a color picker
            // or object selector dialog). Keep the window alive while that external dialog is
            // open; the flag is cleared when the mouse returns to the peek or a hover zone.
            if (QuickPeekWindow.HasPendingExternalInteraction)
            {
                peekCloseStartTime = -1;
                return;
            }

            // Mouse is over neither the peek popup nor a hovered row — start the grace countdown
            if (peekCloseStartTime < 0)
            {
                peekCloseStartTime = EditorApplication.timeSinceStartup;
            }
            else if (EditorApplication.timeSinceStartup - peekCloseStartTime >= PEEK_CLOSE_GRACE_SECONDS)
            {
                CloseQuickPeek();
            }
        }

        /// <summary>
        /// Called each OnGUI frame after the TreeView has been drawn.
        /// Shows or hides the QuickPeekWindow based on the current hover state.
        /// Only acts on Repaint/MouseMove events so layout events don't interfere.
        /// </summary>
        public override void UpdateQuickPeek()
        {
            var currentEventType = Event.current.type;

            // MouseDrag means a button is held — close immediately and bail out before
            // the Repaint/MouseMove filter so we catch drags that start outside this window.
            if (currentEventType == EventType.MouseDrag)
            {
                isLeftMouseButtonDown = true;
                CloseQuickPeek();
                return;
            }

            // Track left mouse button state from events that reach this window.
            if (currentEventType == EventType.MouseDown && Event.current.button == 0)
                isLeftMouseButtonDown = true;
            else if (currentEventType == EventType.MouseUp && Event.current.button == 0)
                isLeftMouseButtonDown = false;

            // If we know the button is currently held, suppress peek and bail out early.
            if (isLeftMouseButtonDown)
            {
                CloseQuickPeek();
                return;
            }

            // Only process hover changes on Repaint/MouseMove to avoid layout mismatches.
            // NOTE: QuickPeek is only *opened* during MouseMove events. MouseMove is sent
            // exclusively when no mouse button is held (Unity sends MouseDrag instead when
            // a button is held), so this naturally prevents QuickPeek from opening during
            // any mouse-drag — including OS-level window resizing where this window may
            // never receive a MouseDown event at all.
            if (currentEventType != EventType.Repaint && currentEventType != EventType.MouseMove)
            {
                return;
            }

            if (TreeView == null)
            {
                return;
            }

            // Respect the global QuickPeek setting
            try
            {
                if (!BrowserSettings.EnableQuickPeek)
                {
                    CloseQuickPeek();
                    return;
                }
            }
            catch
            {
                // If BrowserSettings is unavailable for some reason, fail-safe by allowing peek.
            }

            // Don't show peek while dragging, renaming, or in ghost-creation mode
            if (TreeView.DragDropManager.IsDragging ||
                TreeView.Renamer.IsRenamingAny ||
                TreeView.HasGhostSession)
            {
                CloseQuickPeek();
                return;
            }

            // Don't show QuickPeek while any floating picker tray (EmojiTray/ColorTray) is open.
            // PickerTrayBase tracks the single open tray via its static field; use the public accessor.
            try
            {
                if (PickerTrayBase.IsAnyTrayOpen)
                {
                    CloseQuickPeek();
                    return;
                }
            }
            catch
            {
                // In case PickerTrayBase is unavailable for any reason, fail safe by not blocking QuickPeek.
            }

            var hoveredPath = TreeView.DragInput.QuickPeekHoveredPath;

            if (hoveredPath == null)
            {
                // Mouse is not in a peek zone — close check with grace period handles closing.
                return;
            }

            // Mouse is over a row — determine whether QuickPeek is allowed for this item (and its ancestors)
            var obj = TreeView.GetObjectAtPath(hoveredPath);
            if (obj == null)
            {
                return;
            }

            if (IsBlockedFor(obj, hoveredPath))
            {
                CloseQuickPeek();
                return;
            }

            var screenRect = TreeView.DragInput.QuickPeekHoveredScreenRect;
            var peekSide = TreeView.DragInput.QuickPeekHoveredSide;

            // Open QuickPeek during MouseMove events.
            // MouseMove is only delivered when NO mouse button is held — Unity sends
            // MouseDrag instead during button-held movement. This prevents QuickPeek
            // from opening while dragging or resizing (even OS-level resize where this
            // window may never receive MouseDown).
            if (currentEventType == EventType.MouseMove)
            {
                QuickPeekWindow.Show(obj, screenRect, Position, peekSide);
                peekWindowPath = hoveredPath;
                lastPeekOwner = caller;
                peekCloseStartTime = -1; // Reset the close-grace timer
            }
            else if (currentEventType == EventType.Repaint && QuickPeekWindow.Instance != null)
            {
                // When the popup is already open, allow Repaint to update the preview target too.
                // Some hover paths (notably the left peek padding) can miss a MouseMove update for a
                // frame, and this keeps the visual response crisp without reopening the popup.
                if (peekWindowPath == null || !ArraysEqual(peekWindowPath, hoveredPath))
                {
                    QuickPeekWindow.Show(obj, screenRect, Position, peekSide);
                    peekWindowPath = hoveredPath;
                    lastPeekOwner = caller;
                    peekCloseStartTime = -1;
                }
            }
            else if (QuickPeekWindow.Instance != null &&
                     peekWindowPath != null && ArraysEqual(peekWindowPath, hoveredPath))
            {
                // During Repaint, only reposition an already-open peek for the same item
                // (e.g. when the browser window itself moves). Never open a new one.
                QuickPeekWindow.Show(obj, screenRect, Position, peekSide);
            }
        }

        /// <summary>
        /// True when the node at <paramref name="path"/> resolves but QuickPeek can never be
        /// drawn for it. Only permanent reasons count — an unresolved node, an unfocused
        /// window, or a pending show delay are not "blocked".
        /// </summary>
        public override bool IsBlockedForPath(int[] path)
        {
            if (path == null || TreeView == null)
                return false;

            var obj = TreeView.GetObjectAtPath(path);
            if (obj == null)
                return false;

            return IsBlockedFor(obj, path);
        }

        /// <summary>
        /// Permanent, type/configuration-based reasons QuickPeek cannot be shown for
        /// <paramref name="obj"/> at <paramref name="path"/>.
        /// </summary>
        bool IsBlockedFor(UnityEngine.Object obj, int[] path)
        {
            // Scene assets are treated as special rows and never get a peek.
            try
            {
                if (obj is SceneAsset)
                    return true;
            }
            catch
            {
                // Ignore type-check failures and proceed to other checks
            }

            // Folder references have nothing meaningful to preview.
            try
            {
                if (obj is DefaultAsset)
                {
                    string assetPath = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.IsValidFolder(assetPath))
                        return true;
                }
            }
            catch
            {
                // Ignore type-check/path-resolution failures and proceed to other checks
            }

            // Empty containers (no non-null children) have nothing to preview.
            try
            {
                if (obj is IStructure structObj)
                {
                    var children = structObj.ChildrenObjects;
                    bool hasNonNullChild = children != null && System.Linq.Enumerable.Any(children, c => c != null);
                    if (!hasNonNullChild)
                        return true;
                }
            }
            catch
            {
                // Ignore errors resolving children and fall back to allowing the peek
            }

            // If any ancestor container (including the hovered node itself) has DisableQuickPeek, block the peek.
            try
            {
                if (Collections != null && path is {Length: > 0})
                {
                    for (int depth = path.Length; depth >= 1; depth--)
                    {
                        int[] ancestorPath = new int[depth];
                        Array.Copy(path, ancestorPath, depth);
                        var ancestor = StructureUtils.GetNodeAtPath(ancestorPath, Collections);
                        if (ancestor is Container container && container.DisableQuickPeek)
                            return true;
                    }
                }
            }
            catch
            {
                // Ignore path-resolution errors and fall back to allowing the peek
            }

            return false;
        }

        /// <summary>Closes the QuickPeekWindow and resets tracking state.</summary>
        public override void CloseQuickPeek()
        {
            QuickPeekWindow.CloseInstance();
            peekWindowPath = null;
            peekCloseStartTime = -1;
            lastPeekOwner = null;
        }

        public override void OpenFor(int[] selectedPath, BrowserWindow window)
        {
           PropertyEditorHelper.OpenPropertyEditorFor(selectedPath, window); 
        }

        public override bool IsOpenForPath(int[] path)
        {
            if (path == null || peekWindowPath == null)
                return false;

            if (QuickPeekWindow.Instance == null)
                return false;

            if (lastPeekOwner != caller && lastPeekOwner != null)
                return false;

            return ArraysEqual(peekWindowPath, path);
        }

        /// <summary>Returns true if two int arrays have equal length and equal elements.</summary>
        static bool ArraysEqual(int[] a, int[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        public override void DisposeQuickPeek()
        {
            try { QuickPeekWindow.CloseInstance(); } catch { }
        }

        public override bool HasPendingShow => QuickPeekWindow.HasPendingShow;
    }
}
