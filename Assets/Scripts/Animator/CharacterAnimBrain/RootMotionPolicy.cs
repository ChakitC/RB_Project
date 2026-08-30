using System;

/// <summary>
/// What the animation layer is asking of root motion right now. Immutable and declarative: the
/// Brain publishes it, and a root-motion adapter (<c>RootMotionCCDriver</c>,
/// <c>RootMotionNavMeshDriver</c>) is what actually moves the character.
///
/// Publishing the flags as one value is the point. They used to be set from different places
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

    /// <summary>
    /// The adapter must constrain the animation delta against environment geometry and recover the
    /// actor to a safe NavMesh position when the playback ends.
    ///
    /// Opt-in per playback on purpose. Ordinary skill and chain clips have always applied their
    /// delta unconstrained, and silently sweeping every one of them would change authored motion
    /// across the whole game.
    /// </summary>
    public readonly bool EnvironmentSafe;

    public static readonly RootMotionPolicy Inactive = default;

    public RootMotionPolicy(
        bool active,
        bool planarOnly,
        bool applyYaw,
        bool ignoreCharacterCollision,
        bool environmentSafe = false)
    {
        Active = active;
        PlanarOnly = planarOnly;
        ApplyYaw = applyYaw;
        IgnoreCharacterCollision = ignoreCharacterCollision;
        EnvironmentSafe = environmentSafe;
    }

    public RootMotionPolicy WithActive(bool active) =>
        new(active, PlanarOnly, ApplyYaw, IgnoreCharacterCollision, EnvironmentSafe);

    /// <summary>
    /// Applies the per-playback shape for callers that have never authored yaw independently: a
    /// planar playback is the one that owns facing, so yaw follows <paramref name="planarOnly"/>.
    /// </summary>
    public RootMotionPolicy WithShape(bool planarOnly, bool ignoreCharacterCollision) =>
        new(Active, planarOnly, planarOnly, ignoreCharacterCollision, false);

    /// <summary>
    /// Applies the per-playback shape with yaw and environment safety authored independently. The
    /// Special Point reaction needs full translation (including Y) <em>and</em> animation yaw, which
    /// the two-argument overload cannot express.
    /// </summary>
    public RootMotionPolicy WithShape(
        bool planarOnly,
        bool applyYaw,
        bool ignoreCharacterCollision,
        bool environmentSafe) =>
        new(Active, planarOnly, applyYaw, ignoreCharacterCollision, environmentSafe);

    public bool Equals(RootMotionPolicy other) =>
        Active == other.Active &&
        PlanarOnly == other.PlanarOnly &&
        ApplyYaw == other.ApplyYaw &&
        IgnoreCharacterCollision == other.IgnoreCharacterCollision &&
        EnvironmentSafe == other.EnvironmentSafe;

    public override bool Equals(object obj) => obj is RootMotionPolicy other && Equals(other);

    public override int GetHashCode() =>
        (Active ? 1 : 0) |
        (PlanarOnly ? 2 : 0) |
        (ApplyYaw ? 4 : 0) |
        (IgnoreCharacterCollision ? 8 : 0) |
        (EnvironmentSafe ? 16 : 0);

    public static bool operator ==(RootMotionPolicy left, RootMotionPolicy right) => left.Equals(right);

    public static bool operator !=(RootMotionPolicy left, RootMotionPolicy right) => !left.Equals(right);

    public override string ToString() =>
        Active
            ? $"RootMotion(active, planar:{PlanarOnly}, yaw:{ApplyYaw}, ignoreCharCollision:{IgnoreCharacterCollision}, envSafe:{EnvironmentSafe})"
            : "RootMotion(inactive)";
}
