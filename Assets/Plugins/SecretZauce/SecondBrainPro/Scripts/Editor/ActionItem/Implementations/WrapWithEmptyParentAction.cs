using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    public enum WrapPositionMode
    {
        /// <summary>Children keep their world positions; parent placed at the chosen pivot.</summary>
        KeepWorldPositions,
        /// <summary>All children moved to local (0,0,0) after parenting; parent placed at the chosen pivot.</summary>
        AllAtLocalZero,
    }

    public enum WrapPivot
    {
        BoundsCenter,
        BoundsBottomCenter,
        BoundsTopCenter,
        BoundsFrontCenter,
        BoundsBackCenter,
        AverageOrigins,
        WorldOrigin,
    }

    /// <summary>
    /// Wraps all selected scene GameObjects under a newly created empty parent.
    /// The parent is inserted at the correct hierarchy level (sibling of the selection).
    /// </summary>
    public class WrapWithEmptyParentAction : ActionItem
    {
        [Tooltip("Name of the new parent GameObject.")]
        public string parentName = "Group";

        [Tooltip("Whether children keep their world positions or are moved to local (0,0,0).")]
        public WrapPositionMode positionMode = WrapPositionMode.KeepWorldPositions;

        [Tooltip("Where the new parent is placed in world space.")]
        public WrapPivot pivot = WrapPivot.BoundsCenter;

        [Tooltip("Add a BoxCollider on the parent that tightly wraps all children's combined bounds.")]
        public bool generateBoxCollider;

        public override string ActionPath => "Examples";

        public override string GetDetailDisplay() => string.Empty;

        public override void Execute()
        {
            var selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                Debug.LogWarning("[WrapWithEmptyParentAction] No GameObjects selected.");
                return;
            }

            // Only top-level objects (exclude children already present in the selection)
            var roots = GetTopLevel(selected);
            if (roots.Count == 0) return;

            Undo.SetCurrentGroupName("Wrap With Empty Parent");
            int group = Undo.GetCurrentGroup();

            // Capture world bounds before any transform changes
            Bounds combined = ComputeCombinedBounds(roots);
            Vector3 parentPos = GetPivotPosition(combined, roots);

            // Find the common parent and earliest sibling index to keep hierarchy tidy
            Transform commonParent = GetCommonParent(roots);
            int insertSiblingIndex = GetEarliestSiblingIndex(roots, commonParent);

            // Create the new parent
            var parentGO = new GameObject(string.IsNullOrWhiteSpace(parentName) ? "Group" : parentName);
            Undo.RegisterCreatedObjectUndo(parentGO, "Create Parent");

            parentGO.transform.position = parentPos;
            parentGO.transform.rotation = Quaternion.identity;
            parentGO.transform.localScale = Vector3.one;

            Undo.SetTransformParent(parentGO.transform, commonParent, "Set Parent Hierarchy");
            parentGO.transform.SetSiblingIndex(insertSiblingIndex);

            foreach (var go in roots)
            {
                Undo.SetTransformParent(go.transform, parentGO.transform, "Reparent");

                if (positionMode == WrapPositionMode.AllAtLocalZero)
                    go.transform.localPosition = Vector3.zero;
            }

            if (generateBoxCollider)
                AttachBoxCollider(parentGO, combined);

            // Mark scene dirty if dealing with scene objects
            if (roots[0].scene.IsValid())
                EditorSceneManager.MarkSceneDirty(roots[0].scene);

            Selection.activeGameObject = parentGO;
            Undo.CollapseUndoOperations(group);
        }

        Vector3 GetPivotPosition(Bounds combined, List<GameObject> roots)
        {
            return pivot switch
            {
                WrapPivot.BoundsBottomCenter => new Vector3(combined.center.x, combined.min.y, combined.center.z),
                WrapPivot.BoundsTopCenter    => new Vector3(combined.center.x, combined.max.y, combined.center.z),
                WrapPivot.BoundsFrontCenter  => new Vector3(combined.center.x, combined.center.y, combined.min.z),
                WrapPivot.BoundsBackCenter   => new Vector3(combined.center.x, combined.center.y, combined.max.z),
                WrapPivot.AverageOrigins     => AveragePosition(roots),
                WrapPivot.WorldOrigin        => Vector3.zero,
                _                            => combined.center,
            };
        }

        static void AttachBoxCollider(GameObject parent, Bounds worldBounds)
        {
            var bc = Undo.AddComponent<BoxCollider>(parent);
            // Parent has identity transform so world space == local space
            bc.center = parent.transform.InverseTransformPoint(worldBounds.center);
            Vector3 localSize = parent.transform.InverseTransformVector(worldBounds.size);
            bc.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        }

        static List<GameObject> GetTopLevel(GameObject[] objects)
        {
            var set = new HashSet<GameObject>(objects);
            var result = new List<GameObject>();
            foreach (var go in objects)
            {
                if (!HasAncestorIn(go.transform, set))
                    result.Add(go);
            }
            return result;
        }

        static bool HasAncestorIn(Transform t, HashSet<GameObject> set)
        {
            var parent = t.parent;
            while (parent != null)
            {
                if (set.Contains(parent.gameObject)) return true;
                parent = parent.parent;
            }
            return false;
        }

        static Transform GetCommonParent(List<GameObject> roots)
        {
            var first = roots[0].transform.parent;
            foreach (var go in roots)
            {
                if (go.transform.parent != first) return null;
            }
            return first;
        }

        static int GetEarliestSiblingIndex(List<GameObject> roots, Transform parent)
        {
            int earliest = int.MaxValue;
            foreach (var go in roots)
            {
                if (go.transform.parent == parent)
                    earliest = Mathf.Min(earliest, go.transform.GetSiblingIndex());
            }
            return earliest == int.MaxValue ? 0 : earliest;
        }

        static Bounds ComputeCombinedBounds(List<GameObject> roots)
        {
            bool initialised = false;
            Bounds combined = default;

            foreach (var go in roots)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    if (!initialised) { combined = r.bounds; initialised = true; }
                    else combined.Encapsulate(r.bounds);
                }
                foreach (var c in go.GetComponentsInChildren<Collider>())
                {
                    if (!initialised) { combined = c.bounds; initialised = true; }
                    else combined.Encapsulate(c.bounds);
                }
            }

            if (!initialised)
            {
                // No renderers or colliders — fall back to transform origins
                combined = new Bounds(roots[0].transform.position, Vector3.zero);
                foreach (var go in roots) combined.Encapsulate(go.transform.position);
            }

            return combined;
        }

        static Vector3 AveragePosition(List<GameObject> roots)
        {
            var sum = Vector3.zero;
            foreach (var go in roots) sum += go.transform.position;
            return sum / roots.Count;
        }
    }
}
