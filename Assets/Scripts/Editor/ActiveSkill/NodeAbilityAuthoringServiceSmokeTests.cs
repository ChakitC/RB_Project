#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase 3 smoke tests for NodeAbilityAuthoringService (plan section 12). Runs on real temp
/// assets on disk -- AssetDatabase.AddObjectToAsset and Undo grouping only behave correctly
/// against a saved asset, same reasoning as ActiveSkillStatusEffectAuthoringSmokeTests.
/// </summary>
public static class NodeAbilityAuthoringServiceSmokeTests
{
    const string TempFolder = "Assets/_NodeAbilityAuthoringSmokeTests";

    [MenuItem("Tools/RB/Skills/Run Node Ability Authoring Smoke Tests")]
    public static void RunFromMenu() => RunFromCommandLine();

    public static void RunFromCommandLine()
    {
        Fixture fixture = null;
        try
        {
            fixture = Fixture.Create();

            TestConvertPreservesRootPayloadObjectAndValues(fixture);
            TestConvertTransfersRootOwnedFieldsAndResetsChild(fixture);
            TestCreateOnSinglePayloadSkillAutoConvertsAndAddsStep(fixture);
            TestCreateBindsTheSameIdToNodeAndStep(fixture);
            TestTwoAbilitiesOfTheSameTypeOnOneNodeGetDistinctIds(fixture);
            TestCreateWithInvalidDraftLeavesNoSideEffects(fixture);
            TestEditCommitsDraftValuesOntoTheRealPayloadInPlace(fixture);
            TestDuplicateCreatesAUniqueObjectAndUniqueId(fixture);
            TestRemoveDeletesStepPayloadAndRevokesId(fixture);
            TestRemoveRefusesAStepWithNoRequiredUpgradeId(fixture);
            TestRemoveRefusesAnIdTheNodeDoesNotGrant(fixture);
            TestUndoRestoresEverythingAfterCreate(fixture);

            Debug.Log("[NodeAbilityAuthoringServiceTests] All node ability authoring smoke tests passed.");
        }
        finally
        {
            fixture?.Dispose();
        }
    }

