using System;
using System.Collections.Generic;

// Editor-only descriptor for a designer-facing SkillPayloadDef type. Runtime payload classes
// must compile and execute without this interface -- do not reference it from a non-Editor
// assembly. See Docs/ARCHITECTURE/NODE_CENTRIC_PAYLOAD_AUTHORING_PLAN.md section 9.2.
public interface IPayloadDesignerDescriptor
{
    Type PayloadType { get; }
    string DisplayName { get; }
    string Description { get; }
    string Category { get; }

    void ApplySafeDefaults(SkillPayloadDef payload, PayloadDesignerContext context);
    void DrawWizard(SkillPayloadDef draft, PayloadDesignerContext context);
    PayloadGameplaySummary BuildSummary(SkillPayloadDef payload, PayloadDesignerContext context);

    void CollectAuthoringIssues(
        SkillPayloadDef payload,
        PayloadDesignerContext context,
        List<PayloadAuthoringIssue> issues);
}
