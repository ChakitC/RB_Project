#if UNITY_EDITOR
using UnityEngine;

public enum MapContentIssueSeverity
{
    /// <summary>Authoring is degraded but runtime has a working fallback.</summary>
    Warning,

    /// <summary>The run would break, soft-lock, or silently produce wrong content.</summary>
    Error,
}

/// <summary>
/// One finding from <see cref="MapContentValidator"/>. <see cref="Context"/> is the asset the
/// finding belongs to, so a logged issue can be clicked to select it in the Project window.
/// </summary>
public sealed class MapContentIssue
{
    public MapContentIssueSeverity Severity { get; }

    /// <summary>Where the finding lives, for example the run config or room definition name.</summary>
    public string Scope { get; }

    public string Message { get; }
    public Object Context { get; }

    public MapContentIssue(MapContentIssueSeverity severity, string scope, string message, Object context)
    {
        Severity = severity;
        Scope = scope;
        Message = message;
        Context = context;
    }

    public bool IsError => Severity == MapContentIssueSeverity.Error;

    public override string ToString()
    {
        return $"{(IsError ? "ERROR" : "WARN ")} [{Scope}] {Message}";
    }
}
#endif
