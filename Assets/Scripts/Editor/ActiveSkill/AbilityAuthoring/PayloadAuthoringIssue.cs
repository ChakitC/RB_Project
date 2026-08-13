using System;

public enum PayloadAuthoringSeverity
{
    Info,
    Warning,
    Error,
}

// Error/Warning/Info issue reported by a payload designer descriptor. Mirrors the shape of
// SkillUpgradeValidationIssue (SkillUpgradeTreeValidator.cs), but adds an Info level because
// descriptor summaries surface guidance that must not block Create/Save.
public readonly struct PayloadAuthoringIssue
{
    public PayloadAuthoringIssue(PayloadAuthoringSeverity severity, string message)
    {
        Severity = severity;
        Message = message ?? string.Empty;
    }

    public PayloadAuthoringSeverity Severity { get; }
    public string Message { get; }

    public static PayloadAuthoringIssue Info(string message) => new(PayloadAuthoringSeverity.Info, message);
    public static PayloadAuthoringIssue Warning(string message) => new(PayloadAuthoringSeverity.Warning, message);
    public static PayloadAuthoringIssue Error(string message) => new(PayloadAuthoringSeverity.Error, message);

    public override string ToString() => $"[{Severity}] {Message}";
}

public static class PayloadAuthoringIssueListExtensions
{
    public static bool HasErrors(this System.Collections.Generic.List<PayloadAuthoringIssue> issues)
    {
        if (issues == null)
            return false;

        for (int i = 0; i < issues.Count; i++)
        {
            if (issues[i].Severity == PayloadAuthoringSeverity.Error)
                return true;
        }

        return false;
    }

    public static bool HasWarnings(this System.Collections.Generic.List<PayloadAuthoringIssue> issues)
    {
        if (issues == null)
            return false;

        for (int i = 0; i < issues.Count; i++)
        {
            if (issues[i].Severity == PayloadAuthoringSeverity.Warning)
                return true;
        }

        return false;
    }
}
