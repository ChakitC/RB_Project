using System;
using System.Collections.Generic;

// Unified validation for node-centric ability authoring (plan section 15). Bridges the 3-level
// PayloadAuthoringIssue model (descriptor registry diagnostics + per-payload authoring issues)
// into the existing SkillUpgradeValidationIssue/Severity model that ActiveSkillTreeEditorWindow,
// SkillUpgradeTreeValidator, and the graph's issue badges already share -- so Save, node badges,
// and the inspector's issue list all see one consolidated list instead of two disconnected ones.
//
// Ability-binding invariants already enforced by SkillUpgradeTreeValidator.Validate (duplicate
// grants, an id nothing declares, a declared id no node grants, blank granted ids) are not
// repeated here. This file only adds what that validator cannot see: per-payload authoring
// issues (a Taunt payload with no Taunt Status assigned, etc.) and descriptor registry health.
//
// PayloadAuthoringSeverity has three levels (Info guides without blocking) but
// SkillUpgradeValidationSeverity has two -- Warning and Info both map to Warning here, which is
// the safe direction: Info issues become "requires confirmation" instead of silently invisible,
// never the reverse.
internal static class NodeCentricPayloadValidator
{
    public static List<SkillUpgradeValidationIssue> Validate(
        SkillUpgradeTreeDefinition tree, IReadOnlyList<SkillDefinitionBase> owners)
    {
        var issues = new List<SkillUpgradeValidationIssue>();
        if (tree == null)
            return issues;

        AddRegistryDiagnostics(issues);
        AddPayloadAuthoringIssues(tree, owners, issues);
        AddNonNormalizedIdWarnings(tree, issues);

        return issues;
    }

    static void AddRegistryDiagnostics(List<SkillUpgradeValidationIssue> issues)
    {
        IReadOnlyList<PayloadAuthoringIssue> diagnostics = PayloadDesignerDescriptorRegistry.GetDiagnostics();
        for (int i = 0; i < diagnostics.Count; i++)
            issues.Add(ToTreeIssue(diagnostics[i], null));
    }

    static void AddPayloadAuthoringIssues(
        SkillUpgradeTreeDefinition tree,
        IReadOnlyList<SkillDefinitionBase> owners,
        List<SkillUpgradeValidationIssue> issues)
    {
        if (owners == null)
            return;

        for (int ownerIndex = 0; ownerIndex < owners.Count; ownerIndex++)
        {
            if (owners[ownerIndex] is not SkillGemDefinition skill)
                continue;

            if (skill.payload is not CompositeSkillPayloadDef composite)
                continue;

            IReadOnlyList<SkillEffectStep> steps = composite.Steps;
            for (int stepIndex = 0; stepIndex < steps.Count; stepIndex++)
            {
                SkillEffectStep rawStep = steps[stepIndex];

                // Plan section 8.2 invariant 11 / section 16.7: after migration and legacy
                // retirement, a direct gameplay step (orchestration + behavior fused in one type,
                // the way HealAreaStep used to be) is never valid again. PayloadStep is the only
                // supported orchestration type going forward.
                if (rawStep is not PayloadStep payloadStep)
                {
                    string gate = rawStep?.RequiredUpgradeId?.Trim();
                    string ownerNodeId = !string.IsNullOrEmpty(gate) ? FindGrantingNodeId(tree, gate) : null;
                    issues.Add(new SkillUpgradeValidationIssue(
                        SkillUpgradeValidationSeverity.Error,
                        $"Composite step {stepIndex} on '{skill.name}' is a direct gameplay step " +
                        $"('{rawStep?.GetType().Name ?? "null"}'), not a PayloadStep. Direct gameplay steps are " +
                        "no longer supported -- migrate its behavior into a SkillPayloadDef.",
                        ownerNodeId));
                    continue;
                }

                if (payloadStep.Payload == null)
                    continue;

                string abilityId = payloadStep.RequiredUpgradeId?.Trim();
                bool isNodeOwned = !string.IsNullOrEmpty(abilityId);

                if (!PayloadDesignerDescriptorRegistry.TryGetDescriptor(payloadStep.Payload.GetType(), out IPayloadDesignerDescriptor descriptor))
                {
                    // A payload type with no descriptor cannot have been authored through the
                    // normal wizard (plan section 3 decision 11). It is still reported here so a
                    // stray/manually-authored one does not silently escape Save.
                    if (isNodeOwned)
                    {
                        string nodeId = FindGrantingNodeId(tree, abilityId);
                        issues.Add(new SkillUpgradeValidationIssue(
                            SkillUpgradeValidationSeverity.Error,
                            $"Ability '{abilityId}' uses payload type '{payloadStep.Payload.GetType().Name}', which has no " +
                            "registered designer descriptor.",
                            nodeId));
                    }

                    continue;
                }

                if (!isNodeOwned)
                    continue; // Always-active step: not a node-owned ability, nothing to attribute.

                string grantingNodeId = FindGrantingNodeId(tree, abilityId);
                var context = new PayloadDesignerContext(
                    tree,
                    skill,
                    grantingNodeId != null ? FindNode(tree, grantingNodeId) : null);

                var payloadIssues = new List<PayloadAuthoringIssue>();
                descriptor.CollectAuthoringIssues(payloadStep.Payload, context, payloadIssues);

                for (int i = 0; i < payloadIssues.Count; i++)
                    issues.Add(ToTreeIssue(payloadIssues[i], grantingNodeId));
            }
        }
    }

