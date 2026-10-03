using System;
using UnityEngine;

/// <summary>A character reference bound to one enabled lifetime.</summary>
public readonly struct AITargetHandle : IEquatable<AITargetHandle>
{
    public readonly CharacteContext Context;
    public readonly int Generation;
    public readonly int InstanceId;

    public AITargetHandle(CharacteContext context) : this(context, context != null ? context.LifeGeneration : 0) { }

    public AITargetHandle(CharacteContext context, int generation)
    {
        Context = context;
        Generation = generation;
        InstanceId = context != null ? context.GetInstanceID() : 0;
    }

    public bool IsLive => Context != null && Context.isActiveAndEnabled &&
        Context.gameObject.activeInHierarchy && Context.LifeGeneration == Generation;
    public Transform LegacyTransform => IsLive
        ? (Context.TargetInfo != null ? Context.TargetInfo.transform : Context.transform) : null;
    public bool Equals(AITargetHandle other) => InstanceId == other.InstanceId && Generation == other.Generation;
    public override bool Equals(object obj) => obj is AITargetHandle other && Equals(other);
    public override int GetHashCode() => unchecked(InstanceId * 397 ^ Generation);
    public static bool operator ==(AITargetHandle a, AITargetHandle b) => a.Equals(b);
    public static bool operator !=(AITargetHandle a, AITargetHandle b) => !a.Equals(b);
}
