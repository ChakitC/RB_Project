#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase 5 smoke tests for ActiveSkillTreeEditorWindow's node-centric ability cards (plan
/// section 14). Card resolution and the Duplicate action have no blocking UI, so they are driven
/// directly through reflection on a real window instance, mirroring
/// ActiveSkillStatusEffectAuthoringSmokeTests.TestUndoInvalidatesGameplayEffectCaches. Remove is
/// gated by an EditorUtility.DisplayDialog confirmation and cannot be driven headlessly -- its
/// underlying mutation is already covered by NodeAbilityAuthoringServiceSmokeTests, and this file
/// only checks that card resolution reflects a removal performed directly through the service.
/// Manual click-through (the picker, warning confirmations, the Remove dialog itself, and the
/// window's normal on-screen layout) is still required before trusting the full designer flow --
/// see plan section 18.7.
/// </summary>
public static class ActiveSkillNodeAbilityCardsSmokeTests
{
    const string TempFolder = "Assets/_AbilityCardsSmokeTests";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/RB/Skills/Run Node Ability Cards Smoke Tests")]
    public static void RunFromMenu() => RunFromCommandLine();

    public static void RunFromCommandLine()
    {
        Fixture fixture = null;
        try
        {
            fixture = Fixture.Create();

            TestResolveAbilityCardsFindsACreatedAbility(fixture);
            TestResolveAbilityCardsSkipsAnOrphanedGrantedId(fixture);
            TestResolveAbilityCardsDedupesRepeatedIds(fixture);
            TestDuplicateActionAddsASecondCardWithANewId(fixture);
            TestCardResolutionReflectsARemovalDoneThroughTheService(fixture);

            Debug.Log("[NodeAbilityCardsTests] All node ability card smoke tests passed.");
        }
        finally
        {
            fixture?.Dispose();
        }
    }

    static void TestResolveAbilityCardsFindsACreatedAbility(Fixture fixture)
    {
        SkillUpgradeNodeData node = fixture.AddNode("cards_node_1");
        NodeAbilityAuthoringService.AbilityResult created = fixture.CreateApplyStatusAbility(node);

        ActiveSkillTreeEditorWindow window = fixture.CreateWindow();
        try
        {
            var cards = (System.Collections.IList)InvokeResolveAbilityCards(window, fixture.Skill, node);

            Equal(1, cards.Count, "Expected exactly one card for the one ability this node grants.");
            object card = cards[0];
            Equal(created.AbilityId, (string)GetCardProperty(card, "AbilityId"), "Card must report the ability's real id.");
            Expect(ReferenceEquals(GetCardProperty(card, "Payload"), created.Payload), "Card must reference the real embedded payload, not a copy.");
            Expect(GetCardProperty(card, "Descriptor") != null, "Card must resolve a descriptor for a registered payload type.");
        }
        finally
        {
            CloseWindow(window);
        }
    }

    static void TestResolveAbilityCardsSkipsAnOrphanedGrantedId(Fixture fixture)
    {
        SkillUpgradeNodeData node = fixture.AddNode("cards_node_2");
        node.grantedUpgradeIds.Add("no.such.step.grants.this");

        ActiveSkillTreeEditorWindow window = fixture.CreateWindow();
        try
        {
            var cards = (System.Collections.IList)InvokeResolveAbilityCards(window, fixture.Skill, node);
            Equal(0, cards.Count, "An id with no matching PayloadStep must not produce a card.");
        }
        finally
        {
            CloseWindow(window);
        }
    }

    static void TestResolveAbilityCardsDedupesRepeatedIds(Fixture fixture)
    {
        SkillUpgradeNodeData node = fixture.AddNode("cards_node_3");
        NodeAbilityAuthoringService.AbilityResult created = fixture.CreateApplyStatusAbility(node);
        node.grantedUpgradeIds.Add(created.AbilityId);

        ActiveSkillTreeEditorWindow window = fixture.CreateWindow();
        try
        {
            var cards = (System.Collections.IList)InvokeResolveAbilityCards(window, fixture.Skill, node);
            Equal(1, cards.Count, "A duplicated granted id must still resolve to exactly one card.");
        }
        finally
        {
            CloseWindow(window);
        }
    }

    static void TestDuplicateActionAddsASecondCardWithANewId(Fixture fixture)
    {
        SkillUpgradeNodeData node = fixture.AddNode("cards_node_4");
        NodeAbilityAuthoringService.AbilityResult created = fixture.CreateApplyStatusAbility(node);

        ActiveSkillTreeEditorWindow window = fixture.CreateWindow();
        try
        {
            InvokeMethod(window, "DuplicateAbility", fixture.Skill, node, created.Step);

            var cards = (System.Collections.IList)InvokeResolveAbilityCards(window, fixture.Skill, node);
            Equal(2, cards.Count, "Duplicate must leave the node with two cards.");

            var ids = new HashSet<string>();
            foreach (object card in cards)
                ids.Add((string)GetCardProperty(card, "AbilityId"));
            Equal(2, ids.Count, "The original and duplicated ability must have distinct ids.");
        }
        finally
        {
            CloseWindow(window);
        }
    }

