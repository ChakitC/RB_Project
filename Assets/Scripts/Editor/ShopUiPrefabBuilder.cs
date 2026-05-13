using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ShopUiPrefabBuilder
{
    const string PrefabFolder = "Assets/Prefab/User Interface/Shop";
    const string RowPrefabPath = PrefabFolder + "/ShopItemRow_Starter.prefab";
    const string PanelPrefabPath = PrefabFolder + "/ShopPanel_Starter.prefab";

    [MenuItem("Tools/Shop/Create Starter UI Prefabs")]
    public static void CreateStarterPrefabs()
    {
        EnsureFolder("Assets", "Prefab");
        EnsureFolder("Assets/Prefab", "User Interface");
        EnsureFolder("Assets/Prefab/User Interface", "Shop");

        var rowPrefab = CreateRowPrefab();
        var panelPrefab = CreatePanelPrefab(rowPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = panelPrefab;
        Debug.Log($"Created shop starter UI prefabs:\n{PanelPrefabPath}\n{RowPrefabPath}");
    }

    static ShopItemRowUI CreateRowPrefab()
    {
        var root = CreateUiObject("ShopItemRow_Starter", typeof(Image), typeof(HorizontalLayoutGroup), typeof(ShopItemRowUI));
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(760f, 72f);

        var background = root.GetComponent<Image>();
        background.color = new Color32(31, 36, 38, 245);

        var layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        var icon = CreateImage("Icon", root.transform, new Color32(52, 60, 64, 255));
        SetLayout(icon.gameObject, 56f, 56f);

        var nameText = CreateText("NameText", root.transform, "Item Name", 20f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetLayout(nameText.gameObject, 220f, 0f, 1f);

        var quantityText = CreateText("QuantityText", root.transform, "x1", 16f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
        SetLayout(quantityText.gameObject, 54f);

        var priceText = CreateText("PriceText", root.transform, "100", 18f, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        SetLayout(priceText.gameObject, 90f);

        var stockText = CreateText("StockText", root.transform, "Stock: Unlimited", 14f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetLayout(stockText.gameObject, 120f);

        var reasonText = CreateText("ReasonText", root.transform, "", 13f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        reasonText.color = new Color32(255, 175, 110, 255);
        SetLayout(reasonText.gameObject, 135f);

        var buyButton = CreateButton("BuyButton", root.transform, "Buy", 74f, 44f);

        var row = root.GetComponent<ShopItemRowUI>();
        SetReference(row, "iconImage", icon);
        SetReference(row, "nameText", nameText);
        SetReference(row, "quantityText", quantityText);
        SetReference(row, "priceText", priceText);
        SetReference(row, "stockText", stockText);
        SetReference(row, "reasonText", reasonText);
        SetReference(row, "buyButton", buyButton);

        var saved = PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
        Object.DestroyImmediate(root);
        return saved != null ? saved.GetComponent<ShopItemRowUI>() : null;
    }

    static GameObject CreatePanelPrefab(ShopItemRowUI rowPrefab)
    {
        var root = CreateUiObject("ShopPanel_Starter", typeof(Image), typeof(VerticalLayoutGroup), typeof(ShopService), typeof(ShopPanelUI));
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(900f, 640f);

        root.GetComponent<Image>().color = new Color32(20, 24, 27, 245);

        var rootLayout = root.GetComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(18, 18, 16, 16);
        rootLayout.spacing = 12f;
        rootLayout.childAlignment = TextAnchor.UpperCenter;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        var header = CreateUiObject("Header", typeof(HorizontalLayoutGroup));
        header.transform.SetParent(root.transform, false);
        SetLayout(header, 0f, 54f, 1f);
        var headerLayout = header.GetComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 10f;
        headerLayout.childAlignment = TextAnchor.MiddleCenter;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;

        var titleText = CreateText("TitleText", header.transform, "SHOP", 28f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetLayout(titleText.gameObject, 0f, 0f, 1f);

        var goldLabel = CreateText("GoldLabel", header.transform, "Gold", 16f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
        goldLabel.color = new Color32(210, 220, 220, 255);
        SetLayout(goldLabel.gameObject, 54f);

        var goldText = CreateText("GoldText", header.transform, "0", 22f, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        goldText.color = new Color32(255, 214, 102, 255);
        SetLayout(goldText.gameObject, 120f);

        var closeButton = CreateButton("CloseButton", header.transform, "X", 42f, 42f);

        var scrollRoot = CreateUiObject("ItemScroll", typeof(Image), typeof(ScrollRect));
        scrollRoot.transform.SetParent(root.transform, false);
        SetLayout(scrollRoot, 0f, 0f, 1f, 1f);
        scrollRoot.GetComponent<Image>().color = new Color32(12, 15, 17, 190);

        var scrollRect = scrollRoot.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 24f;

        var viewport = CreateUiObject("Viewport", typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(scrollRoot.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.GetComponent<Image>().color = new Color32(12, 15, 17, 0);

        var content = CreateUiObject("Content", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        var contentLayout = content.GetComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(8, 8, 8, 8);
        contentLayout.spacing = 8f;
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;

        var emptyState = CreateText("EmptyState", viewport.transform, "No items", 18f, FontStyles.Normal, TextAlignmentOptions.Center);
        Stretch(emptyState.rectTransform);
        emptyState.color = new Color32(170, 180, 180, 255);
        emptyState.gameObject.SetActive(false);

        var messageText = CreateText("MessageText", root.transform, "", 15f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        messageText.color = new Color32(255, 175, 110, 255);
        SetLayout(messageText.gameObject, 0f, 28f, 1f);

        var panel = root.GetComponent<ShopPanelUI>();
        SetReference(panel, "shopService", root.GetComponent<ShopService>());
        SetReference(panel, "rowContainer", contentRect);
        SetReference(panel, "rowPrefab", rowPrefab);
        SetReference(panel, "goldText", goldText);
        SetReference(panel, "messageText", messageText);
        SetReference(panel, "emptyState", emptyState.gameObject);
        SetReference(panel, "closeButton", closeButton);

        var saved = PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    static GameObject CreateUiObject(string name, params System.Type[] components)
    {
        var objectComponents = new System.Type[components.Length + 1];
        objectComponents[0] = typeof(RectTransform);
        for (int i = 0; i < components.Length; i++)
            objectComponents[i + 1] = components[i];

        return new GameObject(name, objectComponents);
    }

    static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = CreateUiObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string text,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment)
    {
        var go = CreateUiObject(name, typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = fontStyle;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        return tmp;
    }

    static Button CreateButton(string name, Transform parent, string label, float width, float height)
    {
        var go = CreateUiObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        SetLayout(go, width, height);

        var image = go.GetComponent<Image>();
        image.color = new Color32(68, 105, 92, 255);

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color32(68, 105, 92, 255);
        colors.highlightedColor = new Color32(84, 132, 115, 255);
        colors.pressedColor = new Color32(45, 78, 67, 255);
        colors.disabledColor = new Color32(43, 48, 49, 160);
        button.colors = colors;

        var labelText = CreateText("Label", go.transform, label, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(labelText.rectTransform);

        return button;
    }

    static void SetLayout(GameObject go, float preferredWidth, float preferredHeight = 0f, float flexibleWidth = 0f, float flexibleHeight = 0f)
    {
        var layout = go.GetComponent<LayoutElement>();
        if (layout == null)
            layout = go.AddComponent<LayoutElement>();

        if (preferredWidth > 0f)
            layout.preferredWidth = preferredWidth;

        if (preferredHeight > 0f)
            layout.preferredHeight = preferredHeight;

        layout.flexibleWidth = flexibleWidth;
        layout.flexibleHeight = flexibleHeight;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    static void EnsureFolder(string parent, string folder)
    {
        var path = $"{parent}/{folder}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folder);
    }

    static void SetReference(Object target, string propertyName, Object value)
    {
        var serializedObject = new SerializedObject(target);
        var property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning($"Missing serialized field '{propertyName}' on {target}.");
            return;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
