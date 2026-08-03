using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class InteractionIndicatorPrefabBuilder
{
    const string IndicatorPrefabPath = "Assets/Prefab/User Interface/InteractionIndicator.prefab";
    const string PlayerUIPrefabPath = "Assets/Prefab/User Interface/PlayerUI.prefab";

    [MenuItem("Tools/RB/UI/Build Interaction Indicator Prefab")]
    public static void BuildFromMenu()
    {
        Build();
    }

    public static void BuildFromCommandLine()
    {
        Build();
    }

    static void Build()
    {
        int worldUiLayer = LayerMask.NameToLayer("WorldUI");
        if (worldUiLayer < 0)
            throw new System.InvalidOperationException("The project is missing the WorldUI layer.");

        InteractionIndicatorView indicatorPrefab = BuildIndicatorPrefab(worldUiLayer);
        BindPlayerUIPrefab(indicatorPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[InteractionIndicator] Built '{IndicatorPrefabPath}' and bound it to PlayerUI.");
    }

    static InteractionIndicatorView BuildIndicatorPrefab(int worldUiLayer)
    {
        GameObject root = new(
            "InteractionIndicator",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup),
            typeof(InteractionIndicatorView));

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(220f, 150f);
        rootRect.localScale = Vector3.one * 0.002f;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 10f;

        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform visualRoot = CreateRectObject("Visual", root.transform);
        Stretch(visualRoot);

        Sprite circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        if (circleSprite == null)
            circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        Image backgroundRing = CreateCircle(
            "BackgroundRing",
            visualRoot,
            circleSprite,
            new Color(0.02f, 0.03f, 0.04f, 0.78f),
            104f,
            new Vector2(0f, 18f));

        Image progressRing = CreateCircle(
            "ProgressRing",
            visualRoot,
            circleSprite,
            new Color(0.05f, 0.9f, 0.86f, 1f),
            104f,
            new Vector2(0f, 18f));
        progressRing.type = Image.Type.Filled;
        progressRing.fillMethod = Image.FillMethod.Radial360;
        progressRing.fillOrigin = (int)Image.Origin360.Top;
        progressRing.fillClockwise = true;
        progressRing.fillAmount = 0f;

        CreateCircle(
            "InnerCircle",
            visualRoot,
            circleSprite,
            new Color(0.015f, 0.02f, 0.025f, 0.94f),
            78f,
            new Vector2(0f, 18f));

        TMP_Text keyLabel = CreateText(
            "BindingLabel",
            visualRoot,
            "F",
            38f,
            TextAlignmentOptions.Center);
        keyLabel.fontStyle = FontStyles.Bold;
        keyLabel.rectTransform.sizeDelta = new Vector2(74f, 54f);
        keyLabel.rectTransform.anchoredPosition = new Vector2(0f, 18f);

        TMP_Text actionLabel = CreateText(
            "ActionLabel",
            visualRoot,
            "Interact",
            24f,
            TextAlignmentOptions.Center);
        actionLabel.rectTransform.sizeDelta = new Vector2(210f, 34f);
        actionLabel.rectTransform.anchoredPosition = new Vector2(0f, -57f);
        actionLabel.enableAutoSizing = true;
        actionLabel.fontSizeMin = 16f;
        actionLabel.fontSizeMax = 24f;

        InteractionIndicatorView view = root.GetComponent<InteractionIndicatorView>();
        SetObject(view, "canvasGroup", canvasGroup);
        SetObject(view, "visualRoot", visualRoot);
        SetObject(view, "progressRing", progressRing);
        SetObject(view, "keyLabel", keyLabel);
        SetObject(view, "actionLabel", actionLabel);

        SetLayerRecursively(root, worldUiLayer);
        backgroundRing.raycastTarget = false;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, IndicatorPrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<InteractionIndicatorView>();
    }

    static void BindPlayerUIPrefab(InteractionIndicatorView indicatorPrefab)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerUIPrefabPath);
        try
        {
            InteractionIndicatorPresenter presenter = contents.GetComponent<InteractionIndicatorPresenter>();
            if (presenter == null)
                presenter = contents.AddComponent<InteractionIndicatorPresenter>();

            SetObject(presenter, "indicatorPrefab", indicatorPrefab);
            PrefabUtility.SaveAsPrefabAsset(contents, PlayerUIPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    static RectTransform CreateRectObject(string name, Transform parent)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    static Image CreateCircle(
        string name,
        Transform parent,
        Sprite sprite,
        Color color,
        float size,
        Vector2 anchoredPosition)
    {
        RectTransform rect = CreateRectObject(name, parent);
        rect.sizeDelta = Vector2.one * size;
        rect.anchoredPosition = anchoredPosition;

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    static TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRectObject(name, parent);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    static void SetObject(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new System.InvalidOperationException(
                $"Missing serialized field '{propertyName}' on {target.GetType().Name}.");
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        for (int i = 0; i < root.transform.childCount; i++)
            SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
    }
}
