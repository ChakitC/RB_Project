using System.Collections.Generic;
using SecretZauce.SecondBrain.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    /// <summary>
    /// Payload stored in <see cref="UnityEditor.DragAndDrop"/> generic data while a
    /// SecondBrain internal item is being dragged out of a BrowserWindow.
    /// </summary>
    public class SecondBrainDragData
    {
        /// <summary>The BrowserWindow from which the drag originated.</summary>
        public BrowserWindow SourceWindow;

        /// <summary>
        /// The original SecondBrain ScriptableObject items (Container, SceneObjectRef,
        /// ActionItem, etc.) — NOT the resolved GOs placed in objectReferences.
        /// </summary>
        public List<Object> OriginalItems;

        /// <summary>Cached array form of OriginalItems used by monitoring (avoids per-frame alloc).</summary>
        public Object[] OriginalItemsArray;

        /// <summary>Tree-view paths of the items in the source window's collection.</summary>
        public List<int[]> SourcePaths;

        /// <summary>
        /// Resolved objects suitable for Scene View: SceneObjectRef → live GO, assets → themselves.
        /// Set once at drag start and used when the mouse is over the Scene View.
        /// </summary>
        public Object[] SceneViewObjects;

        /// <summary>
        /// Set to true by the receiving BrowserWindow after a successful cross-window
        /// transfer so the source window's DragExited handler does not try to re-process.
        /// </summary>
        public bool WasHandledByBrowserWindow;

        /// <summary>
        /// Set to true the first time the drag moves to a window other than SourceWindow.
        /// Distinguishes a drag that never left the source (still an internal reparent) from
        /// one that genuinely crossed to another window and then returned.
        /// </summary>
        public bool HasLeftSourceWindow;
    }
}
