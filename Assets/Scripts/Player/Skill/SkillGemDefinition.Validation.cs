using System;
using System.Collections.Generic;

public partial class SkillGemDefinition
{
    /// <summary>Runtime rules are always checked. Authoring also checks IDs, warnings and, in Editor, ownership.</summary>
    public void CollectValidationIssues(List<SkillValidationIssue> issues,
        SkillExecutionKind kind = SkillExecutionKind.StandardSkill, bool includeAuthoring = false)
    {
        if (issues != null)
            CollectDefinitionIssues(issues, kind, includeAuthoring, string.Empty, null);
    }

    private void CollectDefinitionIssues(List<SkillValidationIssue> issues, SkillExecutionKind kind,
        bool includeAuthoring, string location, string stepId)
    {
        void Add(string code, string message, SkillValidationSeverity severity = SkillValidationSeverity.Error) =>
            issues.Add(new SkillValidationIssue(code, severity, this, location, stepId, message));

        if (includeAuthoring && !HasSkillId())
            Add("skill.id", "Skill ID is required.");

        var messages = new List<string>();
        if (TryGetCombo(out var steps))
        {
            if (payload != null)
                Add("combo.root-payload", "Combo execution belongs to its steps; remove the root payload.");
            if (steps.Count == 0)
                Add("combo.empty", "Combo has no steps.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                string childLocation = $"Step {i + 1} ({step.EntryId})";
                void StepError(string code, string message) => issues.Add(new SkillValidationIssue(
                    code, SkillValidationSeverity.Error, this, childLocation, step.EntryId, message));
                if (string.IsNullOrWhiteSpace(step.EntryId) || !ids.Add(step.EntryId))
                    StepError("combo.step-id", "Step has a missing or duplicate ID.");
                if (!float.IsFinite(step.chainWindowN.x) || !float.IsFinite(step.chainWindowN.y) ||
                    step.chainWindowN.x < 0f || step.chainWindowN.y > 1f || step.chainWindowN.x > step.chainWindowN.y)
                    StepError("combo.window", "Step has an invalid Chain Window.");
                var child = step.executionSkill;
                if (child == null || child.IsCombo)
                {
                    StepError("combo.execution", "Step needs a non-combo Skill with animation and payload.");
                    continue; // No recursion through invalid nested/cyclic combos.
                }
                child.CollectDefinitionIssues(issues, SkillExecutionKind.BasicMelee, includeAuthoring, childLocation, step.EntryId);
            }
        }
        else
        {
            if (kind == SkillExecutionKind.BasicMelee && (skillClip == null || !skillClip.IsValid))
                Add("execution.animation", "Basic Melee needs a Skill with animation and payload.");
            if (!TryGetExecution(out var execution))
                Add("execution.payload", "Execution payload is required.");
            else
            {
                CollectPayloadIssues(execution, issues, location, stepId);
                CollectTimelineValidationIssues(kind, messages);
                foreach (string message in messages) Add("execution.timeline", message);
            }
            messages.Clear();
            CollectSkillVfxValidationIssues(messages);
            foreach (string message in messages) Add("execution.vfx", message);
        }

        if (!includeAuthoring)
            return;
        messages.Clear();
        if (!IsCombo) CollectAuthoringWarnings(messages);
        foreach (string message in messages) Add("skill.authoring", message, SkillValidationSeverity.Warning);
#if UNITY_EDITOR
        CollectOwnershipIssues(issues, location, stepId);
#endif
    }

    private void CollectPayloadIssues(SkillPayloadDef execution, List<SkillValidationIssue> issues,
        string location, string comboStepId)
    {
        var messages = new List<string>();
        void AddPayload(SkillPayloadDef source, string payloadLocation, string gate)
        {
            messages.Clear();
            source.CollectValidationIssues(messages);
            foreach (string message in messages)
                issues.Add(new SkillValidationIssue("execution.payload", SkillValidationSeverity.Error,
                    this, payloadLocation, comboStepId, message, source, gate));
        }
        if (execution is not CompositeSkillPayloadDef composite)
        {
            AddPayload(execution, location, null);
            return;
        }
        composite.CollectStructureValidationIssues(messages);
        foreach (string message in messages)
            issues.Add(new SkillValidationIssue("execution.structure", SkillValidationSeverity.Error,
                this, location, comboStepId, message, composite));
        var visited = new HashSet<SkillPayloadDef>();
        for (int i = 0; i < composite.Steps.Count; i++)
        {
            if (composite.Steps[i] is not PayloadStep step || step.Payload == null ||
                step.Payload is CompositeSkillPayloadDef || !visited.Add(step.Payload)) continue;
            string childLocation = string.IsNullOrEmpty(location)
                ? $"Payload step {i + 1}" : $"{location} / Payload step {i + 1}";
            AddPayload(step.Payload, childLocation, step.RequiredUpgradeId);
        }
    }

    public bool ValidateExecution(SkillExecutionKind kind, out string reason)
    {
        if (!TryGetExecution(out _))
        {
            reason = IsCombo ? "A Combo must execute through its steps." : "Execution payload is required.";
            return false;
        }
        var issues = new List<SkillValidationIssue>();
        CollectValidationIssues(issues, kind);
        return TryGetValidationError(issues, out reason);
    }

    private static bool TryGetValidationError(List<SkillValidationIssue> issues, out string reason)
    {
        var errors = new List<string>();
        foreach (var issue in issues)
            if (issue.Severity == SkillValidationSeverity.Error)
                errors.Add(issue.ToString());
        reason = errors.Count == 0 ? null : string.Join("; ", errors);
        return errors.Count == 0;
    }

    private string GetValidationSummary(SkillValidationSeverity severity)
    {
        var issues = new List<SkillValidationIssue>();
        CollectValidationIssues(issues, ValidationExecutionKind, includeAuthoring: true);
        var messages = new List<string>();
        foreach (var issue in issues)
            if (issue.Severity == severity) messages.Add(issue.ToString());
        return string.Join("\n", messages);
    }

#if UNITY_EDITOR
    private void CollectOwnershipIssues(List<SkillValidationIssue> issues, string location, string stepId)
    {
        string path = UnityEditor.AssetDatabase.GetAssetPath(this);
        // In-memory definitions have no asset ownership yet (runtime-created skills and test rigs).
        if (string.IsNullOrEmpty(path)) return;
        void Add(string message) => issues.Add(new SkillValidationIssue("asset.ownership",
            SkillValidationSeverity.Error, this, location, stepId, message));
        var reachable = new HashSet<SkillPayloadDef>();
        CollectReferencedPayloads(IsCombo ? null : payload, reachable);
        foreach (var referenced in reachable)
            if (!UnityEditor.AssetDatabase.IsSubAsset(referenced) ||
                !string.Equals(path, UnityEditor.AssetDatabase.GetAssetPath(referenced), StringComparison.OrdinalIgnoreCase))
                Add($"Referenced payload '{referenced.name}' is not embedded in the same asset as this skill.");
        foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is SkillPayloadDef embedded && !reachable.Contains(embedded))
                Add($"Embedded payload '{embedded.name}' is not referenced by the root payload or any composite step.");
    }
#endif
}