    // Soft consistency check: a hand-edited or pre-generator id (e.g. migrated from an older
    // convention) still works at runtime, so this is a Warning, not an Error.
    static void AddNonNormalizedIdWarnings(SkillUpgradeTreeDefinition tree, List<SkillUpgradeValidationIssue> issues)
    {
        if (tree?.nodes == null)
            return;

        for (int i = 0; i < tree.nodes.Count; i++)
        {
            SkillUpgradeNodeData node = tree.nodes[i];
            if (node?.grantedUpgradeIds == null)
                continue;

            string nodeId = node.RuntimeNodeId;
            for (int j = 0; j < node.grantedUpgradeIds.Count; j++)
            {
                string rawId = node.grantedUpgradeIds[j];
                if (string.IsNullOrWhiteSpace(rawId))
                    continue;

                string trimmed = rawId.Trim();
                if (!string.Equals(trimmed, AbilityBindingIdGenerator.Normalize(trimmed), StringComparison.Ordinal))
                {
                    issues.Add(new SkillUpgradeValidationIssue(
                        SkillUpgradeValidationSeverity.Warning,
                        $"Node '{nodeId}' grants '{trimmed}', which is not in the normalized " +
                        "lowercase-dot-separated id convention.",
                        nodeId));
                }
            }
        }
    }

    static string FindGrantingNodeId(SkillUpgradeTreeDefinition tree, string abilityId)
    {
        if (tree?.nodes == null || string.IsNullOrEmpty(abilityId))
            return null;

        for (int i = 0; i < tree.nodes.Count; i++)
        {
            SkillUpgradeNodeData node = tree.nodes[i];
            if (node?.grantedUpgradeIds == null)
                continue;

            for (int j = 0; j < node.grantedUpgradeIds.Count; j++)
            {
                if (string.Equals(node.grantedUpgradeIds[j]?.Trim(), abilityId, StringComparison.Ordinal))
                    return node.RuntimeNodeId;
            }
        }

        return null;
    }

    static SkillUpgradeNodeData FindNode(SkillUpgradeTreeDefinition tree, string nodeId)
    {
        return tree != null && tree.TryGetNode(nodeId, out SkillUpgradeNodeData node) ? node : null;
    }

    static SkillUpgradeValidationIssue ToTreeIssue(PayloadAuthoringIssue issue, string nodeId)
    {
        SkillUpgradeValidationSeverity severity = issue.Severity == PayloadAuthoringSeverity.Error
            ? SkillUpgradeValidationSeverity.Error
            : SkillUpgradeValidationSeverity.Warning;

        return new SkillUpgradeValidationIssue(severity, issue.Message, nodeId);
    }
}
