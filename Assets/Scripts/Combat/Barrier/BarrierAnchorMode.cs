/// <summary>
/// What a barrier attaches itself to. The anchor supplies the barrier's position for its whole
/// lifetime, and — for anchors that are characters — the max HP the barrier's own pool is
/// derived from.
/// </summary>
public enum BarrierAnchorMode
{
    /// <summary>One barrier on the caster.</summary>
    Caster = 0,

    /// <summary>
    /// One barrier per entity spawned earlier in this same cast. Reads
    /// <see cref="SkillCastExecutionState.SpawnedSummons"/> — never searches the scene.
    /// </summary>
    SpawnedEntitiesFromCurrentCast = 1,

    /// <summary>One barrier pinned to the world position the cast originated from.</summary>
    CastPosition = 2,
}
