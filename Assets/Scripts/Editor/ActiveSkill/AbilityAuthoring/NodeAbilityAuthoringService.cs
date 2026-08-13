using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Atomic asset mutation service for node-centric ability authoring (plan section 12). Every
// operation here is one Undo group and either fully succeeds or rolls back to the group's start
// with no orphaned sub-assets. Callers (wizard, node cards -- Phase 4/5) own Save/Discard; this
// service never calls AssetDatabase.SaveAssets.
internal static class NodeAbilityAuthoringService
{
    public readonly struct AbilityResult
    {
        public AbilityResult(bool success, CompositeSkillPayloadDef composite, PayloadStep step, SkillPayloadDef payload, string abilityId)
        {
            Success = success;
            Composite = composite;
            Step = step;
            Payload = payload;
            AbilityId = abilityId;
        }

        public static AbilityResult Failed => default;

        public bool Success { get; }
        public CompositeSkillPayloadDef Composite { get; }
        public PayloadStep Step { get; }
        public SkillPayloadDef Payload { get; }
        public string AbilityId { get; }
    }

    // ---- Preflight ---------------------------------------------------------------------------

    // Node-centric mutation is blocked unless the tree resolves to exactly one skill owner (plan
    // section 3 decision 23, risk register "shared tree ownership is ambiguous").
    public static bool TryResolveSingleOwner(
        SkillUpgradeTreeDefinition tree,
        out SkillGemDefinition owner,
        List<PayloadAuthoringIssue> issues)
    {
        return TryResolveSingleOwner(
            SkillUpgradeTreeValidator.FindOwningSkills(tree),
            out owner,
            issues);
    }

    // Editor windows that already cache owner discovery should use this overload. Calling the
    // tree overload from an IMGUI repaint path would scan the AssetDatabase every frame while the
    // graph is panned or a node is dragged.
    internal static bool TryResolveSingleOwner(
        IReadOnlyList<SkillGemDefinition> owners,
        out SkillGemDefinition owner,
        List<PayloadAuthoringIssue> issues)
    {
        owner = null;
        owners ??= Array.Empty<SkillGemDefinition>();
        if (owners.Count == 0)
        {
            issues?.Add(PayloadAuthoringIssue.Error(
                "Tree has no owning SkillGemDefinition. Node-centric ability authoring requires exactly one owner."));
            return false;
        }

        if (owners.Count > 1)
        {
            string names = string.Join(", ", owners.Select(o => o != null ? o.name : "<null>"));
            issues?.Add(PayloadAuthoringIssue.Error(
                $"Tree is shared by {owners.Count} skills ({names}). Node-centric ability authoring is blocked " +
                "until ownership is resolved."));
            return false;
        }

        owner = owners[0];
        return owner != null;
    }

    // ---- Convert single payload to composite, preserving execution (plan section 12.1) -------

