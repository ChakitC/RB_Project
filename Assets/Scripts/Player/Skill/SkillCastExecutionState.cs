using System.Collections.Generic;

/// <summary>
/// Request-scoped scratch space shared by every payload inside one cast.
/// Lets a later step consume what an earlier step produced without sweeping the scene
/// (for example a barrier anchored to the turret this same cast just deployed).
/// </summary>
public sealed class SkillCastExecutionState
{
    static readonly IReadOnlyList<SummonedEntityRuntime> EmptySummons =
        new List<SummonedEntityRuntime>(0);

    List<SummonedEntityRuntime> spawnedSummons;

    /// <summary>Summons spawned by this cast, in spawn order. Never null.</summary>
    public IReadOnlyList<SummonedEntityRuntime> SpawnedSummons =>
        spawnedSummons != null ? spawnedSummons : EmptySummons;

    public bool HasSpawnedSummons => spawnedSummons != null && spawnedSummons.Count > 0;

    public void RegisterSpawnedSummon(SummonedEntityRuntime runtime)
    {
        if (runtime == null)
            return;

        spawnedSummons ??= new List<SummonedEntityRuntime>(2);
        if (!spawnedSummons.Contains(runtime))
            spawnedSummons.Add(runtime);
    }
}
