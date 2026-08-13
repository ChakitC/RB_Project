using System;
using System.Collections.Generic;

// Strongly-typed base for a payload designer descriptor. PayloadType is derived from TPayload,
// so a descriptor cannot accidentally map to the wrong (or an abstract) type -- concrete
// descriptors only implement the typed abstract members below.
public abstract class PayloadDesignerDescriptorBase<TPayload> : IPayloadDesignerDescriptor
    where TPayload : SkillPayloadDef
{
    public Type PayloadType => typeof(TPayload);
    public abstract string DisplayName { get; }
    public virtual string Description => string.Empty;
    public virtual string Category => "General";

    void IPayloadDesignerDescriptor.ApplySafeDefaults(SkillPayloadDef payload, PayloadDesignerContext context)
    {
        if (payload is TPayload typed)
            ApplySafeDefaults(typed, context);
    }

    void IPayloadDesignerDescriptor.DrawWizard(SkillPayloadDef draft, PayloadDesignerContext context)
    {
        if (draft is TPayload typed)
            DrawWizard(typed, context);
    }

    PayloadGameplaySummary IPayloadDesignerDescriptor.BuildSummary(SkillPayloadDef payload, PayloadDesignerContext context)
    {
        return payload is TPayload typed ? BuildSummary(typed, context) : PayloadGameplaySummary.Empty;
    }

    void IPayloadDesignerDescriptor.CollectAuthoringIssues(
        SkillPayloadDef payload,
        PayloadDesignerContext context,
        List<PayloadAuthoringIssue> issues)
    {
        if (issues == null)
            return;

        if (payload is not TPayload typed)
        {
            issues.Add(PayloadAuthoringIssue.Error(
                $"{GetType().Name} received a payload of type '{payload?.GetType().Name ?? "null"}', " +
                $"expected '{typeof(TPayload).Name}'."));
            return;
        }

        CollectAuthoringIssues(typed, context, issues);
    }

    protected abstract void ApplySafeDefaults(TPayload payload, PayloadDesignerContext context);
    protected abstract void DrawWizard(TPayload payload, PayloadDesignerContext context);
    protected abstract PayloadGameplaySummary BuildSummary(TPayload payload, PayloadDesignerContext context);

    protected abstract void CollectAuthoringIssues(
        TPayload payload,
        PayloadDesignerContext context,
        List<PayloadAuthoringIssue> issues);

    // Convenience for the common case: reuse the payload's own runtime CollectValidationIssues
    // (already covers "required reference missing" and similar) and report every message as a
    // blocking Error. Descriptors add supplementary Warning/Info issues on top of this call.
    protected static void CollectRuntimeValidationIssuesAsErrors(SkillPayloadDef payload, List<PayloadAuthoringIssue> issues)
    {
        if (payload == null || issues == null)
            return;

        var runtimeIssues = new List<string>();
        payload.CollectValidationIssues(runtimeIssues);
        for (int i = 0; i < runtimeIssues.Count; i++)
            issues.Add(PayloadAuthoringIssue.Error(runtimeIssues[i]));
    }
}
