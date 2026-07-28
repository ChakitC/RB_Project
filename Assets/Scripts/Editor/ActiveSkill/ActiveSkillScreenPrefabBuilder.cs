using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ActiveSkillScreenPrefabBuilder
{
    const int PlaceholderLayoutVersion = 4;
    const string PrefabFolder = "Assets/Prefab/User Interface/Active Skill";
    const string ThemeFolder = "Assets/UI/Active Skill";
    const string ThemePath = ThemeFolder + "/SkillScreenTheme.asset";
    const string ScreenPath = PrefabFolder + "/ActiveSkillScreen.prefab";
    const string SlotTabPath = PrefabFolder + "/ActiveSkillSlotTab.prefab";
    const string VariantCardPath = PrefabFolder + "/ActiveSkillVariantCard.prefab";
    const string NodePath = PrefabFolder + "/ActiveSkillUpgradeNode.prefab";
    const string ConnectionPath = PrefabFolder + "/ActiveSkillTreeConnection.prefab";

    [InitializeOnLoadMethod]
    static void EnsureInitialPlaceholderAssets()
    {
        if (Application.isBatchMode)
            return;

        EditorApplication.delayCall += () =>
        {
            GameObject screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
            ActiveSkillScreenController controller = screen != null
                ? screen.GetComponent<ActiveSkillScreenController>()
                : null;
            if (controller == null || controller.PlaceholderLayoutVersion < PlaceholderLayoutVersion)
                Build();
        };
    }

    [MenuItem("Tools/RB/Skills/Build Active Skill Placeholder Prefabs")]
    public static void BuildFromMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Build Active Skill UI",
                "This rebuilds the placeholder Active Skill prefabs. The theme asset is preserved if it already exists.",
                "Build",
                "Cancel"))
        {
            return;
        }

        Build();
    }

    public static void BuildFromCommandLine()
    {
        Build();
    }

    static void Build()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(ThemeFolder);

        SkillScreenTheme theme = AssetDatabase.LoadAssetAtPath<SkillScreenTheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<SkillScreenTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        ActiveSkillSlotTabView slotTabPrefab = BuildSlotTabPrefab();
        ActiveSkillVariantCardView variantPrefab = BuildVariantCardPrefab();
        ActiveSkillUpgradeNodeView nodePrefab = BuildNodePrefab();
        ActiveSkillTreeConnectionView connectionPrefab = BuildConnectionPrefab();
        BuildScreenPrefab(theme, slotTabPrefab, variantPrefab, nodePrefab, connectionPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ActiveSkillFeatureSmokeTests.RunFromCommandLine();
        Debug.Log($"[ActiveSkillScreen] Placeholder prefabs built at {PrefabFolder}.");
    }

    static ActiveSkillSlotTabView BuildSlotTabPrefab()
    {
        GameObject root = CreateRectObject("ActiveSkillSlotTab", null);
        SetSize(root, 220f, 80f);
        Image background = root.AddComponent<Image>();
        background.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
        Button button = root.AddComponent<Button>();
        TMP_Text label = CreateText("Label", root.transform, "SKILL I", 30f, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);

        Image selected = CreateImage("Selected", root.transform, new Color(0f, 0.85f, 1f, 1f));
        selected.raycastTarget = false;
        SetAnchors(selected.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f));
        selected.rectTransform.sizeDelta = new Vector2(0f, 6f);
        selected.rectTransform.anchoredPosition = new Vector2(0f, 3f);

        ActiveSkillSlotTabView view = root.AddComponent<ActiveSkillSlotTabView>();
        SetObject(view, "button", button);
        SetObject(view, "background", background);
        SetObject(view, "label", label);
        SetObject(view, "selectedMarker", selected.gameObject);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SlotTabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<ActiveSkillSlotTabView>();
    }

    static ActiveSkillVariantCardView BuildVariantCardPrefab()
    {
        GameObject root = CreateRectObject("ActiveSkillVariantCard", null);
        SetSize(root, 310f, 190f);
        Image frame = root.AddComponent<Image>();
        frame.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        Button button = root.AddComponent<Button>();

        Image icon = CreateImage("Icon", root.transform, Color.white);
        SetAnchors(icon.rectTransform, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.95f));
        icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
        icon.preserveAspect = true;

        Image titleBackground = CreateImage("TitleBackground", root.transform, new Color(0.05f, 0.05f, 0.05f, 0.9f));
        SetAnchors(titleBackground.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.22f));
        titleBackground.rectTransform.offsetMin = titleBackground.rectTransform.offsetMax = Vector2.zero;
        TMP_Text title = CreateText("Title", titleBackground.transform, "Skill Variant", 22f, TextAlignmentOptions.Center);
        Stretch(title.rectTransform);

        Image selected = CreateImage("Selected", root.transform, new Color(0f, 0.85f, 1f, 0.35f));
        Stretch(selected.rectTransform, 5f);
        selected.raycastTarget = false;

        ActiveSkillVariantCardView view = root.AddComponent<ActiveSkillVariantCardView>();
        SetObject(view, "button", button);
        SetObject(view, "icon", icon);
        SetObject(view, "frame", frame);
        SetObject(view, "title", title);
        SetObject(view, "selectedMarker", selected.gameObject);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, VariantCardPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<ActiveSkillVariantCardView>();
    }

    static ActiveSkillUpgradeNodeView BuildNodePrefab()
    {
        GameObject root = CreateRectObject("ActiveSkillUpgradeNode", null);
        SetSize(root, 96f, 96f);
        Image frame = root.AddComponent<Image>();
        frame.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        Button button = root.AddComponent<Button>();

        Image glow = CreateImage("AvailableGlow", root.transform, new Color(1f, 0.72f, 0.18f, 0.3f));
        Stretch(glow.rectTransform, -10f);
        glow.raycastTarget = false;

        Image icon = CreateImage("Icon", root.transform, Color.white);
        SetAnchors(icon.rectTransform, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));
        icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
        icon.preserveAspect = true;

        TMP_Text cost = CreateText("Cost", root.transform, "1", 18f, TextAlignmentOptions.Center);
        SetAnchors(cost.rectTransform, new Vector2(0.68f, 0f), new Vector2(1f, 0.32f));
        cost.rectTransform.offsetMin = cost.rectTransform.offsetMax = Vector2.zero;

        TMP_Text unlocked = CreateText("Unlocked", root.transform, "✓", 32f, TextAlignmentOptions.Center);
        SetAnchors(unlocked.rectTransform, new Vector2(0.62f, 0.62f), Vector2.one);
        unlocked.rectTransform.offsetMin = unlocked.rectTransform.offsetMax = Vector2.zero;
        unlocked.color = new Color(0.3f, 1f, 0.4f, 1f);

        Image selected = CreateImage("Selected", root.transform, new Color(0f, 0.85f, 1f, 0.45f));
        Stretch(selected.rectTransform, 4f);
        selected.raycastTarget = false;

        ActiveSkillUpgradeNodeView view = root.AddComponent<ActiveSkillUpgradeNodeView>();
        SetObject(view, "button", button);
        SetObject(view, "icon", icon);
        SetObject(view, "frame", frame);
        SetObject(view, "costText", cost);
        SetObject(view, "availableGlow", glow.gameObject);
        SetObject(view, "unlockedMarker", unlocked.gameObject);
        SetObject(view, "selectedMarker", selected.gameObject);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, NodePath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<ActiveSkillUpgradeNodeView>();
    }

    static ActiveSkillTreeConnectionView BuildConnectionPrefab()
    {
        GameObject root = CreateRectObject("ActiveSkillTreeConnection", null);
        SetSize(root, 100f, 6f);
        Image image = root.AddComponent<Image>();
        image.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        image.raycastTarget = false;
        ActiveSkillTreeConnectionView view = root.AddComponent<ActiveSkillTreeConnectionView>();
        SetObject(view, "image", image);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ConnectionPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<ActiveSkillTreeConnectionView>();
    }

    static void BuildScreenPrefab(
        SkillScreenTheme theme,
        ActiveSkillSlotTabView slotTabPrefab,
        ActiveSkillVariantCardView variantPrefab,
        ActiveSkillUpgradeNodeView nodePrefab,
        ActiveSkillTreeConnectionView connectionPrefab)
    {
        GameObject root = new("ActiveSkillScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(ActiveSkillScreenController));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.localScale = Vector3.one;
        rootRect.sizeDelta = new Vector2(1920f, 1080f);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image background = CreateImage("Background", root.transform, new Color(0.12f, 0.12f, 0.12f, 0.98f));
        Stretch(background.rectTransform);
        if (theme.screenBackground != null)
            background.sprite = theme.screenBackground;

        Button backButton = CreateButton("BackButton", root.transform, "BACK", 34f, new Color(0.55f, 0.47f, 0.27f, 1f));
        SetAnchors(backButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f));
        SetSize(backButton.gameObject, 240f, 90f);
        backButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(145f, -65f);

        TMP_Text points = CreateText("Points", root.transform, "Active Skill Points: 0", 28f, TextAlignmentOptions.Right);
        SetAnchors(points.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f));
        points.rectTransform.sizeDelta = new Vector2(420f, 60f);
        points.rectTransform.anchoredPosition = new Vector2(-250f, -55f);

        Button resetButton = CreateButton("ResetTreeButton", root.transform, "RESET TREE", 22f, new Color(0.42f, 0.18f, 0.16f, 1f));
        SetAnchors(resetButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f));
        SetSize(resetButton.gameObject, 180f, 54f);
        resetButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-115f, -110f);

        RectTransform variantPanel = CreatePanel("VariantPanel", root.transform, new Color(0.14f, 0.14f, 0.14f, 0.95f));
        SetAnchors(variantPanel, new Vector2(0f, 0f), new Vector2(0f, 1f));
        variantPanel.offsetMin = new Vector2(20f, 150f);
        variantPanel.offsetMax = new Vector2(380f, -145f);

        GameObject selectedVariantObject = (GameObject)PrefabUtility.InstantiatePrefab(variantPrefab.gameObject, variantPanel);
        selectedVariantObject.name = "SelectedVariantCard";
        RectTransform selectedVariantRect = selectedVariantObject.GetComponent<RectTransform>();
        SetAnchors(selectedVariantRect, new Vector2(0f, 1f), new Vector2(1f, 1f));
        selectedVariantRect.offsetMin = new Vector2(20f, -290f);
        selectedVariantRect.offsetMax = new Vector2(-20f, -20f);

        ScrollRect variantScroll = CreateScrollView("VariantScroll", variantPanel, false, true, out RectTransform variantContent);
        RectTransform variantScrollRect = variantScroll.GetComponent<RectTransform>();
        SetAnchors(variantScrollRect, Vector2.zero, Vector2.one);
        variantScrollRect.offsetMin = new Vector2(20f, 20f);
        variantScrollRect.offsetMax = new Vector2(-20f, -310f);
        var variantLayout = variantContent.gameObject.AddComponent<VerticalLayoutGroup>();
        variantLayout.spacing = 18f;
        variantLayout.childAlignment = TextAnchor.UpperCenter;
        variantLayout.childControlHeight = false;
        variantLayout.childControlWidth = true;
        variantContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform treePanel = CreatePanel("TreePanel", root.transform, new Color(0.16f, 0.16f, 0.16f, 0.95f));
        SetAnchors(treePanel, Vector2.zero, Vector2.one);
        treePanel.offsetMin = new Vector2(400f, 150f);
        treePanel.offsetMax = new Vector2(-20f, -145f);

        GameObject viewportObject = CreateRectObject("TreeViewport", treePanel);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        Stretch(viewport, 18f);
        viewportObject.AddComponent<RectMask2D>();
        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(0.08f, 0.08f, 0.08f, 0.75f);

        GameObject contentObject = CreateRectObject("Content", viewport);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        SetAnchors(content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        content.sizeDelta = new Vector2(1600f, 900f);
        RectTransform connectionRoot = CreateRectObject("Connections", content).GetComponent<RectTransform>();
        Stretch(connectionRoot);
        RectTransform nodeRoot = CreateRectObject("Nodes", content).GetComponent<RectTransform>();
        Stretch(nodeRoot);

        ActiveSkillTreeView treeView = viewportObject.AddComponent<ActiveSkillTreeView>();
        SetObject(treeView, "viewport", viewport);
        SetObject(treeView, "contentRoot", content);
        SetObject(treeView, "connectionRoot", connectionRoot);
        SetObject(treeView, "nodeRoot", nodeRoot);
        SetObject(treeView, "nodePrefab", nodePrefab);
        SetObject(treeView, "connectionPrefab", connectionPrefab);
        SetObject(treeView, "theme", theme);

        Button fitTreeButton = CreateButton(
            "FitTreeButton",
            viewport,
            "FIT TREE",
            18f,
            new Color(0.24f, 0.36f, 0.4f, 0.96f));
        RectTransform fitTreeButtonRect = fitTreeButton.GetComponent<RectTransform>();
        SetAnchors(fitTreeButtonRect, new Vector2(0f, 1f), new Vector2(0f, 1f));
        SetSize(fitTreeButton.gameObject, 150f, 48f);
        fitTreeButtonRect.anchoredPosition = new Vector2(87f, -37f);

        RectTransform navigationTooltipRoot = CreatePanel(
            "TreeNavigationTooltip",
            viewport,
            new Color(0.04f, 0.04f, 0.04f, 0.94f));
        SetAnchors(navigationTooltipRoot, new Vector2(0f, 1f), new Vector2(0f, 1f));
        navigationTooltipRoot.sizeDelta = new Vector2(250f, 72f);
        navigationTooltipRoot.anchoredPosition = new Vector2(137f, -105f);
        navigationTooltipRoot.GetComponent<Image>().raycastTarget = false;
        TMP_Text navigationTooltipText = CreateText(
            "Text",
            navigationTooltipRoot,
            "Scroll: Zoom\nDrag Background: Pan",
            17f,
            TextAlignmentOptions.Center);
        Stretch(navigationTooltipText.rectTransform, 8f);
        navigationTooltipRoot.gameObject.SetActive(false);

        TMP_Text emptyState = CreateText("EmptyState", treePanel, "No Active Skill Tree assigned.", 32f, TextAlignmentOptions.Center);
        Stretch(emptyState.rectTransform, 80f);

        ActiveSkillNodeDetailPanel detail = BuildDetailPanel(treePanel, viewport, treeView);
        ActiveSkillConfirmationDialog confirmation = BuildConfirmationDialog(root.transform);

        ScrollRect slotScroll = CreateScrollView("SlotTabs", root.transform, true, false, out RectTransform slotContent);
        RectTransform slotScrollRect = slotScroll.GetComponent<RectTransform>();
        SetAnchors(slotScrollRect, new Vector2(0.2f, 0f), new Vector2(0.85f, 0f));
        slotScrollRect.sizeDelta = new Vector2(0f, 110f);
        slotScrollRect.anchoredPosition = new Vector2(0f, 65f);
        var slotLayout = slotContent.gameObject.AddComponent<HorizontalLayoutGroup>();
        slotLayout.spacing = 24f;
        slotLayout.childAlignment = TextAnchor.MiddleCenter;
        slotLayout.childControlHeight = false;
        slotLayout.childControlWidth = false;
        slotContent.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        ActiveSkillScreenController controller = root.GetComponent<ActiveSkillScreenController>();
        SetInt(controller, "placeholderLayoutVersion", PlaceholderLayoutVersion);
        SetObject(controller, "screenGroup", root.GetComponent<CanvasGroup>());
        SetObject(controller, "backButton", backButton);
        SetObject(controller, "pointsText", points);
        SetObject(controller, "emptyStateText", emptyState);
        SetObject(controller, "resetTreeButton", resetButton);
        SetObject(controller, "theme", theme);
        SetObject(controller, "slotTabRoot", slotContent);
        SetObject(controller, "slotTabPrefab", slotTabPrefab);
        SetObject(controller, "selectedVariantCard", selectedVariantObject.GetComponent<ActiveSkillVariantCardView>());
        SetObject(controller, "variantListRoot", variantContent);
        SetObject(controller, "variantCardPrefab", variantPrefab);
        SetObject(controller, "treeView", treeView);
        SetObject(controller, "fitTreeButton", fitTreeButton);
        SetObject(controller, "treeNavigationTooltip", navigationTooltipRoot.gameObject);
        SetObject(controller, "detailPanel", detail);
        SetObject(controller, "confirmationDialog", confirmation);

        EventTrigger fitTreeTrigger = fitTreeButton.gameObject.AddComponent<EventTrigger>();
        var pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        UnityEventTools.AddPersistentListener(pointerEnter.callback, controller.ShowTreeNavigationTooltip);
        var pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        UnityEventTools.AddPersistentListener(pointerExit.callback, controller.HideTreeNavigationTooltip);
        var scroll = new EventTrigger.Entry { eventID = EventTriggerType.Scroll };
        UnityEventTools.AddPersistentListener(scroll.callback, controller.HandleTreeNavigationScroll);
        fitTreeTrigger.triggers = new List<EventTrigger.Entry> { pointerEnter, pointerExit, scroll };

        rootRect.localScale = Vector3.one;
        PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
        Object.DestroyImmediate(root);

        GameObject savedRoot = PrefabUtility.LoadPrefabContents(ScreenPath);
        savedRoot.transform.localScale = Vector3.one;
        Canvas savedCanvas = savedRoot.GetComponent<Canvas>();
        savedCanvas.overrideSorting = true;
        savedCanvas.sortingOrder = 100;
        PrefabUtility.SaveAsPrefabAsset(savedRoot, ScreenPath);
        PrefabUtility.UnloadPrefabContents(savedRoot);
    }

    static ActiveSkillNodeDetailPanel BuildDetailPanel(
        Transform parent,
        RectTransform treeViewport,
        ActiveSkillTreeView treeView)
    {
        RectTransform panel = CreatePanel("NodeDetailPanel", parent, new Color(0.08f, 0.08f, 0.08f, 0.96f));
        SetAnchors(panel, new Vector2(1f, 0f), new Vector2(1f, 0f));
        panel.sizeDelta = new Vector2(440f, 520f);
        panel.anchoredPosition = new Vector2(238f, 280f);
        CanvasGroup drawerGroup = panel.gameObject.AddComponent<CanvasGroup>();
        drawerGroup.interactable = false;
        drawerGroup.blocksRaycasts = false;

        TMP_Text title = CreateText("Title", panel, "Upgrade Node", 28f, TextAlignmentOptions.TopLeft);
        SetTopRect(title.rectTransform, 20f, -18f, -70f, 48f);
        Button close = CreateButton("CloseButton", panel, "X", 22f, new Color(0.22f, 0.22f, 0.22f, 1f));
        RectTransform closeRect = close.GetComponent<RectTransform>();
        SetAnchors(closeRect, Vector2.one, Vector2.one);
        closeRect.sizeDelta = new Vector2(44f, 44f);
        closeRect.anchoredPosition = new Vector2(-30f, -30f);
        TMP_Text description = CreateText("Description", panel, "Description", 19f, TextAlignmentOptions.TopLeft);
        SetTopRect(description.rectTransform, 20f, -72f, -20f, 70f);
        TMP_Text requirements = CreateText("Requirements", panel, "Requirements", 17f, TextAlignmentOptions.TopLeft);
        SetTopRect(requirements.rectTransform, 20f, -148f, -20f, 64f);
        TMP_Text preview = CreateText("StatPreview", panel, "Stat preview", 18f, TextAlignmentOptions.TopLeft);
        SetTopRect(preview.rectTransform, 20f, -218f, -20f, 200f);
        Button unlock = CreateButton("UnlockButton", panel, "UNLOCK", 22f, new Color(0.65f, 0.43f, 0.12f, 1f));
        RectTransform unlockRect = unlock.GetComponent<RectTransform>();
        SetAnchors(unlockRect, new Vector2(0.15f, 0f), new Vector2(0.85f, 0f));
        unlockRect.sizeDelta = new Vector2(0f, 54f);
        unlockRect.anchoredPosition = new Vector2(0f, 38f);

        ActiveSkillNodeDetailPanel detail = panel.gameObject.AddComponent<ActiveSkillNodeDetailPanel>();
        SetObject(detail, "drawerRoot", panel);
        SetObject(detail, "treeViewport", treeViewport);
        SetObject(detail, "treeView", treeView);
        SetObject(detail, "drawerGroup", drawerGroup);
        SetObject(detail, "closeButton", close);
        SetObject(detail, "titleText", title);
        SetObject(detail, "descriptionText", description);
        SetObject(detail, "requirementText", requirements);
        SetObject(detail, "statPreviewText", preview);
        SetObject(detail, "unlockButton", unlock);
        SetObject(detail, "unlockButtonText", unlock.GetComponentInChildren<TMP_Text>());
        return detail;
    }

    static ActiveSkillConfirmationDialog BuildConfirmationDialog(Transform parent)
    {
        RectTransform overlay = CreatePanel("ConfirmationDialog", parent, new Color(0f, 0f, 0f, 0.72f));
        Stretch(overlay);
        RectTransform dialog = CreatePanel("Dialog", overlay, new Color(0.12f, 0.12f, 0.12f, 1f));
        SetAnchors(dialog, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        dialog.sizeDelta = new Vector2(620f, 260f);
        TMP_Text message = CreateText("Message", dialog, "Confirm action?", 24f, TextAlignmentOptions.Center);
        SetAnchors(message.rectTransform, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.9f));
        message.rectTransform.offsetMin = message.rectTransform.offsetMax = Vector2.zero;
        Button confirm = CreateButton("Confirm", dialog, "CONFIRM", 22f, new Color(0.55f, 0.35f, 0.1f, 1f));
        SetAnchors(confirm.GetComponent<RectTransform>(), new Vector2(0.08f, 0.08f), new Vector2(0.46f, 0.3f));
        confirm.GetComponent<RectTransform>().offsetMin = confirm.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        Button cancel = CreateButton("Cancel", dialog, "CANCEL", 22f, new Color(0.25f, 0.25f, 0.25f, 1f));
        SetAnchors(cancel.GetComponent<RectTransform>(), new Vector2(0.54f, 0.08f), new Vector2(0.92f, 0.3f));
        cancel.GetComponent<RectTransform>().offsetMin = cancel.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        ActiveSkillConfirmationDialog component = overlay.gameObject.AddComponent<ActiveSkillConfirmationDialog>();
        SetObject(component, "messageText", message);
        SetObject(component, "confirmButton", confirm);
        SetObject(component, "cancelButton", cancel);
        overlay.gameObject.SetActive(false);
        return component;
    }

    static ScrollRect CreateScrollView(
        string name,
        Transform parent,
        bool horizontal,
        bool vertical,
        out RectTransform content)
    {
        GameObject root = CreateRectObject(name, parent);
        Image rootImage = root.AddComponent<Image>();
        rootImage.color = new Color(0f, 0f, 0f, 0.15f);
        ScrollRect scroll = root.AddComponent<ScrollRect>();
        scroll.horizontal = horizontal;
        scroll.vertical = vertical;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewportObject = CreateRectObject("Viewport", root.transform);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        Stretch(viewport);
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateRectObject("Content", viewport);
        content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = horizontal ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
        content.anchorMax = horizontal ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
        content.pivot = horizontal ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;

        scroll.viewport = viewport;
        scroll.content = content;
        return scroll;
    }

    static GameObject CreateRectObject(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    static RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = CreateRectObject(name, parent);
        Image image = panel.AddComponent<Image>();
        image.color = color;
        return panel.GetComponent<RectTransform>();
    }

    static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject gameObject = CreateRectObject(name, parent);
        Image image = gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    static TMP_Text CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject gameObject = CreateRectObject(name, parent);
        var label = gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    static Button CreateButton(string name, Transform parent, string label, float fontSize, Color color)
    {
        GameObject root = CreateRectObject(name, parent);
        Image image = root.AddComponent<Image>();
        image.color = color;
        Button button = root.AddComponent<Button>();
        TMP_Text text = CreateText("Label", root.transform, label, fontSize, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    static void SetObject(Object target, string propertyName, Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new System.InvalidOperationException($"Missing serialized field '{propertyName}' on {target.GetType().Name}.");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetInt(Object target, string propertyName, int value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new System.InvalidOperationException($"Missing serialized field '{propertyName}' on {target.GetType().Name}.");
        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
    }

    static void SetSize(GameObject gameObject, float width, float height)
    {
        gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
    }

    static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
    }

    static void SetTopRect(RectTransform rect, float left, float top, float right, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, top - height);
        rect.offsetMax = new Vector2(right, top);
    }

    static void EnsureFolder(string path)
    {
        string[] segments = path.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }
}