    static void TestConvertPreservesRootPayloadObjectAndValues(Fixture fixture)
    {
        SkillGemDefinition skill = fixture.CreateSkillWithRootPayload<TauntSkillPayloadDef>("ConvertSkill", out TauntSkillPayloadDef originalRoot);
        originalRoot.GetType().GetField("radius",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(originalRoot, 42f);
        EditorUtility.SetDirty(originalRoot);
        AssetDatabase.SaveAssetIfDirty(skill);

        var issues = new List<PayloadAuthoringIssue>();
        bool success = NodeAbilityAuthoringService.ConvertToCompositePreservingExecution(skill, out CompositeSkillPayloadDef composite, issues);

        Expect(success, $"Convert failed: {Describe(issues)}");
        Expect(composite != null, "Convert did not produce a composite.");
        Equal(1, composite.Steps.Count, "Converted composite should own exactly one always-active step.");
        Expect(composite.Steps[0] is PayloadStep, "The preserved step must be a PayloadStep.");

        var preservedStep = (PayloadStep)composite.Steps[0];
        Expect(ReferenceEquals(preservedStep.Payload, originalRoot),
            "Convert must reuse the original root payload object, not clone it.");
        Equal(42f, ((TauntSkillPayloadDef)preservedStep.Payload).Radius, "Convert must preserve the root payload's serialized values.");
        Expect(string.IsNullOrEmpty(preservedStep.RequiredUpgradeId), "The always-active step must keep a blank gate.");
    }

    static void TestConvertTransfersRootOwnedFieldsAndResetsChild(Fixture fixture)
    {
        SkillGemDefinition skill = fixture.CreateSkillWithRootPayload<TauntSkillPayloadDef>("ConvertFieldsSkill", out TauntSkillPayloadDef originalRoot);
        var rootSerialized = new SerializedObject(originalRoot);
        rootSerialized.FindProperty("helperFacingMode").enumValueIndex = (int)SkillHelperFacingMode.FaceDetectedTargetOnCast;
        rootSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(originalRoot);
        AssetDatabase.SaveAssetIfDirty(skill);

        var issues = new List<PayloadAuthoringIssue>();
        NodeAbilityAuthoringService.ConvertToCompositePreservingExecution(skill, out CompositeSkillPayloadDef composite, issues);

        Equal(SkillHelperFacingMode.FaceDetectedTargetOnCast, composite.HelperFacingMode,
            "Root-owned helperFacingMode must transfer to the new composite.");

        var childPayload = (PayloadStep)composite.Steps[0];
        Equal(SkillHelperFacingMode.KeepCurrentFacing, childPayload.Payload.HelperFacingMode,
            "The child payload's helperFacingMode must reset to the composite-child default.");
    }

    static void TestCreateOnSinglePayloadSkillAutoConvertsAndAddsStep(Fixture fixture)
    {
        SkillGemDefinition skill = fixture.CreateSkillWithRootPayload<TauntSkillPayloadDef>("AutoConvertSkill", out _);
        SkillUpgradeNodeData node = fixture.AddNode("auto_convert_node");

        var draft = ScriptableObject.CreateInstance<ApplyStatusSkillPayloadDef>();
        draft.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            AddValidStatusApplication(draft, fixture.SomeStatus);

            var issues = new List<PayloadAuthoringIssue>();
            NodeAbilityAuthoringService.AbilityResult result =
                NodeAbilityAuthoringService.CreateNodeAbility(fixture.Tree, skill, node, draft, issues);

            Expect(result.Success, $"Create failed on a single-payload skill: {Describe(issues)}");
            Expect(skill.payload is CompositeSkillPayloadDef, "Skill must have been auto-converted to composite.");
            var composite = (CompositeSkillPayloadDef)skill.payload;
            Equal(2, composite.Steps.Count, "Expected the preserved always-active step plus the new node ability step.");
            Expect(!ReferenceEquals(result.Payload, draft), "The embedded payload must be a fresh copy, never the caller's draft instance.");

            // AssetDatabase.IsSubAsset (which IsEmbedded relies on) only reflects a freshly
            // AddObjectToAsset'd sub-asset once the asset has been saved -- the service itself
            // must not save (window owns Save/Discard), so the test does it here to observe the
            // same on-disk state the tree window would see after the designer clicks Save.
            AssetDatabase.SaveAssetIfDirty(skill);
            Expect(SkillPayloadAssetUtility.IsEmbedded(skill, result.Payload), "The new ability payload must be embedded in the skill asset.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(draft);
        }
    }

    static void TestCreateBindsTheSameIdToNodeAndStep(Fixture fixture)
    {
        SkillGemDefinition skill = fixture.CreateSkillWithRootPayload<TauntSkillPayloadDef>("BindSkill", out _);
        SkillUpgradeNodeData node = fixture.AddNode("bind_node");

        var draft = ScriptableObject.CreateInstance<ApplyStatusSkillPayloadDef>();
        draft.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            AddValidStatusApplication(draft, fixture.SomeStatus);

            var issues = new List<PayloadAuthoringIssue>();
            NodeAbilityAuthoringService.AbilityResult result =
                NodeAbilityAuthoringService.CreateNodeAbility(fixture.Tree, skill, node, draft, issues);

            Expect(result.Success, $"Create failed: {Describe(issues)}");
            Expect(node.grantedUpgradeIds.Contains(result.AbilityId), "Node must grant the generated ability id.");
            Equal(result.AbilityId, result.Step.RequiredUpgradeId, "Step's required upgrade id must match the generated ability id.");
            Expect(result.AbilityId.StartsWith(skill.skillId + ".", StringComparison.Ordinal),
                $"Ability id '{result.AbilityId}' should be namespaced under the skill id.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(draft);
        }
    }

    static void TestTwoAbilitiesOfTheSameTypeOnOneNodeGetDistinctIds(Fixture fixture)
    {
        SkillGemDefinition skill = fixture.CreateSkillWithRootPayload<TauntSkillPayloadDef>("DistinctIdSkill", out _);
        SkillUpgradeNodeData node = fixture.AddNode("distinct_id_node");

        string firstId = CreateApplyStatusAbility(fixture, skill, node).AbilityId;
        string secondId = CreateApplyStatusAbility(fixture, skill, node).AbilityId;

        Expect(!string.Equals(firstId, secondId, StringComparison.Ordinal),
            $"Two abilities on the same node must get distinct ids, got '{firstId}' twice.");
        Equal(2, node.grantedUpgradeIds.Count, "Node must grant both distinct ability ids.");
    }

    static void TestCreateWithInvalidDraftLeavesNoSideEffects(Fixture fixture)
    {
        SkillGemDefinition skill = fixture.CreateSkillWithRootPayload<TauntSkillPayloadDef>("InvalidDraftSkill", out _);
        SkillUpgradeNodeData node = fixture.AddNode("invalid_draft_node");

        // Empty ApplyStatusSkillPayloadDef draft (no status configured) must fail descriptor
        // validation and never touch the asset.
        var draft = ScriptableObject.CreateInstance<ApplyStatusSkillPayloadDef>();
        draft.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var issues = new List<PayloadAuthoringIssue>();
            NodeAbilityAuthoringService.AbilityResult result =
                NodeAbilityAuthoringService.CreateNodeAbility(fixture.Tree, skill, node, draft, issues);

            Expect(!result.Success, "Create must fail for a draft with blocking validation errors.");
            Expect(issues.HasErrors(), "Create must report at least one Error for an invalid draft.");
            Expect(skill.payload is TauntSkillPayloadDef, "Skill must remain a single-payload skill -- no auto-conversion on a failed create.");
            Expect(node.grantedUpgradeIds == null || node.grantedUpgradeIds.Count == 0, "Node must not gain any granted id on a failed create.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(draft);
        }
    }

    static void TestEditCommitsDraftValuesOntoTheRealPayloadInPlace(Fixture fixture)
    {
        SkillGemDefinition skill = fixture.CreateSkillWithRootPayload<TauntSkillPayloadDef>("EditSkill", out _);
        SkillUpgradeNodeData node = fixture.AddNode("edit_node");
        NodeAbilityAuthoringService.AbilityResult created = CreateApplyStatusAbility(fixture, skill, node);
        var realPayload = (ApplyStatusSkillPayloadDef)created.Payload;

        var editDraft = ScriptableObject.CreateInstance<ApplyStatusSkillPayloadDef>();
        editDraft.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            EditorUtility.CopySerialized(realPayload, editDraft);
            AddValidStatusApplication(editDraft, fixture.OtherStatus);

            var issues = new List<PayloadAuthoringIssue>();
            bool success = NodeAbilityAuthoringService.ApplyEditedAbility(
                fixture.Tree, skill, node, realPayload, editDraft, issues);

            Expect(success, $"Edit failed: {Describe(issues)}");
            Equal(2, realPayload.Applications.Count, "Edit must commit the draft's added application onto the real payload.");
            Expect(((CompositeSkillPayloadDef)skill.payload).IndexOfStep(created.Step) >= 0,
                "Edit must not replace the step or its object identity.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(editDraft);
        }
    }

    static void TestDuplicateCreatesAUniqueObjectAndUniqueId(Fixture fixture)
    {
        SkillGemDefinition skill = fixture.CreateSkillWithRootPayload<TauntSkillPayloadDef>("DuplicateSkill", out _);
        SkillUpgradeNodeData node = fixture.AddNode("duplicate_node");
        NodeAbilityAuthoringService.AbilityResult original = CreateApplyStatusAbility(fixture, skill, node);

        var issues = new List<PayloadAuthoringIssue>();
        NodeAbilityAuthoringService.AbilityResult duplicate =
            NodeAbilityAuthoringService.DuplicateNodeAbility(fixture.Tree, skill, node, (PayloadStep)original.Step, issues);

        Expect(duplicate.Success, $"Duplicate failed: {Describe(issues)}");
        Expect(!ReferenceEquals(duplicate.Payload, original.Payload), "Duplicate must create a distinct payload object.");
        Expect(!string.Equals(duplicate.AbilityId, original.AbilityId, StringComparison.Ordinal), "Duplicate must get a new unique id.");
        Equal(1, ((ApplyStatusSkillPayloadDef)duplicate.Payload).Applications.Count, "Duplicate must copy the source payload's configuration.");

        var composite = (CompositeSkillPayloadDef)skill.payload;
        var ownedPayloads = new HashSet<SkillPayloadDef>();
        foreach (SkillEffectStep step in composite.Steps)
        {
            if (step is PayloadStep payloadStep && payloadStep.Payload != null)
                Expect(ownedPayloads.Add(payloadStep.Payload), "No two steps may reference the same payload object after duplication.");
        }
    }

    static void TestRemoveDeletesStepPayloadAndRevokesId(Fixture fixture)
    {
        SkillGemDefinition skill = fixture.CreateSkillWithRootPayload<TauntSkillPayloadDef>("RemoveSkill", out _);
        SkillUpgradeNodeData node = fixture.AddNode("remove_node");
        NodeAbilityAuthoringService.AbilityResult created = CreateApplyStatusAbility(fixture, skill, node);
        SkillPayloadDef payload = created.Payload;

        var issues = new List<PayloadAuthoringIssue>();
        bool success = NodeAbilityAuthoringService.RemoveNodeAbility(
            fixture.Tree, skill, node, (PayloadStep)created.Step, issues);

        Expect(success, $"Remove failed: {Describe(issues)}");
        var composite = (CompositeSkillPayloadDef)skill.payload;
        Equal(1, composite.Steps.Count, "Remove must leave only the always-active step behind.");
        Expect(payload == null || !AssetDatabase.Contains(payload), "Removed payload must no longer exist as a sub-asset.");
        Expect(!node.grantedUpgradeIds.Contains(created.AbilityId), "Remove must revoke the node's granted id.");
    }

    static void TestRemoveRefusesAStepWithNoRequiredUpgradeId(Fixture fixture)
    {
        SkillGemDefinition skill = fixture.CreateSkillWithRootPayload<TauntSkillPayloadDef>("RemoveBlankGateSkill", out _);
        SkillUpgradeNodeData node = fixture.AddNode("remove_blank_gate_node");
        NodeAbilityAuthoringService.ConvertToCompositePreservingExecution(skill, out CompositeSkillPayloadDef composite, new List<PayloadAuthoringIssue>());
        var alwaysActiveStep = (PayloadStep)composite.Steps[0];

        var issues = new List<PayloadAuthoringIssue>();
        bool success = NodeAbilityAuthoringService.RemoveNodeAbility(fixture.Tree, skill, node, alwaysActiveStep, issues);

        Expect(!success, "Remove must refuse a blank-gated always-active step.");
        Expect(issues.HasErrors(), "Refusing removal must report an Error.");
        Equal(1, composite.Steps.Count, "The always-active step must remain untouched.");
    }

    static void TestRemoveRefusesAnIdTheNodeDoesNotGrant(Fixture fixture)
    {
        SkillGemDefinition skill = fixture.CreateSkillWithRootPayload<TauntSkillPayloadDef>("RemoveWrongNodeSkill", out _);
        SkillUpgradeNodeData ownerNode = fixture.AddNode("owner_node");
        SkillUpgradeNodeData otherNode = fixture.AddNode("other_node");
        NodeAbilityAuthoringService.AbilityResult created = CreateApplyStatusAbility(fixture, skill, ownerNode);

        var issues = new List<PayloadAuthoringIssue>();
        bool success = NodeAbilityAuthoringService.RemoveNodeAbility(
            fixture.Tree, skill, otherNode, (PayloadStep)created.Step, issues);

        Expect(!success, "Remove must refuse to remove a binding a different node owns.");
        Expect(issues.HasErrors(), "Refusing removal must report an Error.");
        Expect(ownerNode.grantedUpgradeIds.Contains(created.AbilityId), "The owning node's grant must remain untouched.");
    }

    static void TestUndoRestoresEverythingAfterCreate(Fixture fixture)
    {
        SkillGemDefinition skill = fixture.CreateSkillWithRootPayload<TauntSkillPayloadDef>("UndoSkill", out TauntSkillPayloadDef originalRoot);
        SkillUpgradeNodeData node = fixture.AddNode("undo_node");

        NodeAbilityAuthoringService.AbilityResult created = CreateApplyStatusAbility(fixture, skill, node);
        Expect(created.Success, "Create must succeed before testing Undo.");
        Expect(skill.payload is CompositeSkillPayloadDef, "Sanity check: skill must be composite after create.");

        Undo.PerformUndo();

        Expect(ReferenceEquals(skill.payload, originalRoot),
            "A single Undo must restore the skill's original single-payload root object.");
        Expect(node.grantedUpgradeIds == null || !node.grantedUpgradeIds.Contains(created.AbilityId),
            "A single Undo must revoke the node's granted ability id.");
    }

    // ---- Shared fixture helpers -----------------------------------------------------------

    static NodeAbilityAuthoringService.AbilityResult CreateApplyStatusAbility(
        Fixture fixture, SkillGemDefinition skill, SkillUpgradeNodeData node)
    {
        var draft = ScriptableObject.CreateInstance<ApplyStatusSkillPayloadDef>();
        draft.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            AddValidStatusApplication(draft, fixture.SomeStatus);
            var issues = new List<PayloadAuthoringIssue>();
            NodeAbilityAuthoringService.AbilityResult result =
                NodeAbilityAuthoringService.CreateNodeAbility(fixture.Tree, skill, node, draft, issues);
            Expect(result.Success, $"Fixture helper Create failed: {Describe(issues)}");
            return result;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(draft);
        }
    }