    static void TestCardResolutionReflectsARemovalDoneThroughTheService(Fixture fixture)
    {
        SkillUpgradeNodeData node = fixture.AddNode("cards_node_5");
        NodeAbilityAuthoringService.AbilityResult created = fixture.CreateApplyStatusAbility(node);

        var issues = new List<PayloadAuthoringIssue>();
        bool removed = NodeAbilityAuthoringService.RemoveNodeAbility(
            fixture.Tree, fixture.Skill, node, (PayloadStep)created.Step, issues);
        Expect(removed, $"Fixture setup must remove the ability successfully. Issues: {Describe(issues)}");

        ActiveSkillTreeEditorWindow window = fixture.CreateWindow();
        try
        {
            var cards = (System.Collections.IList)InvokeResolveAbilityCards(window, fixture.Skill, node);
            Equal(0, cards.Count, "Card resolution must not show an ability removed through the service.");
        }
        finally
        {
            CloseWindow(window);
        }
    }

    // ---- Reflection helpers ------------------------------------------------------------------

    static object InvokeResolveAbilityCards(ActiveSkillTreeEditorWindow window, SkillGemDefinition owner, SkillUpgradeNodeData node)
    {
        MethodInfo method = typeof(ActiveSkillTreeEditorWindow).GetMethod("ResolveAbilityCards", Flags);
        if (method == null)
            throw new InvalidOperationException("ResolveAbilityCards not found.");
        return method.Invoke(window, new object[] { owner, node });
    }

    static void InvokeMethod(object target, string name, params object[] args)
    {
        MethodInfo method = typeof(ActiveSkillTreeEditorWindow).GetMethod(name, Flags);
        if (method == null)
            throw new InvalidOperationException($"Method '{name}' not found.");
        method.Invoke(target, args);
    }

    static object GetCardProperty(object card, string name)
    {
        Type cardType = card.GetType();
        PropertyInfo property = cardType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
            throw new InvalidOperationException($"AbilityCardInfo.{name} not found.");
        return property.GetValue(card);
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

    // DuplicateAbility (and any future action routed through OnAbilityAuthoringApplied) marks the
    // window _dirty. Destroying a _dirty tree window fires OnDisable's real
    // EditorUtility.DisplayDialog("Unsaved Active Skill Tree", ...) prompt, which blocks the whole
    // Unity process with nothing able to click it in an automated run -- clear _dirty first.
    static void CloseWindow(ActiveSkillTreeEditorWindow window)
    {
        typeof(ActiveSkillTreeEditorWindow).GetField("_dirty", Flags)?.SetValue(window, false);
        UnityEngine.Object.DestroyImmediate(window);
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
            fixture.Tree.treeId = "smoke.cards.tree";
            fixture.Tree.nodes = new List<SkillUpgradeNodeData>();
            AssetDatabase.CreateAsset(fixture.Tree, $"{TempFolder}/SmokeTree.asset");

            fixture.Skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
            fixture.Skill.skillId = "smoke.cards.skill";
            fixture.Skill.upgradeTree = fixture.Tree;
            AssetDatabase.CreateAsset(fixture.Skill, $"{TempFolder}/SmokeSkill.asset");
            SkillPayloadAssetUtility.ReplaceWithEmbedded(fixture.Skill, typeof(TauntSkillPayloadDef), recordUndo: false);
            EditorUtility.SetDirty(fixture.Skill);
            AssetDatabase.SaveAssetIfDirty(fixture.Skill);

            fixture.SomeStatus = ScriptableObject.CreateInstance<StatusEffectDef>();
            fixture.SomeStatus.effectId = "smoke.cards.status";
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

        public NodeAbilityAuthoringService.AbilityResult CreateApplyStatusAbility(SkillUpgradeNodeData node)
        {
            var draft = ScriptableObject.CreateInstance<ApplyStatusSkillPayloadDef>();
            draft.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                AddValidStatusApplication(draft, SomeStatus);
                var issues = new List<PayloadAuthoringIssue>();
                NodeAbilityAuthoringService.AbilityResult result =
                    NodeAbilityAuthoringService.CreateNodeAbility(Tree, Skill, node, draft, issues);
                Expect(result.Success, $"Fixture helper Create failed: {Describe(issues)}");
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

    static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
    }

    #endregion
}
#endif
