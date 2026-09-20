#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Display coordinates only. Transform identities and authoring paths remain untouched.
public static class SkillHitboxBonePickerLayout
{
    static string Name(Transform bone) => bone.name.ToLowerInvariant().Split(':').Last();

    public static bool IsFinger(Transform bone, Transform root)
    {
        for (var node = bone; node != null && node != root; node = node.parent)
        {
            string name = Name(node);
            if (new[] { "thumb", "index", "middle", "ring", "pinky", "little", "finger" }.Any(name.StartsWith)) return true;
        }
        return false;
    }

    public static bool IsWeapon(Transform bone, Transform root)
    {
        for (var node = bone; node != null && node != root; node = node.parent)
        {
            string name = Name(node);
            if (name.Contains("weapon") || name.Contains("weappon") || name.Contains("wapone")) return true;
        }
        return false;
    }

    public static int Side(Transform bone)
    {
        string name = Name(bone);
        if (name.Contains("left") || name.EndsWith(".l") || name.EndsWith("_l")) return -1;
        if (name.Contains("right") || name.EndsWith(".r") || name.EndsWith("_r")) return 1;
        return 0;
    }

    public static bool IsWeaponAttachment(Transform bone, Transform root) => IsWeapon(bone, root) &&
        (bone.parent == null || bone.parent == root || !IsWeapon(bone.parent, root));

    public static bool TryDiagramPosition(Transform bone, out Vector3 position)
    {
        string name = Name(bone);
        int side = Side(bone);
        position = Vector3.zero;
        if (name.Contains("head")) position = new Vector3(0, 1.25f, 0);
        else if (name.Contains("neck")) position = new Vector3(0, 1.02f, 0);
        else if (name.Contains("spine") || name.Contains("chest"))
            position = new Vector3(0, name.Contains("02") || name.Contains("2") || name.Contains("chest") ? .78f : .4f, 0);
        else if (name == "root.x" || name.Contains("hips") || name.Contains("pelvis")) position = Vector3.zero;
        else if (side == 0) return false;
        else if (name.Contains("shoulder") || name.Contains("clavicle")) position = new Vector3(side * .24f, .92f, 0);
        else if (name.Contains("hand") || name.Contains("wrist")) position = new Vector3(side * .98f, .35f, 0);
        else if (name.Contains("forearm") || name.Contains("lowerarm"))
            position = new Vector3(side * (name.Contains("twist") ? .85f : .69f), name.Contains("twist") ? .46f : .6f, 0);
        else if (name.Contains("arm"))
            position = new Vector3(side * (name.Contains("twist") ? .51f : .34f), name.Contains("twist") ? .75f : .88f, 0);
        else if (name.Contains("thigh") || name.Contains("upperleg"))
            position = new Vector3(side * .19f, name.Contains("twist") ? -.36f : -.06f, 0);
        else if (name.Contains("toe")) position = new Vector3(side * .3f, -1.37f, .08f);
        else if (name.Contains("foot") || name.Contains("ankle")) position = new Vector3(side * .22f, -1.3f, 0);
        else if (name.Contains("leg") || name.Contains("calf") || name.Contains("shin"))
            position = new Vector3(side * .21f, name.Contains("twist") ? -1.02f : -.7f, 0);
        else return false;
        return true;
    }

    public static Dictionary<Transform, Vector3> BodyPositions(Transform root, IEnumerable<Transform> bones, bool modelPose, float yaw, float pitch)
    {
        var body = bones.Where(b => b != null && !IsFinger(b, root) && !IsWeapon(b, root)).ToArray();
        var diagram = new Dictionary<Transform, Vector3>();
        foreach (var bone in body)
            if (TryDiagramPosition(bone, out var point)) diagram[bone] = Quaternion.Euler(pitch, yaw, 0) * point;
        if (!modelPose && diagram.Count >= 6) return diagram;
        return body.ToDictionary(b => b, b => SkillHitboxBonePickerWindow.ProjectBone(root, b, yaw, pitch));
    }