    static void AddValidStatusApplication(ApplyStatusSkillPayloadDef draft, StatusEffectDef effect)
    {
        var serialized = new SerializedObject(draft);
        SerializedProperty applications = serialized.FindProperty("applications");
        int index = applications.arraySize;
        applications.InsertArrayElementAtIndex(index);
        SerializedProperty spec = applications.GetArrayElementAtIndex(index).FindPropertyRelative("spec");
        spec.FindPropertyRelative("effect").objectReferenceValue = effect;
        spec.FindPropertyRelative("stacks").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    #region Fixture

    sealed class Fixture : IDisposable
    {
        public SkillUpgradeTreeDefinition Tree;
        public StatusEffectDef SomeStatus;
        public StatusEffectDef OtherStatus;

        public static Fixture Create()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.CreateFolder("Assets", TempFolder.Substring("Assets/".Length));

            var fixture = new Fixture();

            fixture.Tree = ScriptableObject.CreateInstance<SkillUpgradeTreeDefinition>();
            fixture.Tree.treeId = "smoke.node_authoring.tree";
            fixture.Tree.nodes = new List<SkillUpgradeNodeData>();
            AssetDatabase.CreateAsset(fixture.Tree, $"{TempFolder}/SmokeTree.asset");

            fixture.SomeStatus = CreateStatus("SmokeSomeStatus", "smoke.node_authoring.some");
            fixture.OtherStatus = CreateStatus("SmokeOtherStatus", "smoke.node_authoring.other");

            AssetDatabase.SaveAssetIfDirty(fixture.Tree);
            return fixture;
        }

        public SkillGemDefinition CreateSkillWithRootPayload<TPayload>(string assetName, out TPayload rootPayload)
            where TPayload : SkillPayloadDef
        {
            var skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
            skill.skillId = $"smoke.{assetName.ToLowerInvariant()}";
            skill.upgradeTree = Tree;
            AssetDatabase.CreateAsset(skill, $"{TempFolder}/{assetName}.asset");

            rootPayload = (TPayload)SkillPayloadAssetUtility.ReplaceWithEmbedded(skill, typeof(TPayload), recordUndo: false);

            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssetIfDirty(skill);
            return skill;
        }

        public SkillUpgradeNodeData AddNode(string nodeId)
        {
            var node = new SkillUpgradeNodeData
            {
                nodeId = nodeId,
                displayName = nodeId,
                cost = 1,
                requiredCharacterLevel = 1,
            };
            Undo.RegisterCompleteObjectUndo(Tree, "Add Smoke Test Node");
            Tree.nodes.Add(node);
            EditorUtility.SetDirty(Tree);
            AssetDatabase.SaveAssetIfDirty(Tree);
            return node;
        }

        static StatusEffectDef CreateStatus(string assetName, string effectId)
        {
            var definition = ScriptableObject.CreateInstance<StatusEffectDef>();
            definition.effectId = effectId;
            definition.duration = 5f;
            AssetDatabase.CreateAsset(definition, $"{TempFolder}/{assetName}.asset");
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }

        public void Dispose()
        {
            AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.Refresh();
        }
    }

    #endregion

    #region Assertions

    static string Describe(IReadOnlyList<PayloadAuthoringIssue> issues)
    {
        return issues == null || issues.Count == 0
            ? "<none>"
            : string.Join(" | ", issues.Select(issue => issue.ToString()));
    }

    static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
    }

    #endregion
}
#endif
