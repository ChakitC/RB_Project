#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase 4 smoke tests for ActiveSkillAbilityWizardWindow (plan section 13). OnGUI/button clicks
/// cannot be driven headlessly, so this covers the non-GUI mechanics through reflection: draft
/// construction/safe-defaults for Create, draft population for Edit, and cleanup on close. Manual
/// click-through in the Unity Editor (picker, field entry, warning confirmation, Advanced foldout,
/// scroll behavior) is still required before trusting the UI itself -- see plan section 18.7.
/// </summary>
public static class ActiveSkillAbilityWizardSmokeTests
{
    const string TempFolder = "Assets/_AbilityWizardSmokeTests";

    [MenuItem("Tools/RB/Skills/Run Ability Wizard Smoke Tests")]
    public static void RunFromMenu() => RunFromCommandLine();

    public static void RunFromCommandLine()
    {
        Fixture fixture = null;
        try
        {
            fixture = Fixture.Create();

            TestCreateModeStartsWithNoDraftUntilATypeIsPicked(fixture);
            TestCreateModeAppliesSafeDefaultsExactlyOnce(fixture);
            TestEditModeCopiesTheRealPayloadIntoTheDraft(fixture);
            TestCancelDestroysTheDraft(fixture);
            TestClosingTheWindowDestroysTheDraft(fixture);
            TestCommitThroughCreateEmbedsAnAbilityAndClosesTheWindow(fixture);

            Debug.Log("[AbilityWizardTests] All ability wizard smoke tests passed.");
        }
        finally
        {
            fixture?.Dispose();
        }
    }

    static void TestCreateModeStartsWithNoDraftUntilATypeIsPicked(Fixture fixture)
    {
        ActiveSkillAbilityWizardWindow window = ActiveSkillAbilityWizardWindow.OpenCreate(
            fixture.Tree, fixture.Skill, fixture.Node, null);
        try
        {
            Expect(GetDraft(window) == null, "Create mode must show the type picker before any draft exists.");
        }
        finally
        {
            window.Close();
        }
    }

    static void TestCreateModeAppliesSafeDefaultsExactlyOnce(Fixture fixture)
    {
        ActiveSkillAbilityWizardWindow window = ActiveSkillAbilityWizardWindow.OpenCreate(
            fixture.Tree, fixture.Skill, fixture.Node, null);
        try
        {
            InvokeBuildCreateDraft(window, typeof(TauntSkillPayloadDef));

            var draft = (SkillPayloadDef)GetDraft(window);
            Expect(draft != null, "Picking a type must create a draft.");
            Equal(HideFlags.HideAndDontSave, draft.hideFlags, "The draft must be HideAndDontSave, never a real asset.");
            Expect(draft is TauntSkillPayloadDef, "Draft type must match the picked payload type.");
            Expect(GetDescriptor(window) != null, "Picking a type must resolve its descriptor.");
        }
        finally
        {
            window.Close();
        }
    }

    static void TestEditModeCopiesTheRealPayloadIntoTheDraft(Fixture fixture)
    {
        var issues = new List<PayloadAuthoringIssue>();
        NodeAbilityAuthoringService.AbilityResult created = fixture.CreateApplyStatusAbility(issues);
        Expect(created.Success, "Fixture setup must succeed before testing Edit mode.");
        var realPayload = (ApplyStatusSkillPayloadDef)created.Payload;

        ActiveSkillAbilityWizardWindow window = ActiveSkillAbilityWizardWindow.OpenEdit(
            fixture.Tree, fixture.Skill, fixture.Node, (PayloadStep)created.Step, null);
        try
        {
            var draft = (ApplyStatusSkillPayloadDef)GetDraft(window);
            Expect(draft != null, "Edit mode must build a draft immediately, without a picker step.");
            Expect(!ReferenceEquals(draft, realPayload), "Edit draft must be a separate object from the real payload.");
            Equal(realPayload.Applications.Count, draft.Applications.Count, "Edit draft must copy the real payload's configuration.");
        }
        finally
        {
            window.Close();
        }
    }

    static void TestCancelDestroysTheDraft(Fixture fixture)
    {
        ActiveSkillAbilityWizardWindow window = ActiveSkillAbilityWizardWindow.OpenCreate(
            fixture.Tree, fixture.Skill, fixture.Node, null);
        InvokeBuildCreateDraft(window, typeof(TauntSkillPayloadDef));
        var draft = (SkillPayloadDef)GetDraft(window);
        Expect(draft != null, "Sanity check: draft must exist before cancel.");

        InvokeDestroyDraft(window);
        window.Close();

        Expect(draft == null, "DestroyDraft must leave no live HideAndDontSave object behind.");
    }