    public static Dictionary<Transform, Vector3> WeaponPositions(Transform root, IEnumerable<Transform> bones, bool modelPose, float yaw, float pitch)
    {
        var all = bones.Where(b => b != null).ToArray();
        var body = BodyPositions(root, all, modelPose, 0, 0);
        var result = new Dictionary<Transform, Vector3>();
        if (body.Count == 0) return result;
        float minX = body.Values.Min(p => p.x), maxX = body.Values.Max(p => p.x);
        float minY = body.Values.Min(p => p.y), maxY = body.Values.Max(p => p.y);
        float width = Mathf.Max(.1f, maxX - minX), height = Mathf.Max(.1f, maxY - minY);
        int left = 0, right = 0;
        foreach (var bone in all.Where(b => IsWeaponAttachment(b, root)).OrderBy(b => b.name, StringComparer.Ordinal))
        {
            bool onLeft = Side(bone) < 0;
            int row = onLeft ? left++ : right++;
            var point = new Vector3(onLeft ? minX - width * .16f : maxX + width * .16f,
                minY + height * (.31f - row * .12f), 0);
            result[bone] = Quaternion.Euler(pitch, yaw, 0) * point;
        }
        return result;
    }

    public static Vector2 ToCanvas(Vector3 point, Vector2 center, float scale, Vector2 size, Vector2 pan) =>
        new Vector2(size.x * .5f + (point.x - center.x) * scale, size.y * .5f - (point.y - center.y) * scale) + pan;

    public static Dictionary<Transform, Vector2> SeparateTwistLanes(IReadOnlyDictionary<Transform, Vector2> original,
        out Dictionary<Transform, Vector2> starts)
    {
        var points = original.ToDictionary(p => p.Key, p => p.Value);
        starts = new Dictionary<Transform, Vector2>();
        foreach (var pair in original)
            if (pair.Key.parent != null && original.TryGetValue(pair.Key.parent, out var parent)) starts[pair.Key] = parent;
        var lanes = new Dictionary<Transform, int>();
        foreach (var bone in original.Keys.Where(b => Name(b).Contains("twist")).OrderBy(b => b.name, StringComparer.Ordinal))
        {
            if (!starts.TryGetValue(bone, out var start)) continue;
            Vector2 delta = original[bone] - start;
            var continuation = original.Where(p => p.Key != bone && p.Key.parent == bone.parent && !Name(p.Key).Contains("twist"))
                .Select(p => p.Value - start).Where(d => Vector2.Dot(d, delta) > 0).OrderByDescending(d => d.sqrMagnitude).FirstOrDefault();
            Vector2 direction = continuation.sqrMagnitude > .001f ? continuation.normalized : delta.sqrMagnitude > .001f ? delta.normalized : Vector2.up;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            int side = Side(bone);
            if ((side != 0 && normal.x * side < 0) || (side == 0 && normal.x < 0)) normal = -normal;
            lanes.TryGetValue(bone.parent, out int lane); lanes[bone.parent] = ++lane;
            Vector2 offset = normal * (30 * lane);
            // Separate both ends in screen space so zooming never merges the two hit targets.
            starts[bone] = start + offset;
            points[bone] = start + offset + direction * Mathf.Max(18, Vector2.Dot(delta, direction));
        }
        return points;
    }

    // Keep every candidate under the pointer, including overlapping branch segments.
    // Selecting the first nearest Transform would make coincident bones unreachable.
    public static Transform[] HitCandidates(IReadOnlyDictionary<Transform, Vector2> points, Vector2 mouse,
        IReadOnlyDictionary<Transform, Vector2> starts = null)
    {
        var hits = new List<(Transform bone, float distance)>();
        foreach (var pair in points)
        {
            float joint = Vector2.Distance(mouse, pair.Value);
            float segment = float.PositiveInfinity;
            Vector2 parent = default;
            bool hasStart = starts != null ? starts.TryGetValue(pair.Key, out parent) :
                pair.Key.parent != null && points.TryGetValue(pair.Key.parent, out parent);
            if (hasStart)
            {
                Vector2 delta = pair.Value - parent;
                float t = delta.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector2.Dot(mouse - parent, delta) / delta.sqrMagnitude) : 0;
                segment = Vector2.Distance(mouse, parent + delta * t);
            }
            if (joint <= 10 || segment <= 8) hits.Add((pair.Key, Mathf.Min(joint, segment)));
        }
        return hits.OrderBy(h => h.distance).ThenBy(h => h.bone.GetInstanceID()).Select(h => h.bone).ToArray();
    }
}
#endif
