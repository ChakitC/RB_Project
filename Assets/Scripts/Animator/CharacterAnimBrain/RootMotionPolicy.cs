using System;

/// <summary>
/// What the animation layer is asking of root motion right now. Immutable and declarative: the
/// Brain publishes it, and a root-motion adapter (<c>RootMotionCCDriver</c>,
/// <c>RootMotionNavMeshDriver</c>) is what actually moves the character.
///
/// Publishing the four flags as one value is the point. They used to be set from different places
/// — <c>Active</c> from <c>EnterExclusiveLocomotion</c>, the rest from the per-playback policy
/// helpers — so an adapter could observe root motion as active while the shape flags still
/// described the previous playback.
/// </summary>
public readonly struct RootMotionPolicy : IEquatable<RootMotionPolicy>
{
    /// <summary>Root motion drives the character. Adapters do nothing while this is false.</summary>
    public readonly bool Active;

    /// <summary>Vertical root motion is discarded; only planar movement is applied.</summary>
    public readonly bool PlanarOnly;

    /// <summary>The clip's yaw is applied to the actor root.</summary>
    public readonly bool ApplyYaw;

    /// <summary>Character-vs-character collision is suspended for the duration.</summary>
    public readonly bool IgnoreCharacterCollision;

    public static readonly RootMotionPolicy Inactive = default;

    public RootMotionPolicy(bool active, bool planarOnly, bool applyYaw, bool ignoreCharacterCollision)
    {
        Active = active;
        PlanarOnly = planarOnly;
        ApplyYaw = applyYaw;
        IgnoreCharacterCollision = ignoreCharacterCollision;
    }

    public RootMotionPolicy WithActive(bool active) =>
        new(active, PlanarOnly, ApplyYaw, IgnoreCharacterCollision);

    /// <summary>
    /// Applies the per-playback shape. Yaw follows <paramref name="planarOnly"/>: a planar playback
    /// is the one that owns facing, so the two have never been authored independently.
    /// </summary>
    public RootMotionPolicy WithShape(bool planarOnly, bool ignoreCharacterCollision) =>
        new(Active, planarOnly, planarOnly, ignoreCharacterCollision);

    public bool Equals(RootMotionPolicy other) =>
        Active == other.Active &&
        PlanarOnly == other.PlanarOnly &&
        ApplyYaw == other.ApplyYaw &&
        IgnoreCharacterCollision == other.IgnoreCharacterCollision;

    public override bool Equals(object obj) => obj is RootMotionPolicy other && Equals(other);

    public override int GetHashCode() =>
        (Active ? 1 : 0) | (PlanarOnly ? 2 : 0) | (ApplyYaw ? 4 : 0) | (IgnoreCharacterCollision ? 8 : 0);

    public static bool operator ==(RootMotionPolicy left, RootMotionPolicy right) => left.Equals(right);

    public static bool operator !=(RootMotionPolicy left, RootMotionPolicy right) => !left.Equals(right);

    public override string ToString() =>
        Active
            ? $"RootMotion(active, planar:{PlanarOnly}, yaw:{ApplyYaw}, ignoreCharCollision:{IgnoreCharacterCollision})"
            : "RootMotion(inactive)";
}