    static void TestClosingTheWindowDestroysTheDraft(Fixture fixture)
    {
        ActiveSkillAbilityWizardWindow window = ActiveSkillAbilityWizardWindow.OpenCreate(
            fixture.Tree, fixture.Skill, fixture.Node, null);
        InvokeBuildCreateDraft(window, typeof(TauntSkillPayloadDef));
        var draft = (SkillPayloadDef)GetDraft(window);
        Expect(draft != null, "Sanity check: draft must exist before close.");

        // Simulates the designer clicking the OS window-close button (no explicit Cancel click) --
        // OnDestroy must still clean up.
        UnityEngine.Object.DestroyImmediate(window);

        Expect(draft == null, "Closing the window without an explicit Cancel must still destroy the draft (OnDestroy).");
    }

    static void TestCommitThroughCreateEmbedsAnAbilityAndClosesTheWindow(Fixture fixture)
    {
        SkillUpgradeNodeData node = fixture.AddNode("wizard_commit_node");
        ActiveSkillAbilityWizardWindow window = ActiveSkillAbilityWizardWindow.OpenCreate(
            fixture.Tree, fixture.Skill, node, null);

        InvokeBuildCreateDraft(window, typeof(ApplyStatusSkillPayloadDef));
        var draft = (ApplyStatusSkillPayloadDef)GetDraft(window);
        AddValidStatusApplication(draft, fixture.SomeStatus);

        bool applied = false;
        SetOnApplied(window, () => applied = true);

        InvokeCommit(window);

        Expect(applied, "Commit must invoke the onApplied callback after a successful Create.");
        Expect(GetDraft(window) == null, "Commit must destroy the draft on success.");
        Expect(node.grantedUpgradeIds.Count == 1, "Commit must bind the generated ability id to the node.");
    }

    // ---- Reflection helpers (private window fields/methods are the whole point of this window) ----

    static object GetDraft(ActiveSkillAbilityWizardWindow window) => GetField(window, "_draft");
    static object GetDescriptor(ActiveSkillAbilityWizardWindow window) => GetField(window, "_descriptor");

    static object GetField(object target, string name)
    {
        FieldInfo field = typeof(ActiveSkillAbilityWizardWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            throw new InvalidOperationException($"Field '{name}' not found.");
        return field.GetValue(target);
    }

    static void SetOnApplied(ActiveSkillAbilityWizardWindow window, Action callback)
    {
        FieldInfo field = typeof(ActiveSkillAbilityWizardWindow).GetField("_onApplied", BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(window, callback);
    }

    static void InvokeBuildCreateDraft(ActiveSkillAbilityWizardWindow window, Type payloadType)
    {
        InvokeMethod(window, "BuildCreateDraft", payloadType);
    }

    static void InvokeDestroyDraft(ActiveSkillAbilityWizardWindow window)
    {
        InvokeMethod(window, "DestroyDraft");
    }

    static void InvokeCommit(ActiveSkillAbilityWizardWindow window)
    {
        InvokeMethod(window, "Commit");
    }

    static void InvokeMethod(object target, string name, params object[] args)
    {
        MethodInfo method = typeof(ActiveSkillAbilityWizardWindow).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
            throw new InvalidOperationException($"Method '{name}' not found.");
        method.Invoke(target, args);
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
        public SkillGemDefinition Skill;
        public SkillUpgradeTreeDefinition Tree;
        public SkillUpgradeNodeData Node;
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
            fixture.Tree.treeId = "smoke.wizard.tree";
            fixture.Tree.nodes = new List<SkillUpgradeNodeData>();
            AssetDatabase.CreateAsset(fixture.Tree, $"{TempFolder}/SmokeTree.asset");

            fixture.Node = fixture.AddNode("smoke_node");

            fixture.Skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
            fixture.Skill.skillId = "smoke.wizard.skill";
            fixture.Skill.upgradeTree = fixture.Tree;
            AssetDatabase.CreateAsset(fixture.Skill, $"{TempFolder}/SmokeSkill.asset");
            SkillPayloadAssetUtility.ReplaceWithEmbedded(fixture.Skill, typeof(TauntSkillPayloadDef), recordUndo: false);
            EditorUtility.SetDirty(fixture.Skill);
            AssetDatabase.SaveAssetIfDirty(fixture.Skill);

            fixture.SomeStatus = ScriptableObject.CreateInstance<StatusEffectDef>();
            fixture.SomeStatus.effectId = "smoke.wizard.status";
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

        public NodeAbilityAuthoringService.AbilityResult CreateApplyStatusAbility(List<PayloadAuthoringIssue> issues)
        {
            var draft = ScriptableObject.CreateInstance<ApplyStatusSkillPayloadDef>();
            draft.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                AddValidStatusApplication(draft, SomeStatus);
                return NodeAbilityAuthoringService.CreateNodeAbility(Tree, Skill, Node, draft, issues);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        public void Dispose()
        {
            AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.Refresh();
        }
    }

    #endregion

    #region Assertions

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
