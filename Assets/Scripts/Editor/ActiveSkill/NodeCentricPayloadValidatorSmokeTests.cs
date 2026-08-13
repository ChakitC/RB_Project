#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase 6 smoke tests for unified validation (plan section 15). Covers
/// NodeCentricPayloadValidator's pure logic directly, plus the tree window's Save gate through
/// reflection for the non-blocking (zero-issue) path only -- the error/warning paths pop an
/// EditorUtility.DisplayDialog and cannot be driven headlessly, so those are exercised by
/// constructing issue lists directly instead of going through the dialog-guarded save method.
/// Manual click-through of "Cannot Save" and "Save With Warnings?" is still required -- see plan
/// section 18.7.
/// </summary>
public static class NodeCentricPayloadValidatorSmokeTests
{
    const string TempFolder = "Assets/_UnifiedValidationSmokeTests";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/RB/Skills/Run Unified Validation Smoke Tests")]
    public static void RunFromMenu() => RunFromCommandLine();

    public static void RunFromCommandLine()
    {
        Fixture fixture = null;
        try
        {
            fixture = Fixture.Create();

            TestCleanAbilityProducesNoIssues(fixture);
            TestMissingRequiredReferenceIsReportedAsAnErrorOnTheGrantingNode(fixture);
            TestNonNormalizedGrantedIdIsAWarningOnItsNode(fixture);
            TestAlwaysActiveStepIsNeverAttributedToANode(fixture);
            TestSaveGateReturnsTrueImmediatelyWhenThereAreNoIssues(fixture);

            Debug.Log("[UnifiedValidationTests] All unified validation smoke tests passed.");
        }
        finally
        {
            fixture?.Dispose();
        }
    }

    static void TestCleanAbilityProducesNoIssues(Fixture fixture)
    {
        SkillUpgradeNodeData node = fixture.AddNode("validator_clean_node");
        fixture.CreateApplyStatusAbility(node, fixture.SomeStatus);

        List<SkillUpgradeValidationIssue> issues = NodeCentricPayloadValidator.Validate(
            fixture.Tree, new List<SkillDefinitionBase> { fixture.Skill });

        Expect(issues.Count == 0, $"A fully configured ability must report no issues. Got: {Describe(issues)}");
    }

