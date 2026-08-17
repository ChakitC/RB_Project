/// <summary>
/// Why a skill payload refused to produce a gameplay effect. Used to decide whether the cast
/// transaction commits (energy, charge, cooldown) and what the player is told.
/// </summary>
public enum SkillExecutionFailureReason
{
    None = 0,

    /// <summary>Payload ran but produced nothing (no enabled step, zero spawns, ...).</summary>
    NoEffect,

    /// <summary>Ground, clearance, NavMesh, or layout could not accept the requested placement.</summary>
    PlacementBlocked,

    /// <summary>Authoring data is incomplete (missing prefab, missing skill id, ...).</summary>
    MissingAuthoringData,

    /// <summary>A required runtime service was unavailable (map, controller, caster context, ...).</summary>
    MissingRuntimeContext,

    /// <summary>The payload actively rejected this caster or this cast.</summary>
    Rejected,
}

/// <summary>
/// Result of running a skill payload. A cast is a transaction: only a successful execution
/// commits energy, charge, and cooldown.
/// </summary>
public readonly struct SkillExecutionResult
{
    public const string GenericFailureMessage = "Cannot use this skill right now";
    public const string PlacementFailureMessage = "Cannot deploy here";

    public readonly bool Success;
    public readonly SkillExecutionFailureReason Reason;

    /// <summary>Verbose reason for logs. Never shown to the player.</summary>
    public readonly string DebugMessage;

    private SkillExecutionResult(bool success, SkillExecutionFailureReason reason, string debugMessage)
    {
        Success = success;
        Reason = reason;
        DebugMessage = debugMessage;
    }

    public static SkillExecutionResult Succeeded =>
        new SkillExecutionResult(true, SkillExecutionFailureReason.None, null);

    public static SkillExecutionResult Failed(SkillExecutionFailureReason reason, string debugMessage = null)
    {
        return new SkillExecutionResult(
            false,
            reason == SkillExecutionFailureReason.None ? SkillExecutionFailureReason.NoEffect : reason,
            debugMessage);
    }

    /// <summary>Short, player-facing line. Intentionally coarse compared to <see cref="DebugMessage"/>.</summary>
    public string PublicMessage
    {
        get
        {
            if (Success)
                return string.Empty;

            switch (Reason)
            {
                case SkillExecutionFailureReason.PlacementBlocked:
                    return PlacementFailureMessage;
                default:
                    return GenericFailureMessage;
            }
        }
    }
}
