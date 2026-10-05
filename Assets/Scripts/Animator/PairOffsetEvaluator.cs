using System.Collections.Generic;
using UnityEngine;

// Blends rotation deltas in the tangent space around identity. Missing profiles
// contribute identity; a single contribution matches identity-to-offset Slerp.
public sealed class PairOffsetEvaluator
{
    private struct Accumulator
    {
        public Vector3 RotationVector;
        public float Weight;
    }

    private readonly Dictionary<Transform, Accumulator> bones = new();
    public IEnumerable<Transform> Bones => bones.Keys;
    public void Clear() => bones.Clear();

    public void Add(Transform bone, Vector3 eulerOffset, float weight)
    {
        weight = Mathf.Clamp01(weight);
        if (bone == null || weight <= 0f) return;
        Quaternion q = Quaternion.Euler(eulerOffset);
        // Canonical hemisphere, including a deterministic tie at 180 degrees.
        if (q.w < 0f || (q.w == 0f && (q.x < 0f || (q.x == 0f && (q.y < 0f || (q.y == 0f && q.z < 0f))))))
            q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
        Vector3 axis = new Vector3(q.x, q.y, q.z);
        float length = axis.magnitude;
        Vector3 rotation = length > 0.000001f ? axis * (2f * Mathf.Atan2(length, q.w) / length) : Vector3.zero;
        bones.TryGetValue(bone, out var sum);
        sum.RotationVector += rotation * weight;
        sum.Weight += weight;
        bones[bone] = sum;
    }

    public Quaternion GetRotation(Transform bone)
    {
        if (!bones.TryGetValue(bone, out var sum)) return Quaternion.identity;
        Vector3 rotation = sum.RotationVector / Mathf.Max(1f, sum.Weight);
        float angle = rotation.magnitude;
        return angle > 0.000001f ? Quaternion.AngleAxis(angle * Mathf.Rad2Deg, rotation / angle) : Quaternion.identity;
    }

    public static PairOffsetProfilesSO.PairOffsetProfile FindProfile(PairOffsetProfilesSO profiles,
        PairOffsetBasePose pose, PairOffsetUpperAction action, AnimationClip baseClip, AnimationClip upperClip)
    {
        if (profiles == null) return null;
        var keyed = profiles.FindProfile(pose, action, false);
        return keyed ?? (baseClip != null && upperClip != null ? profiles.FindProfile(baseClip, upperClip, false) : null);
    }
}
