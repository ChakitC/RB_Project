using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public static class ActiveSkillFeatureSmokeTests
{
    [MenuItem("Tools/RB/Skills/Run Active Skill Core Smoke Tests")]
    public static void RunFromMenu()
    {
        RunFromCommandLine();
        EditorUtility.DisplayDialog("Active Skill Tests", "All core smoke tests passed.", "OK");
    }

    public static void RunFromCommandLine()
    {
        var assets = new List<ScriptableObject>();
        try
        {
            TestCatchUpAndPassiveIsolation();
            TestPrerequisitesSharedPoolAndVariantIsolation(assets);
            TestResetUsesPaidCost(assets);
            TestTreeMismatchRefundsRemovedNodes(assets);
            TestDeterministicStatStacking(assets);
            TestSkillTreeDefaultAndVariantOverride(assets);
            TestVisualScaleMetrics();
            TestRuntimeNodeVisualScaleAndFrameFallback();
            TestGraphDisplaysAndRefreshesNodeIcon(assets);
            TestGraphVisualScaleUsesCenteredPosition(assets);
            TestGraphAllowsOneUnlocksPortToReachMultipleRequiresPorts(assets);
            TestVisualScaleValidationAndBounds(assets);
            TestTreeNavigationMath();
            TestUpgradeUiSkillTreeButtonWiring();
            Debug.Log("[ActiveSkillTests] All core smoke tests passed.");
        }
        finally
        {
            for (int i = 0; i < assets.Count; i++)
                UnityEngine.Object.DestroyImmediate(assets[i]);
        }
    }

    static void TestCatchUpAndPassiveIsolation()
    {
        var data = new CharacterProgressData
        {
            level = 6,
            skillPoints = 9,
        };
        var model = new ActiveSkillProgressModel(null, data, data.level);

        Expect(model.EnsureInitialized(), "Old progress must be initialized once.");
        Equal(5, model.AvailablePoints, "Catch-up must grant one point for levels after level 1.");
        Equal(9, data.skillPoints, "Active Skill Points must not change Passive Points.");
        Expect(!model.EnsureInitialized(), "Catch-up must not run twice.");
        Equal(5, model.AvailablePoints, "Repeated initialization must not duplicate points.");
    }

    static void TestPrerequisitesSharedPoolAndVariantIsolation(List<ScriptableObject> assets)
    {
        SkillUpgradeTreeDefinition firstTree = CreateTree(assets, "tree.first",
            Node("root", 1), Node("child", 2, "root"));
        SkillUpgradeTreeDefinition secondTree = CreateTree(assets, "tree.second", Node("other", 1));
        var data = InitializedData(4);
        var model = new ActiveSkillProgressModel(null, data, 10);

        Expect(!model.TryUnlock("slot.1", "variant.a", firstTree, "child", out _),
            "All prerequisite nodes must be unlocked first.");
        Expect(model.TryUnlock("slot.1", "variant.a", firstTree, "root", out string rootReason), rootReason);
        Equal(3, model.AvailablePoints, "Every variant must spend from the shared character pool.");
        Expect(model.TryUnlock("slot.2", "variant.b", secondTree, "other", out string otherReason), otherReason);
        Equal(2, model.AvailablePoints, "A different slot must use the same shared pool.");
        Expect(!model.IsUnlocked("slot.1", "variant.a", firstTree, "other", out _),
            "Progress from another variant must remain isolated.");
        Expect(model.TryUnlock("slot.1", "variant.a", firstTree, "child", out string childReason), childReason);
        Equal(0, model.AvailablePoints, "Child cost must be charged after its prerequisite is met.");
    }

    static void TestResetUsesPaidCost(List<ScriptableObject> assets)
    {
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.reset", Node("root", 3));
        var data = InitializedData(5);
        var model = new ActiveSkillProgressModel(null, data, 10);
        Expect(model.TryUnlock("slot", "variant", tree, "root", out string reason), reason);

        tree.nodes[0].cost = 99;
        Expect(model.ResetTree("slot", "variant", tree, out int refund), "Reset must clear an unlocked tree.");
        Equal(3, refund, "Reset must use the cost paid at unlock time, not the edited asset cost.");
        Equal(5, model.AvailablePoints, "Reset must fully restore the paid points.");
    }

    static void TestTreeMismatchRefundsRemovedNodes(List<ScriptableObject> assets)
    {
        SkillUpgradeTreeDefinition replacement = CreateTree(assets, "tree.new", Node("new", 1));
        var data = InitializedData(2);
        data.activeSkillTrees.Add(new CharacterSkillTreeProgressSaveData
        {
            slotId = "slot",
            optionId = "variant",
            treeId = "tree.old",
            unlockedNodes = new List<CharacterSkillUpgradeNodeSaveData>
            {
                new() { nodeId = "removed", paidCost = 4 },
            },
        });
        var model = new ActiveSkillProgressModel(null, data, 10);

        SkillUpgradeStatSnapshot snapshot = model.BuildSnapshot("slot", "variant", replacement, out bool changed);
        Expect(changed, "Changing tree ID must reconcile old progress.");
        Expect(snapshot.IsEmpty, "Nodes missing from the replacement tree must have no runtime effect.");
        Equal(6, model.AvailablePoints, "Tree replacement must refund the originally paid cost.");
        Equal("tree.new", data.activeSkillTrees[0].treeId, "Progress must move to the replacement tree ID.");
        Equal(0, data.activeSkillTrees[0].unlockedNodes.Count, "Replacement tree must start empty.");
    }

    static void TestDeterministicStatStacking(List<ScriptableObject> assets)
    {
        SkillUpgradeNodeData first = Node("first", 1);
        first.skillLevelDelta = 1;
        first.statModifiers.Add(new StatModifier { stat = StatType.Damage, add = 10f, mul = 1.5f });
        SkillUpgradeNodeData second = Node("second", 1);
        second.skillLevelDelta = 2;
        second.statModifiers.Add(new StatModifier { stat = StatType.Damage, add = 5f, mul = 2f });
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.stats", first, second);
        var data = InitializedData(2);
        var model = new ActiveSkillProgressModel(null, data, 10);
        Expect(model.TryUnlock("slot", "variant", tree, "first", out string firstReason), firstReason);
        Expect(model.TryUnlock("slot", "variant", tree, "second", out string secondReason), secondReason);

        SkillUpgradeStatSnapshot snapshot = model.BuildSnapshot("slot", "variant", tree, out _);
        var stats = new FinalSkillStats { damage = 100f };
        snapshot.Apply(stats);
        Equal(3, snapshot.SkillLevelDelta, "Skill level deltas must sum.");
        Approximately(345f, stats.damage, "Stat stacking must be (base + sum(add)) * product(mul).");
    }

    static void TestSkillTreeDefaultAndVariantOverride(List<ScriptableObject> assets)
    {
        SkillUpgradeTreeDefinition defaultTree = CreateTree(assets, "tree.default", Node("default", 1));
        SkillUpgradeTreeDefinition overrideTree = CreateTree(assets, "tree.override", Node("override", 1));
        SkillGemDefinition skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        skill.upgradeTree = defaultTree;
        assets.Add(skill);

        var option = new CharacterSkillLoadoutOption { skillAsset = skill };
        Equal(defaultTree, option.ResolvedUpgradeTree,
            "A variant without an override must use its Skill Asset tree.");

        option.upgradeTreeOverride = overrideTree;
        Equal(overrideTree, option.ResolvedUpgradeTree,
            "A variant override must take precedence over the Skill Asset tree.");
    }

    static void TestVisualScaleMetrics()
    {
        SkillUpgradeNodeData node = Node("scale", 1);
        Approximately(1f, node.ResolvedVisualScale, "A new node must use the normal visual scale.");
        Approximately(96f, node.ResolvedRuntimeSize, "Normal visual scale must resolve to 96 pixels.");

        node.visualScale = 1.5f;
        Approximately(1.5f, node.ResolvedVisualScale, "An authored visual scale must be preserved.");
        Approximately(144f, node.ResolvedRuntimeSize, "Runtime size must use the authored visual scale.");

        node.visualScale = 3f;
        Approximately(2f, node.ResolvedVisualScale, "Visual scale must clamp to the maximum.");
        Approximately(192f, node.ResolvedRuntimeSize, "Maximum visual scale must resolve to 192 pixels.");

        node.visualScale = float.NaN;
        Approximately(1f, node.ResolvedVisualScale, "A non-finite visual scale must fall back to normal.");
    }

    static void TestRuntimeNodeVisualScaleAndFrameFallback()
    {
        var root = new GameObject(
            "VisualScaleTestNode",
            typeof(RectTransform),
            typeof(UnityEngine.UI.Image),
            typeof(ActiveSkillUpgradeNodeView));
        var texture = new Texture2D(2, 2);
        Sprite normalFrame = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
        Sprite importantFrame = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
        SkillScreenTheme theme = ScriptableObject.CreateInstance<SkillScreenTheme>();
        try
        {
            ActiveSkillUpgradeNodeView view = root.GetComponent<ActiveSkillUpgradeNodeView>();
            UnityEngine.UI.Image frame = root.GetComponent<UnityEngine.UI.Image>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("frame").objectReferenceValue = frame;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            theme.nodeFrame = normalFrame;
            theme.importantNodeFrame = importantFrame;
            SkillUpgradeNodeData node = Node("visual", 1);
            node.visualScale = 1.5f;

            view.Bind(node, ActiveSkillNodeVisualState.Locked, false, theme, null);
            Equal(new Vector2(144f, 144f), root.GetComponent<RectTransform>().sizeDelta,
                "Runtime node RectTransform must follow visual scale.");
            Equal(importantFrame, frame.sprite,
                "A scaled node must use the important frame when one is assigned.");

            theme.importantNodeFrame = null;
            view.Bind(node, ActiveSkillNodeVisualState.Locked, false, theme, null);
            Equal(normalFrame, frame.sprite,
                "A scaled node must fall back to the normal frame.");

            node.visualScale = 1f;
            theme.importantNodeFrame = importantFrame;
            view.Bind(node, ActiveSkillNodeVisualState.Locked, false, theme, null);
            Equal(normalFrame, frame.sprite,
                "A normal-sized node must keep the normal frame.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(theme);
            UnityEngine.Object.DestroyImmediate(normalFrame);
            UnityEngine.Object.DestroyImmediate(importantFrame);
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void TestGraphAllowsOneUnlocksPortToReachMultipleRequiresPorts(List<ScriptableObject> assets)
    {
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.graph",
            Node("root", 1), Node("first", 1, "root"), Node("second", 1));
        var graph = new ActiveSkillTreeEditorWindow.SkillTreeGraphView();
        graph.Load(tree);

        List<Port> allPorts = graph.ports.ToList();
        Port rootOutput = FindPort(allPorts, "root", Direction.Output);
        Port firstInput = FindPort(allPorts, "first", Direction.Input);
        Port secondInput = FindPort(allPorts, "second", Direction.Input);
        List<Port> compatible = graph.GetCompatiblePorts(rootOutput, null);

        Expect(!compatible.Contains(firstInput),
            "The editor must not offer a duplicate connection to the same Requires port.");
        Expect(compatible.Contains(secondInput),
            "A connected Unlocks port must remain available for another Requires port.");
    }

    static void TestGraphDisplaysAndRefreshesNodeIcon(List<ScriptableObject> assets)
    {
        var texture = new Texture2D(2, 2);
        Sprite icon = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
        try
        {
            SkillUpgradeNodeData node = Node("icon", 1);
            node.icon = icon;
            SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.icon", node);
            var graph = new ActiveSkillTreeEditorWindow.SkillTreeGraphView();
            graph.Load(tree);

            Image image = graph.Q<Image>("skill-node-icon");
            Expect(image != null, "The graph node must contain an icon element.");
            Equal(icon, image.sprite, "The graph node must display its authored Sprite.");

            node.icon = null;
            graph.RefreshTitles();
            Equal(null, image.sprite, "The graph node icon must refresh after Inspector changes.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(icon);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    static void TestGraphVisualScaleUsesCenteredPosition(List<ScriptableObject> assets)
    {
        SkillUpgradeNodeData node = Node("scaled", 1);
        node.uiPosition = new Vector2(180f, 90f);
        node.visualScale = 1.5f;
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.scaled-graph", node);
        var graph = new ActiveSkillTreeEditorWindow.SkillTreeGraphView();
        graph.Load(tree);

        Node graphNode = graph.nodes.Single();
        Rect rect = GetAuthoredRect(graphNode);
        Equal(node.uiPosition, rect.center,
            "Graph node position must treat uiPosition as the node center.");
        Equal(new Vector2(330f, 180f), rect.size,
            "Graph node size must use the authored visual scale.");

        node.visualScale = 2f;
        graph.RefreshTitles();
        rect = GetAuthoredRect(graphNode);
        Equal(node.uiPosition, rect.center,
            "Changing visual scale must preserve the graph node center.");
        Equal(new Vector2(440f, 240f), rect.size,
            "Graph node size must refresh after Inspector changes.");

        graphNode.SetPosition(new Rect(new Vector2(40f, 60f), rect.size));
        graph.HandleGraphChange(new GraphViewChange
        {
            movedElements = new List<GraphElement> { graphNode },
        });
        Equal(GetAuthoredRect(graphNode).center, node.uiPosition,
            "Moving a graph node must save its center position.");
    }

    static Port FindPort(List<Port> ports, string nodeTitle, Direction direction)
    {
        Port port = ports.FirstOrDefault(candidate =>
            candidate.direction == direction && candidate.node?.title == nodeTitle);
        if (port == null)
            throw new InvalidOperationException($"Missing {direction} port for node '{nodeTitle}'.");
        return port;
    }

    static void TestUpgradeUiSkillTreeButtonWiring()
    {
        const string prefabPath = "Assets/Prefab/User Interface/UpgradUI.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Expect(prefab != null, "UpgradUI prefab must exist.");

        UILoadLaval controller = prefab.GetComponent<UILoadLaval>();
        Expect(controller != null, "UpgradUI prefab must contain UILoadLaval.");

        UnityEngine.UI.Button button = prefab.GetComponentsInChildren<UnityEngine.UI.Button>(true)
            .FirstOrDefault(candidate => candidate.name == "Skill_Tree_Button");
        Expect(button != null, "UpgradUI prefab must contain Skill_Tree_Button.");
        Equal(1, button.onClick.GetPersistentEventCount(),
            "Skill_Tree_Button must have one persistent listener.");
        Expect(button.onClick.GetPersistentTarget(0) == controller,
            "Skill_Tree_Button must target its UILoadLaval controller.");
        Equal(nameof(UILoadLaval.OpenActiveSkillTree), button.onClick.GetPersistentMethodName(0),
            "Skill_Tree_Button must open the Active Skill Tree.");

        var serializedController = new SerializedObject(controller);
        SerializedProperty screenPrefab = serializedController.FindProperty("activeSkillScreenPrefab");
        Expect(screenPrefab?.objectReferenceValue is ActiveSkillScreenController,
            "UpgradUI must reference the ActiveSkillScreen prefab.");

        var activeSkillScreen = screenPrefab.objectReferenceValue as ActiveSkillScreenController;
        Equal(Vector3.one, activeSkillScreen.transform.localScale,
            "ActiveSkillScreen prefab root scale must remain visible when instantiated.");
        Canvas screenCanvas = activeSkillScreen.GetComponent<Canvas>();
        Expect(screenCanvas != null && screenCanvas.sortingOrder >= 100,
            "ActiveSkillScreen must render above the lobby UI.");

        var serializedActiveSkillScreen = new SerializedObject(activeSkillScreen);
        var fitTreeButton = serializedActiveSkillScreen.FindProperty("fitTreeButton")?.objectReferenceValue
            as UnityEngine.UI.Button;
        Expect(fitTreeButton != null && fitTreeButton.name == "FitTreeButton",
            "ActiveSkillScreen must wire the FIT TREE button.");

        GameObject tooltipRoot =
            serializedActiveSkillScreen.FindProperty("treeNavigationTooltip")?.objectReferenceValue as GameObject;
        Expect(tooltipRoot != null && !tooltipRoot.activeSelf,
            "The tree navigation tooltip must start hidden.");
        Expect(tooltipRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)
                .All(graphic => !graphic.raycastTarget),
            "The tree navigation tooltip must not block pointer input.");
        EventTrigger fitTreeTrigger = fitTreeButton.GetComponent<EventTrigger>();
        Expect(fitTreeTrigger != null &&
                fitTreeTrigger.triggers.Any(entry => entry.eventID == EventTriggerType.PointerEnter) &&
                fitTreeTrigger.triggers.Any(entry => entry.eventID == EventTriggerType.PointerExit) &&
                fitTreeTrigger.triggers.Any(entry => entry.eventID == EventTriggerType.Scroll),
            "FIT TREE must show/hide its tooltip and forward wheel zoom to the tree.");

        ActiveSkillTreeView treeView = activeSkillScreen.GetComponentInChildren<ActiveSkillTreeView>(true);
        Expect(treeView != null, "ActiveSkillScreen must contain ActiveSkillTreeView.");
        var serializedTreeView = new SerializedObject(treeView);
        Equal(2f, serializedTreeView.FindProperty("maxZoomScale").floatValue,
            "Tree view max zoom must default to 2.0.");
        Equal(1.1f, serializedTreeView.FindProperty("zoomFactorPerNotch").floatValue,
            "Tree view zoom factor must default to 1.1 per wheel notch.");
        Expect(treeView.GetComponent<UnityEngine.UI.ScrollRect>() == null,
            "ActiveSkillTreeView must not use ScrollRect for pan and zoom.");

        ActiveSkillNodeDetailPanel detailPanel =
            activeSkillScreen.GetComponentInChildren<ActiveSkillNodeDetailPanel>(true);
        Expect(detailPanel != null && detailPanel.gameObject.activeSelf,
            "The node detail drawer must remain active so it can animate on screen.");
        var serializedDetailPanel = new SerializedObject(detailPanel);
        Equal(treeView, serializedDetailPanel.FindProperty("treeView")?.objectReferenceValue,
            "The detail drawer must notify the tree view while resizing its viewport.");
        Equal(treeView.transform as RectTransform,
            serializedDetailPanel.FindProperty("treeViewport")?.objectReferenceValue,
            "The detail drawer must resize the active tree viewport.");
        var closeButton = serializedDetailPanel.FindProperty("closeButton")?.objectReferenceValue
            as UnityEngine.UI.Button;
        Expect(closeButton != null && closeButton.name == "CloseButton",
            "The node detail drawer must expose a close button.");
        Approximately(440f, serializedDetailPanel.FindProperty("drawerWidth").floatValue,
            "The node detail drawer must reserve 440 logical pixels.");
        Approximately(0.2f, serializedDetailPanel.FindProperty("transitionDuration").floatValue,
            "The node detail drawer transition must take 0.2 seconds.");
    }

    static void TestTreeNavigationMath()
    {
        MethodInfo zoomMethod = typeof(ActiveSkillTreeView).GetMethod(
            "CalculateZoomedPosition",
            BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo clampMethod = typeof(ActiveSkillTreeView).GetMethod(
            "CalculateClampedPan",
            BindingFlags.NonPublic | BindingFlags.Static);
        Expect(zoomMethod != null && clampMethod != null,
            "ActiveSkillTreeView navigation math helpers must remain testable.");

        Vector2 currentPosition = new(30f, -10f);
        Vector2 pointer = new(100f, 50f);
        const float oldScale = 1f;
        const float newScale = 1.5f;
        Vector2 contentPoint = (pointer - currentPosition) / oldScale;
        Vector2 zoomedPosition = (Vector2)zoomMethod.Invoke(
            null,
            new object[] { currentPosition, pointer, oldScale, newScale });
        Vector2 pointerAfterZoom = zoomedPosition + contentPoint * newScale;
        Approximately(pointer.x, pointerAfterZoom.x,
            "Zooming must keep the horizontal content point under the pointer.");
        Approximately(pointer.y, pointerAfterZoom.y,
            "Zooming must keep the vertical content point under the pointer.");

        Vector2 centered = (Vector2)clampMethod.Invoke(
            null,
            new object[] { new Vector2(50f, -50f), new Vector2(500f, 300f), new Vector2(800f, 600f), 1f });
        Equal(Vector2.zero, centered,
            "Graph axes smaller than the viewport must remain centered.");

        Vector2 clamped = (Vector2)clampMethod.Invoke(
            null,
            new object[] { new Vector2(500f, -500f), new Vector2(1000f, 900f), new Vector2(800f, 600f), 1f });
        Equal(new Vector2(100f, -150f), clamped,
            "Pan must hard-clamp to the scaled graph bounds.");
    }

    static void TestVisualScaleValidationAndBounds(List<ScriptableObject> assets)
    {
        SkillUpgradeNodeData first = Node("first", 1);
        SkillUpgradeNodeData second = Node("second", 1);
        first.uiPosition = Vector2.zero;
        second.uiPosition = new Vector2(80f, 0f);
        SkillUpgradeTreeDefinition tree = CreateTree(assets, "tree.visual-validation", first, second);

        first.visualScale = 0.5f;
        List<SkillUpgradeValidationIssue> issues = SkillUpgradeTreeValidator.Validate(tree);
        Expect(issues.Any(issue =>
                issue.Severity == SkillUpgradeValidationSeverity.Error &&
                issue.Message.Contains("visual scale", StringComparison.OrdinalIgnoreCase)),
            "Validator must reject an authored visual scale outside the supported range.");
        Expect(issues.Any(issue =>
                issue.Severity == SkillUpgradeValidationSeverity.Warning &&
                issue.Message.Contains("overlap", StringComparison.OrdinalIgnoreCase)),
            "Validator must warn when runtime node bounds overlap.");

        first.visualScale = 2f;
        second.visualScale = 2f;
        first.uiPosition = new Vector2(0f, -420f);
        second.uiPosition = new Vector2(0f, 420f);
        issues = SkillUpgradeTreeValidator.Validate(tree);
        Expect(!issues.Any(issue => issue.Message.Contains("overlap", StringComparison.OrdinalIgnoreCase)),
            "Separated runtime node bounds must not report an overlap.");
        Expect(issues.Any(issue =>
                issue.Severity == SkillUpgradeValidationSeverity.Warning &&
                issue.Message.Contains("auto-fit scale", StringComparison.OrdinalIgnoreCase)),
            "Readable bounds validation must include scaled node extents.");
    }

    static CharacterProgressData InitializedData(int points)
    {
        return new CharacterProgressData
        {
            level = 10,
            activeSkillPoints = points,
            activeSkillProgressInitialized = true,
        };
    }

    static SkillUpgradeTreeDefinition CreateTree(
        List<ScriptableObject> assets,
        string treeId,
        params SkillUpgradeNodeData[] nodes)
    {
        SkillUpgradeTreeDefinition tree = ScriptableObject.CreateInstance<SkillUpgradeTreeDefinition>();
        tree.treeId = treeId;
        tree.nodes = new List<SkillUpgradeNodeData>(nodes);
        assets.Add(tree);
        return tree;
    }

    static SkillUpgradeNodeData Node(string nodeId, int cost, params string[] prerequisites)
    {
        return new SkillUpgradeNodeData
        {
            nodeId = nodeId,
            cost = cost,
            requiredCharacterLevel = 1,
            requiredNodeIds = new List<string>(prerequisites),
        };
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

    static void Approximately(float expected, float actual, string message)
    {
        if (!Mathf.Approximately(expected, actual))
            throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
    }

    static Rect GetAuthoredRect(VisualElement element)
    {
        if (element.userData is Rect authoredRect)
            return authoredRect;

        return new Rect(
            element.style.left.value.value,
            element.style.top.value.value,
            element.style.width.value.value,
            element.style.height.value.value);
    }
}