    static void TestMissingRequiredReferenceIsReportedAsAnErrorOnTheGrantingNode(Fixture fixture)
    {
        SkillUpgradeNodeData node = fixture.AddNode("validator_missing_ref_node");

        // TauntSkillPayloadDef with no Taunt Status assigned is always invalid (Phase 1 descriptor
        // test already covers this at the descriptor level) -- here the point is that the unified
        // validator surfaces it, tagged with the exact node that grants it.
        var draft = ScriptableObject.CreateInstance<TauntSkillPayloadDef>();
        draft.hideFlags = HideFlags.HideAndDontSave;
        NodeAbilityAuthoringService.AbilityResult created;
        try
        {
            var issues = new List<PayloadAuthoringIssue>();
            created = NodeAbilityAuthoringService.CreateNodeAbility(fixture.Tree, fixture.Skill, node, draft, issues);
            Expect(!created.Success, "A Taunt draft with no Taunt Status must fail Create -- sanity check for this test's premise.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(draft);
        }

        // NodeAbilityAuthoringService already refuses to embed an invalid draft (Phase 3), so
        // reaching the "missing reference on a real embedded payload" case here means authoring it
        // directly the way Advanced/Developer mode or a pre-Phase-1 asset could. That path also
        // needs a composite payload to attach to -- the failed Create above never converted one,
        // so force the conversion here the same way a first successful Create would have.
        NodeAbilityAuthoringService.ConvertToCompositePreservingExecution(
            fixture.Skill, out CompositeSkillPayloadDef composite, new List<PayloadAuthoringIssue>());

        var directPayload = ScriptableObject.CreateInstance<TauntSkillPayloadDef>();
        directPayload.name = "Taunt Execution";
        directPayload.hideFlags = HideFlags.None;
        AssetDatabase.AddObjectToAsset(directPayload, fixture.Skill);
        var step = new PayloadStep();
        step.SetPayload(directPayload);
        step.RequiredUpgradeId = "smoke.validator.direct_taunt";
        composite.AddStep(step);
        node.grantedUpgradeIds.Add("smoke.validator.direct_taunt");
        EditorUtility.SetDirty(fixture.Skill);
        AssetDatabase.SaveAssetIfDirty(fixture.Skill);

        List<SkillUpgradeValidationIssue> validated = NodeCentricPayloadValidator.Validate(
            fixture.Tree, new List<SkillDefinitionBase> { fixture.Skill });

        bool found = false;
        for (int i = 0; i < validated.Count; i++)
        {
            if (validated[i].Severity == SkillUpgradeValidationSeverity.Error &&
                validated[i].BelongsTo(node.RuntimeNodeId))
            {
                found = true;
                break;
            }
        }

        Expect(found, $"Expected an Error attributed to node '{node.RuntimeNodeId}' for the missing Taunt Status. Got: {Describe(validated)}");
    }

    static void TestNonNormalizedGrantedIdIsAWarningOnItsNode(Fixture fixture)
    {
        SkillUpgradeNodeData node = fixture.AddNode("validator_raw_id_node");
        node.grantedUpgradeIds.Add("Not Normalized ID!!");

        List<SkillUpgradeValidationIssue> issues = NodeCentricPayloadValidator.Validate(
            fixture.Tree, new List<SkillDefinitionBase> { fixture.Skill });

        bool found = false;
        for (int i = 0; i < issues.Count; i++)
        {
            if (issues[i].Severity == SkillUpgradeValidationSeverity.Warning && issues[i].BelongsTo(node.RuntimeNodeId))
            {
                found = true;
                break;
            }
        }

        Expect(found, $"Expected a Warning on node '{node.RuntimeNodeId}' for a non-normalized granted id. Got: {Describe(issues)}");
    }

    static void TestAlwaysActiveStepIsNeverAttributedToANode(Fixture fixture)
    {
        // The fixture's root Taunt payload (converted to the always-active step by the very first
        // CreateNodeAbility call) has a blank RequiredUpgradeId and, being unconfigured, is itself
        // invalid -- but it must never be reported as belonging to any node, since no node owns it.
        SkillUpgradeNodeData node = fixture.AddNode("validator_always_active_probe_node");
        fixture.CreateApplyStatusAbility(node, fixture.SomeStatus);

        List<SkillUpgradeValidationIssue> issues = NodeCentricPayloadValidator.Validate(
            fixture.Tree, new List<SkillDefinitionBase> { fixture.Skill });

        for (int i = 0; i < issues.Count; i++)
            Expect(!issues[i].BelongsTo(null), "NodeCentricPayloadValidator must never emit a null-node issue for a step -- only registry diagnostics use null.");
    }

    // Deliberately does not reuse fixture.Skill/fixture.Tree: the earlier tests in this run
    // intentionally leave an invalid direct-authored Taunt step on that shared skill (plan
    // section 15 wants that reported, not silently ignored), and ConfirmSaveAgainstValidationIssues
    // pops a real, non-scriptable EditorUtility.DisplayDialog the moment it sees any Error --
    // reusing the corrupted shared skill here would block the whole Unity process on a dialog
    // this automated run cannot click. This test needs its own guaranteed-clean skill/tree.
    static void TestSaveGateReturnsTrueImmediatelyWhenThereAreNoIssues(Fixture fixture)
    {
        SkillUpgradeTreeDefinition isolatedTree = ScriptableObject.CreateInstance<SkillUpgradeTreeDefinition>();
        isolatedTree.treeId = "smoke.validator.save_gate.tree";
        isolatedTree.nodes = new List<SkillUpgradeNodeData>();
        AssetDatabase.CreateAsset(isolatedTree, $"{TempFolder}/SaveGateTree.asset");

        SkillGemDefinition isolatedSkill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        isolatedSkill.skillId = "smoke.validator.save_gate.skill";
        isolatedSkill.upgradeTree = isolatedTree;
        AssetDatabase.CreateAsset(isolatedSkill, $"{TempFolder}/SaveGateSkill.asset");
        SkillPayloadAssetUtility.ReplaceWithEmbedded(isolatedSkill, typeof(TauntSkillPayloadDef), recordUndo: false);
        EditorUtility.SetDirty(isolatedSkill);
        AssetDatabase.SaveAssetIfDirty(isolatedSkill);

        var node = new SkillUpgradeNodeData { nodeId = "save_gate_node", displayName = "save_gate_node", cost = 1, requiredCharacterLevel = 1 };
        isolatedTree.nodes.Add(node);
        EditorUtility.SetDirty(isolatedTree);
        AssetDatabase.SaveAssetIfDirty(isolatedTree);

        var draft = ScriptableObject.CreateInstance<ApplyStatusSkillPayloadDef>();
        draft.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            AddValidStatusApplication(draft, fixture.SomeStatus);
            var createIssues = new List<PayloadAuthoringIssue>();
            NodeAbilityAuthoringService.AbilityResult result =
                NodeAbilityAuthoringService.CreateNodeAbility(isolatedTree, isolatedSkill, node, draft, createIssues);
            Expect(result.Success, $"Isolated fixture setup must succeed: {Describe(createIssues)}");
            EditorUtility.SetDirty(isolatedSkill);
            AssetDatabase.SaveAssetIfDirty(isolatedSkill);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(draft);
        }

        // Sanity check before calling the dialog-guarded gate: fail loudly here instead of risking
        // another stuck DisplayDialog if this test's own premise (a clean tree) is ever violated.
        List<SkillUpgradeValidationIssue> preflight = NodeCentricPayloadValidator.Validate(
            isolatedTree, new List<SkillDefinitionBase> { isolatedSkill });
        Expect(preflight.Count == 0, $"Isolated skill must be clean before exercising the Save gate. Got: {Describe(preflight)}");

        var window = ScriptableObject.CreateInstance<ActiveSkillTreeEditorWindow>();
        typeof(ActiveSkillTreeEditorWindow).GetField("_tree", Flags)?.SetValue(window, isolatedTree);
        typeof(ActiveSkillTreeEditorWindow).GetField("_dirty", Flags)?.SetValue(window, false);
        try
        {
            MethodInfo method = typeof(ActiveSkillTreeEditorWindow).GetMethod("ConfirmSaveAgainstValidationIssues", Flags);
            Expect(method != null, "ConfirmSaveAgainstValidationIssues must exist on the tree window.");
            var result = (bool)method.Invoke(window, null);
            Expect(result, "A clean tree must not block or prompt on Save.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(window);
        }
    }

    // ---- Helpers -------------------------------------------------------------------------

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
        public SkillGemDefinition Skill;
        public SkillUpgradeTreeDefinition Tree;
        public StatusEffectDef SomeStatus;

        public static Fixture Create()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.CreateFolder("Assets", TempFolder.Substring("Assets/".Length));

            var fixture = new Fixture
            {
                Tree = ScriptableObject.CreateInstance<SkillUpgradeTreeDefinition>(),
            };
            fixture.Tree.treeId = "smoke.validator.tree";
            fixture.Tree.nodes = new List<SkillUpgradeNodeData>();
            AssetDatabase.CreateAsset(fixture.Tree, $"{TempFolder}/SmokeTree.asset");

            fixture.Skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
            fixture.Skill.skillId = "smoke.validator.skill";
            fixture.Skill.upgradeTree = fixture.Tree;
            AssetDatabase.CreateAsset(fixture.Skill, $"{TempFolder}/SmokeSkill.asset");
            SkillPayloadAssetUtility.ReplaceWithEmbedded(fixture.Skill, typeof(TauntSkillPayloadDef), recordUndo: false);
            EditorUtility.SetDirty(fixture.Skill);
            AssetDatabase.SaveAssetIfDirty(fixture.Skill);

            fixture.SomeStatus = ScriptableObject.CreateInstance<StatusEffectDef>();
            fixture.SomeStatus.effectId = "smoke.validator.status";
            fixture.SomeStatus.duration = 5f;
            AssetDatabase.CreateAsset(fixture.SomeStatus, $"{TempFolder}/SmokeStatus.asset");
            AssetDatabase.SaveAssetIfDirty(fixture.SomeStatus);

            return fixture;
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
            Tree.nodes.Add(node);
            EditorUtility.SetDirty(Tree);
            AssetDatabase.SaveAssetIfDirty(Tree);
            return node;
        }

