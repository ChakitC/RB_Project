using System.Collections.Generic;
using SecretZauce.SecondBrain.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    public class ProFeatureProvider : IProFeatureProvider
    {
        public SceneLinkGUIBase CreateSceneLinkGUI(UnityEditor.Editor editor)
        {
            return new SceneLinkGUI(editor);
        }

        public ActionItemGUIBase CreateActionItemGUI(object node, GUIStyle style, Rect arrowRect, Rect rowRect)
        {
            return new ActionItemGUI(node as ActionItem, style, arrowRect, rowRect);
        }

        public ActionItemHandlerBase CreateActionItemHandler()
        {
            return new ActionItemHandler();
        }

        public QuickPeekHandlerBase CreateQuickPeekHandler(BrowserWindow window)
        {
            return new QuickPeekHandler(window);
        }

        // ── Drag-out ─────────────────────────────────────────────────────────────────

        public void BeginExternalDrag(BrowserWindow source, List<Object> items, List<int[]> paths)
            => DragOutController.BeginExternalDrag(source, items, paths);

        public void HandleDragExited(BrowserWindow source)
            => DragOutController.HandleDragExited(source);

        public bool IsCrossWindowDragFromAnotherWindow(BrowserWindow thisWindow)
        {
            var data = DragOutController.GetActiveDragData();
            return data != null && data.SourceWindow != thisWindow;
        }

        public bool IsCrossWindowDragFromThisWindow(BrowserWindow thisWindow)
        {
            var data = DragOutController.GetActiveDragData();
            return data != null && data.SourceWindow == thisWindow;
        }

        public bool HasActiveDragOutFrom(BrowserWindow window)
            => DragOutController.HasActiveDragOutFrom(window);

        public void ApplyDragPayloadForCurrentTarget()
            => DragOutController.ApplyPayloadForCurrentTarget();

        public void CancelActiveDragOut()
            => DragOutController.CancelActiveDragOut();

        public bool HasDragLeftSourceWindow(BrowserWindow thisWindow)
        {
            var data = DragOutController.GetActiveDragData();
            return data != null && data.SourceWindow == thisWindow && data.HasLeftSourceWindow;
        }

        public bool ConsumeStartupDragExited(BrowserWindow thisWindow)
            => DragOutController.ConsumeStartupDragExited(thisWindow);

        public bool ExecuteCrossWindowTransfer(
            BrowserWindow dest,
            int[] dropTargetPath,
            int dropPosition,
            TreeView treeView,
            SelectionStateSO selection,
            List<IStructure> collections)
        {
            var data = DragOutController.GetActiveDragData();
            if (data == null || data.SourceWindow == dest) return false;

            // Idempotency guard. A single drop can reach BrowserWindow through two branches:
            // the DragPerform path (row target) and the "dropped on empty space" path. When the
            // destination Base has no containers there are no rows to hover, so the empty-space
            // branch runs, and on Windows the OS drag loop can also deliver DragPerform — firing
            // the transfer twice for one drop.
            //
            // The data survives both: GetActiveDragData falls back to DragAndDrop.GetGenericData,
            // which still holds this object after StopMonitoring has nulled s_ActiveDrag.
            //
            // The second run is what produced the reported corruption. Leaf items were migrated
            // again, duplicating them; and a Container already re-parented under destBase was
            // rejected by Base.CanAcceptChild's duplicate detection, so it fell into the leaf
            // branch and spawned an empty "Transferred Items" container.
            //
            // Returns true, not false: false would let the caller fall through to the normal
            // external-drop path (AddExternalItems / CreateContainerAndAddExternalItems), which
            // would duplicate the items by a different route. The drop is genuinely consumed.
            if (data.WasHandledByBrowserWindow) return true;

            DragAndDrop.AcceptDrag();
            DragOutController.ExecuteCrossWindowTransfer(
                data, dest, dropTargetPath,
                (DragAndDropManager.DropPosition)dropPosition,
                treeView, selection, collections);
            return true;
        }
    }
}