    public static bool ConvertToCompositePreservingExecution(
        SkillGemDefinition skill,
        out CompositeSkillPayloadDef composite,
        List<PayloadAuthoringIssue> issues)
    {
        composite = skill?.payload as CompositeSkillPayloadDef;
        if (composite != null)
            return true;

        issues ??= new List<PayloadAuthoringIssue>();

        if (skill == null)
        {
            issues.Add(PayloadAuthoringIssue.Error("Skill is null."));
            return false;
        }

        string skillPath = AssetDatabase.GetAssetPath(skill);
        if (string.IsNullOrEmpty(skillPath))
        {
            issues.Add(PayloadAuthoringIssue.Error("Save the skill asset before adding abilities."));
            return false;
        }

        SkillPayloadDef existingRoot = skill.payload;
        if (existingRoot != null && !SkillPayloadAssetUtility.IsEmbedded(skill, existingRoot))
        {
            issues.Add(PayloadAuthoringIssue.Error(
                "The current execution payload is not embedded in this skill asset -- fix that before converting to composite."));
            return false;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Convert Skill Execution To Composite");
        int group = Undo.GetCurrentGroup();
        try
        {
            var newComposite = ScriptableObject.CreateInstance<CompositeSkillPayloadDef>();
            newComposite.name = "Composite Execution";
            newComposite.hideFlags = HideFlags.None;
            Undo.RegisterCreatedObjectUndo(newComposite, "Create Composite Payload");
            AssetDatabase.AddObjectToAsset(newComposite, skill);

            if (existingRoot != null)
            {
                CopyRootOwnedExecutionFields(existingRoot, newComposite);

                Undo.RegisterCompleteObjectUndo(existingRoot, "Reset Child Payload Execution Fields");
                ResetChildExecutionFieldsToCompositeDefaults(existingRoot);

                var alwaysActiveStep = new PayloadStep();
                alwaysActiveStep.SetPayload(existingRoot);
                newComposite.AddStep(alwaysActiveStep);
            }

            Undo.RegisterCompleteObjectUndo(skill, "Assign Composite Payload");
            skill.payload = newComposite;

            EditorUtility.SetDirty(newComposite);
            if (existingRoot != null)
                EditorUtility.SetDirty(existingRoot);
            EditorUtility.SetDirty(skill);

            Undo.CollapseUndoOperations(group);
            composite = newComposite;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Undo.RevertAllDownToGroup(group);
            issues.Add(PayloadAuthoringIssue.Error($"Conversion to composite failed and was rolled back: {e.Message}"));
            composite = skill.payload as CompositeSkillPayloadDef;
            return false;
        }
    }

    // ---- Create (plan section 12.2) -----------------------------------------------------------

    // configuredDraft is an already-validated, already-configured transient instance (the wizard
    // owns building/editing/destroying it -- plan section 13.2). This call embeds a fresh copy of
    // its serialized data as the new step's payload; the caller's draft is never embedded as-is.
    public static AbilityResult CreateNodeAbility(
        SkillUpgradeTreeDefinition tree,
        SkillGemDefinition skill,
        SkillUpgradeNodeData node,
        SkillPayloadDef configuredDraft,
        List<PayloadAuthoringIssue> issues)
    {
        issues ??= new List<PayloadAuthoringIssue>();

        if (tree == null || skill == null || node == null || configuredDraft == null)
        {
            issues.Add(PayloadAuthoringIssue.Error(
                "Create requires a tree, an owning skill, a target node, and a configured draft."));
            return AbilityResult.Failed;
        }

        if (!PayloadDesignerDescriptorRegistry.TryGetDescriptor(configuredDraft.GetType(), out IPayloadDesignerDescriptor descriptor))
        {
            issues.Add(PayloadAuthoringIssue.Error(
                $"'{configuredDraft.GetType().Name}' has no registered designer descriptor and cannot be created through this flow."));
            return AbilityResult.Failed;
        }

        var context = new PayloadDesignerContext(tree, skill, node);
        descriptor.CollectAuthoringIssues(configuredDraft, context, issues);
        if (issues.HasErrors())
            return AbilityResult.Failed;

        string skillPath = AssetDatabase.GetAssetPath(skill);
        if (string.IsNullOrEmpty(skillPath))
        {
            issues.Add(PayloadAuthoringIssue.Error("Save the skill asset before adding abilities."));
            return AbilityResult.Failed;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Add Node Ability");
        int group = Undo.GetCurrentGroup();
        try
        {
            if (!ConvertToCompositePreservingExecution(skill, out CompositeSkillPayloadDef composite, issues))
            {
                Undo.RevertAllDownToGroup(group);
                return AbilityResult.Failed;
            }

            string abilityId = GenerateAbilityId(tree, skill, node, configuredDraft.GetType());

            var embeddedPayload = (SkillPayloadDef)ScriptableObject.CreateInstance(configuredDraft.GetType());
            EditorUtility.CopySerialized(configuredDraft, embeddedPayload);
            ResetChildExecutionFieldsToCompositeDefaults(embeddedPayload);
            embeddedPayload.name = $"{descriptor.DisplayName} Execution";
            embeddedPayload.hideFlags = HideFlags.None;
            Undo.RegisterCreatedObjectUndo(embeddedPayload, "Create Ability Payload");
            AssetDatabase.AddObjectToAsset(embeddedPayload, skill);

            var step = new PayloadStep();
            step.SetPayload(embeddedPayload);
            step.RequiredUpgradeId = abilityId;

            Undo.RegisterCompleteObjectUndo(composite, "Add Ability Step");
            composite.AddStep(step);

            BindAbilityIdToNode(tree, node, abilityId);

            EditorUtility.SetDirty(embeddedPayload);
            EditorUtility.SetDirty(composite);
            EditorUtility.SetDirty(skill);
            EditorUtility.SetDirty(tree);

            Undo.CollapseUndoOperations(group);
            return new AbilityResult(true, composite, step, embeddedPayload, abilityId);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Undo.RevertAllDownToGroup(group);
            issues.Add(PayloadAuthoringIssue.Error($"Create Ability failed and was rolled back: {e.Message}"));
            return AbilityResult.Failed;
        }
    }

    // ---- Edit (plan section 12.3) -------------------------------------------------------------

    // The wizard owns steps 1-3 (copy real payload into a transient draft, edit it, validate it).
    // This call is step 4: commit the draft's serialized values back onto the real payload as one
    // Undo step. Step 5 (cancel) needs no service call -- the wizard just destroys its draft.
    public static bool ApplyEditedAbility(
        SkillUpgradeTreeDefinition tree,
        SkillGemDefinition skill,
        SkillUpgradeNodeData node,
        SkillPayloadDef realPayload,
        SkillPayloadDef configuredDraft,
        List<PayloadAuthoringIssue> issues)
    {
        issues ??= new List<PayloadAuthoringIssue>();

        if (realPayload == null || configuredDraft == null)
        {
            issues.Add(PayloadAuthoringIssue.Error("Edit requires both the real payload and a configured draft."));
            return false;
        }

        if (realPayload.GetType() != configuredDraft.GetType())
        {
            issues.Add(PayloadAuthoringIssue.Error("Draft type does not match the payload being edited."));
            return false;
        }

        if (!PayloadDesignerDescriptorRegistry.TryGetDescriptor(realPayload.GetType(), out IPayloadDesignerDescriptor descriptor))
        {
            issues.Add(PayloadAuthoringIssue.Error($"'{realPayload.GetType().Name}' has no registered designer descriptor."));
            return false;
        }

        var context = new PayloadDesignerContext(tree, skill, node);
        descriptor.CollectAuthoringIssues(configuredDraft, context, issues);
        if (issues.HasErrors())
            return false;

        Undo.RegisterCompleteObjectUndo(realPayload, "Edit Ability");
        EditorUtility.CopySerialized(configuredDraft, realPayload);
        ResetChildExecutionFieldsToCompositeDefaults(realPayload);
        EditorUtility.SetDirty(realPayload);
        return true;
    }

    // ---- Duplicate (plan section 12.4) --------------------------------------------------------

    public static AbilityResult DuplicateNodeAbility(
        SkillUpgradeTreeDefinition tree,
        SkillGemDefinition skill,
        SkillUpgradeNodeData node,
        PayloadStep sourceStep,
        List<PayloadAuthoringIssue> issues)
    {
        issues ??= new List<PayloadAuthoringIssue>();

        if (tree == null || skill == null || node == null || sourceStep?.Payload == null)
        {
            issues.Add(PayloadAuthoringIssue.Error(
                "Duplicate requires a tree, an owning skill, a target node, and a source ability step with a payload."));
            return AbilityResult.Failed;
        }

        if (skill.payload is not CompositeSkillPayloadDef composite || composite.IndexOfStep(sourceStep) < 0)
        {
            issues.Add(PayloadAuthoringIssue.Error("Source step is not owned by this skill's composite payload."));
            return AbilityResult.Failed;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Duplicate Node Ability");
        int group = Undo.GetCurrentGroup();
        try
        {
            SkillPayloadDef sourcePayload = sourceStep.Payload;
            var duplicatePayload = (SkillPayloadDef)ScriptableObject.CreateInstance(sourcePayload.GetType());
            EditorUtility.CopySerialized(sourcePayload, duplicatePayload);
            duplicatePayload.name = sourcePayload.name;
            duplicatePayload.hideFlags = HideFlags.None;
            Undo.RegisterCreatedObjectUndo(duplicatePayload, "Create Duplicated Payload");
            AssetDatabase.AddObjectToAsset(duplicatePayload, skill);

            string abilityId = GenerateAbilityId(tree, skill, node, duplicatePayload.GetType());

            var newStep = new PayloadStep();
            newStep.SetPayload(duplicatePayload);
            newStep.RequiredUpgradeId = abilityId;

            Undo.RegisterCompleteObjectUndo(composite, "Add Duplicated Ability Step");
            composite.AddStep(newStep);

            BindAbilityIdToNode(tree, node, abilityId);

            EditorUtility.SetDirty(duplicatePayload);
            EditorUtility.SetDirty(composite);
            EditorUtility.SetDirty(skill);
            EditorUtility.SetDirty(tree);

            Undo.CollapseUndoOperations(group);
            return new AbilityResult(true, composite, newStep, duplicatePayload, abilityId);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Undo.RevertAllDownToGroup(group);
            issues.Add(PayloadAuthoringIssue.Error($"Duplicate Ability failed and was rolled back: {e.Message}"));
            return AbilityResult.Failed;
        }
    }

    // ---- Remove (plan section 12.5) -----------------------------------------------------------

    public static bool RemoveNodeAbility(
        SkillUpgradeTreeDefinition tree,
        SkillGemDefinition skill,
        SkillUpgradeNodeData node,
        PayloadStep step,
        List<PayloadAuthoringIssue> issues)
    {
        issues ??= new List<PayloadAuthoringIssue>();

        if (tree == null || skill == null || node == null || step == null)
        {
            issues.Add(PayloadAuthoringIssue.Error("Remove requires a tree, an owning skill, a target node, and an ability step."));
            return false;
        }

        if (skill.payload is not CompositeSkillPayloadDef composite || composite.IndexOfStep(step) < 0)
        {
            issues.Add(PayloadAuthoringIssue.Error("Step is not owned by this skill's composite payload."));
            return false;
        }

        string abilityId = step.RequiredUpgradeId;
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            issues.Add(PayloadAuthoringIssue.Error(
                "Step has no required upgrade id -- it is not a node-owned ability and cannot be removed through " +
                "this flow (it may be the always-active step)."));
            return false;
        }

        if (node.grantedUpgradeIds == null || !node.grantedUpgradeIds.Contains(abilityId))
        {
            issues.Add(PayloadAuthoringIssue.Error(
                $"Node '{node.RuntimeNodeId}' does not grant '{abilityId}' -- refusing to remove a binding this node does not own."));
            return false;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Remove Node Ability");
        int group = Undo.GetCurrentGroup();
        try
        {
            SkillPayloadDef payload = step.Payload;

            Undo.RegisterCompleteObjectUndo(composite, "Remove Ability Step");
            composite.RemoveStep(step);

            // Reference-safe: never destroy a payload another step still owns, and never revoke a
            // node's grant while any remaining step still gates on it. A full cross-tree/cross-skill
            // reference scan belongs to Phase 6 unified validation; this guard covers the structural
            // invariant this composite itself must uphold.
            if (payload != null && !SkillPayloadAssetUtility.IsReferencedByComposite(composite, payload))
                Undo.DestroyObjectImmediate(payload);

            Undo.RegisterCompleteObjectUndo(tree, "Revoke Ability Upgrade Id");
            if (!IsIdRequiredByAnyRemainingStep(composite, abilityId))
                node.grantedUpgradeIds.Remove(abilityId);

            EditorUtility.SetDirty(composite);
            EditorUtility.SetDirty(skill);
            EditorUtility.SetDirty(tree);

            Undo.CollapseUndoOperations(group);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Undo.RevertAllDownToGroup(group);
            issues.Add(PayloadAuthoringIssue.Error($"Remove Ability failed and was rolled back: {e.Message}"));
            return false;
        }
    }

    // ---- Shared helpers -----------------------------------------------------------------------

    public static string GenerateAbilityId(
        SkillUpgradeTreeDefinition tree,
        SkillGemDefinition skill,
        SkillUpgradeNodeData node,
        Type payloadType)
    {
        HashSet<string> known = AbilityBindingIdGenerator.CollectKnownIds(tree, skill);
        string slug = AbilityBindingIdGenerator.Normalize(SkillPayloadAssetUtility.GetPayloadDisplayName(payloadType));
        return AbilityBindingIdGenerator.GenerateId(skill.SkillDefinitionId, node.RuntimeNodeId, slug, known.Contains);
    }

    static void BindAbilityIdToNode(SkillUpgradeTreeDefinition tree, SkillUpgradeNodeData node, string abilityId)
    {
        Undo.RegisterCompleteObjectUndo(tree, "Grant Ability Upgrade Id");
        node.grantedUpgradeIds ??= new List<string>();
        if (!node.grantedUpgradeIds.Contains(abilityId))
            node.grantedUpgradeIds.Add(abilityId);
    }

    static bool IsIdRequiredByAnyRemainingStep(CompositeSkillPayloadDef composite, string abilityId)
    {
        IReadOnlyList<SkillEffectStep> steps = composite.Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            if (string.Equals(steps[i]?.RequiredUpgradeId, abilityId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    // The three root-owned execution fields (helperFacingMode, chainContinueMode,
    // chainContinueNormalizedTime) are private on SkillPayloadDef -- copy/reset them through
    // SerializedObject rather than adding a runtime setter API (plan section 12.1).
    static void CopyRootOwnedExecutionFields(SkillPayloadDef source, SkillPayloadDef destination)
    {
        var sourceSerialized = new SerializedObject(source);
        var destinationSerialized = new SerializedObject(destination);

        CopyScalarProperty(sourceSerialized, destinationSerialized, "helperFacingMode");
        CopyScalarProperty(sourceSerialized, destinationSerialized, "chainContinueMode");
        CopyScalarProperty(sourceSerialized, destinationSerialized, "chainContinueNormalizedTime");

        destinationSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ResetChildExecutionFieldsToCompositeDefaults(SkillPayloadDef child)
    {
        var serialized = new SerializedObject(child);
        SerializedProperty helperFacingMode = serialized.FindProperty("helperFacingMode");
        SerializedProperty chainContinueMode = serialized.FindProperty("chainContinueMode");
        SerializedProperty chainContinueNormalizedTime = serialized.FindProperty("chainContinueNormalizedTime");

        if (helperFacingMode != null)
            helperFacingMode.enumValueIndex = 0;
        if (chainContinueMode != null)
            chainContinueMode.enumValueIndex = 0;
        if (chainContinueNormalizedTime != null)
            chainContinueNormalizedTime.floatValue = 1f;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CopyScalarProperty(SerializedObject source, SerializedObject destination, string propertyName)
    {
        SerializedProperty sourceProperty = source.FindProperty(propertyName);
        SerializedProperty destinationProperty = destination.FindProperty(propertyName);
        if (sourceProperty == null || destinationProperty == null)
            return;

        switch (sourceProperty.propertyType)
        {
            case SerializedPropertyType.Enum:
                destinationProperty.enumValueIndex = sourceProperty.enumValueIndex;
                break;
            case SerializedPropertyType.Float:
                destinationProperty.floatValue = sourceProperty.floatValue;
                break;
        }
    }
}