        public NodeAbilityAuthoringService.AbilityResult CreateApplyStatusAbility(SkillUpgradeNodeData node, StatusEffectDef status)
        {
            var draft = ScriptableObject.CreateInstance<ApplyStatusSkillPayloadDef>();
            draft.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                AddValidStatusApplication(draft, status);
                var issues = new List<PayloadAuthoringIssue>();
                NodeAbilityAuthoringService.AbilityResult result =
                    NodeAbilityAuthoringService.CreateNodeAbility(Tree, Skill, node, draft, issues);
                Expect(result.Success, $"Fixture helper Create failed: {Describe(issues)}");
                EditorUtility.SetDirty(Skill);
                AssetDatabase.SaveAssetIfDirty(Skill);
                return result;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        public ActiveSkillTreeEditorWindow CreateWindow()
        {
            var window = ScriptableObject.CreateInstance<ActiveSkillTreeEditorWindow>();
            typeof(ActiveSkillTreeEditorWindow).GetField("_tree", Flags)?.SetValue(window, Tree);
            typeof(ActiveSkillTreeEditorWindow).GetField("_dirty", Flags)?.SetValue(window, false);
            return window;
        }

        public void Dispose()
        {
            AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.Refresh();
        }
    }

    #endregion

    #region Assertions

    static string Describe(IReadOnlyList<SkillUpgradeValidationIssue> issues)
    {
        if (issues == null || issues.Count == 0)
            return "<none>";

        var parts = new List<string>();
        foreach (SkillUpgradeValidationIssue issue in issues)
            parts.Add($"{issue.Severity}[{issue.NodeId ?? "tree"}]: {issue.Message}");
        return string.Join(" | ", parts);
    }

    static string Describe(IReadOnlyList<PayloadAuthoringIssue> issues)
    {
        if (issues == null || issues.Count == 0)
            return "<none>";

        var parts = new List<string>();
        foreach (PayloadAuthoringIssue issue in issues)
            parts.Add(issue.ToString());
        return string.Join(" | ", parts);
    }

    static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    #endregion
}
#endif
