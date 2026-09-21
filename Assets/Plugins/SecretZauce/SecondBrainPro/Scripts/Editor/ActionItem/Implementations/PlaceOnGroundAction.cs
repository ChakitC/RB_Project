using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    public enum BoundsSource
    {
        Collider,
        Mesh,
    }

    public enum PlaceRaycastMethod
    {
        SinglePoint,
        FourCorners,
    }

    public enum PlaceRaycastDirection
    {
        Down,
        Up,
        Forward,
        Back,
        Left,
        Right,
    }

    /// <summary>
    /// Places selected scene objects on the nearest surface along a configurable ray direction.
    /// Works in both edit mode and play mode using geometric raycasts (no physics simulation required).
    /// </summary>
    public class PlaceOnGroundAction : ActionItem
    {
        [Tooltip("Layers considered as ground. Default includes everything.")]
        public LayerMask groundLayers = ~0;

        [Tooltip("Whether to use the object's Collider or Renderer bounds for footprint calculation.")]
        public BoundsSource boundsSource = BoundsSource.Collider;

        [Tooltip("Single ray from the object center, or four rays from the bounding-box face corners.")]
        public PlaceRaycastMethod raycastMethod = PlaceRaycastMethod.SinglePoint;

        [Tooltip("Direction to shoot rays for ground detection.")]
        public PlaceRaycastDirection raycastDirection = PlaceRaycastDirection.Down;

        [Tooltip("Rotate the placed object to align its up-axis with the surface normal.")]
        public bool alignToSurface = true;

        [Tooltip("Maximum ray distance.")]
        public float maxRayDistance = 1000f;

        public override string ActionPath => "Examples";

        public override string GetDetailDisplay() =>
            $"{raycastMethod} | {(alignToSurface ? "Align" : "No Align")}";

        public override void Execute()
        {
            var selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                Debug.LogWarning("[PlaceOnGroundAction] No GameObjects selected.");
                return;
            }

            Undo.SetCurrentGroupName("Place On Ground");
            int group = Undo.GetCurrentGroup();

            var dir = GetDirectionVector(raycastDirection);
            foreach (var go in selected)
                TryPlace(go, dir);

            Undo.CollapseUndoOperations(group);
        }

        void TryPlace(GameObject go, Vector3 dir)
        {
            var bounds = GetBounds(go);
            if (!bounds.HasValue)
            {
                Debug.LogWarning($"[PlaceOnGroundAction] '{go.name}' has no {boundsSource} component — skipped.");
                return;
            }

            var b = bounds.Value;
            Vector3 frontier = GetExtremity(b, dir);
            Vector3[] origins = raycastMethod == PlaceRaycastMethod.FourCorners
                ? GetCornerOrigins(b, dir)
                : new[] { b.center };

            bool hitAny = false;
            float closestDist = float.MaxValue;
            Vector3 bestPoint = Vector3.zero;
            Vector3 bestNormal = -dir;

            foreach (var origin in origins)
            {
                var hits = Physics.RaycastAll(origin, dir, maxRayDistance, groundLayers,
                    QueryTriggerInteraction.Ignore);

                foreach (var hit in hits)
                {
                    if (IsPartOf(hit.collider, go)) continue;

                    float dist = Vector3.Dot(hit.point - origin, dir);
                    if (dist >= 0f && dist < closestDist)
                    {
                        closestDist = dist;
                        bestPoint = hit.point;
                        bestNormal = hit.normal;
                        hitAny = true;
                    }
                }
            }

            if (!hitAny) return;

            Undo.RecordObject(go.transform, "Place On Ground");

            // Shift position so the frontier (bottom face) lands on the hit point
            go.transform.position = bestPoint + (go.transform.position - frontier);

            if (alignToSurface)
                go.transform.rotation =
                    Quaternion.FromToRotation(go.transform.up, bestNormal) * go.transform.rotation;
        }

        Bounds? GetBounds(GameObject go)
        {
            if (boundsSource == BoundsSource.Mesh)
            {
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return null;
                var b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                return b;
            }

            var colliders = go.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                var b = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++) b.Encapsulate(colliders[i].bounds);
                return b;
            }

            // Fallback to renderer bounds when collider mode finds nothing
            var fallback = go.GetComponentsInChildren<Renderer>();
            if (fallback.Length == 0) return null;
            var rb = fallback[0].bounds;
            for (int i = 1; i < fallback.Length; i++) rb.Encapsulate(fallback[i].bounds);
            return rb;
        }

        /// <summary>Returns the point on the AABB surface furthest in <paramref name="dir"/>.</summary>
        static Vector3 GetExtremity(Bounds b, Vector3 dir)
        {
            return b.center + new Vector3(
                dir.x != 0f ? Mathf.Sign(dir.x) * b.extents.x : 0f,
                dir.y != 0f ? Mathf.Sign(dir.y) * b.extents.y : 0f,
                dir.z != 0f ? Mathf.Sign(dir.z) * b.extents.z : 0f);
        }

        /// <summary>
        /// Returns 4 ray origins at the corners of the bounding-box face perpendicular to <paramref name="dir"/>.
        /// Uses the two axes most perpendicular to dir so cardinal directions always give the correct face corners.
        /// </summary>
        static Vector3[] GetCornerOrigins(Bounds b, Vector3 dir)
        {
            float ax = Mathf.Abs(dir.x), ay = Mathf.Abs(dir.y), az = Mathf.Abs(dir.z);

            Vector3 side1, side2;
            if (ay >= ax && ay >= az)
            {
                // Mostly Y — face corners live on the XZ plane
                side1 = new Vector3(b.extents.x, 0f, 0f);
                side2 = new Vector3(0f, 0f, b.extents.z);
            }
            else if (az >= ax)
            {
                // Mostly Z — face corners live on the XY plane
                side1 = new Vector3(b.extents.x, 0f, 0f);
                side2 = new Vector3(0f, b.extents.y, 0f);
            }
            else
            {
                // Mostly X — face corners live on the YZ plane
                side1 = new Vector3(0f, b.extents.y, 0f);
                side2 = new Vector3(0f, 0f, b.extents.z);
            }

            return new[]
            {
                b.center + side1 + side2,
                b.center - side1 + side2,
                b.center + side1 - side2,
                b.center - side1 - side2,
            };
        }

        static bool IsPartOf(Collider col, GameObject root)
        {
            var t = col.transform;
            while (t != null)
            {
                if (t.gameObject == root) return true;
                t = t.parent;
            }
            return false;
        }

        static Vector3 GetDirectionVector(PlaceRaycastDirection d) => d switch
        {
            PlaceRaycastDirection.Up      => Vector3.up,
            PlaceRaycastDirection.Forward => Vector3.forward,
            PlaceRaycastDirection.Back    => Vector3.back,
            PlaceRaycastDirection.Left    => Vector3.left,
            PlaceRaycastDirection.Right   => Vector3.right,
            _                             => Vector3.down,
        };
    }
}
