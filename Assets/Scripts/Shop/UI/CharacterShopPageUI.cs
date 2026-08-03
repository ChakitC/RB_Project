using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(ShopPanelUI))]
public sealed class CharacterShopPageUI : MonoBehaviour
{
    const string TabsRootName = "CharacterShopTabs";
    const string CharacterPageName = "CharacterScroll";
    const string ConfirmationRootName = "CharacterPurchaseConfirmation";

    static readonly Color32 ActiveTabColor = new(84, 132, 115, 255);
    static readonly Color32 InactiveTabColor = new(48, 58, 59, 255);

    [SerializeField] private CharacterDatabase characterDatabase;

    readonly List<CharacterRow> rows = new();

    ShopPanelUI shopPanel;
    PlayerInventory inventorySource;
    GameObject itemsPage;
    GameObject characterPage;
    RectTransform rowContainer;
    GameObject emptyState;
    Button itemsTabButton;
    Button charactersTabButton;
    GameObject confirmationRoot;
    TMP_Text confirmationText;
    string pendingCharacterId;
    bool showingCharacters;

    void Awake()
    {
        shopPanel = GetComponent<ShopPanelUI>();
        ResolveDatabase();
        EnsureLayout();
        ShowItemsPage();
    }

    void OnEnable()
    {
        CharacterUnlockService.CharacterUnlocked += HandleCharacterUnlocked;
        ShowItemsPage();
        Refresh();
    }

    void OnDisable()
    {
        CharacterUnlockService.CharacterUnlocked -= HandleCharacterUnlocked;
        HideConfirmation();
    }

    public void BindSource(PlayerInventory inventory)
    {
        inventorySource = inventory;
    }

    public void ShowItemsPage()
    {
        EnsureLayout();
        SetPage(showCharacters: false);
    }

    public void ShowCharactersPage()
    {
        EnsureLayout();
        SetPage(showCharacters: true);
        Refresh();
    }

    public void Refresh()
    {
        ResolveDatabase();
        EnsureLayout();

        if (rowContainer == null)
            return;

        IReadOnlyList<CharacterStats> characters = characterDatabase != null
            ? characterDatabase.Characters
            : null;
        int characterCount = characters != null ? characters.Count : 0;

        EnsureRowCount(characterCount);

        for (int i = 0; i < rows.Count; i++)
        {
            CharacterStats character = i < characterCount ? characters[i] : null;
            BindRow(rows[i], character);
        }

        if (emptyState != null)
            emptyState.SetActive(characterCount == 0);
    }

    void ResolveDatabase()
    {
        if (characterDatabase != null)
            return;

        CharacterDatabase[] databases = Resources.FindObjectsOfTypeAll<CharacterDatabase>();
        for (int i = 0; i < databases.Length; i++)
        {
            if (databases[i] == null)
                continue;

            characterDatabase = databases[i];
            break;
        }
    }

    void EnsureLayout()
    {
        if (shopPanel == null)
            shopPanel = GetComponent<ShopPanelUI>();

        if (itemsPage == null)
        {
            Transform itemTransform = transform.Find("ItemScroll");
            if (itemTransform != null)
                itemsPage = itemTransform.gameObject;
        }

        if (characterPage != null && rowContainer != null && confirmationRoot != null)
            return;

        Transform existingTabs = transform.Find(TabsRootName);
        Transform existingPage = transform.Find(CharacterPageName);
        Transform existingConfirmation = transform.Find(ConfirmationRootName);

        if (existingTabs != null || existingPage != null || existingConfirmation != null)
        {
            Debug.LogWarning(
                "[CharacterShopPageUI] The generated character-shop hierarchy is incomplete. " +
                "Remove the generated children and reopen the shop to rebuild it.",
                this);
            return;
        }

        BuildTabs();
        BuildCharacterPage();
        BuildConfirmation();
        ApplySiblingOrder();
    }

    void BuildTabs()
    {
        GameObject tabs = CreateUiObject(TabsRootName, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        tabs.transform.SetParent(transform, false);

        var layout = tabs.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        SetLayout(tabs, 0f, 44f, 1f);

        itemsTabButton = CreateButton("ItemsTabButton", tabs.transform, "ITEMS", 0f, 44f);
        charactersTabButton = CreateButton("CharactersTabButton", tabs.transform, "CHARACTERS", 0f, 44f);
        itemsTabButton.onClick.AddListener(ShowItemsPage);
        charactersTabButton.onClick.AddListener(ShowCharactersPage);
    }

    void BuildCharacterPage()
    {
        characterPage = CreateUiObject(CharacterPageName, typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
        characterPage.transform.SetParent(transform, false);
        SetLayout(characterPage, 0f, 0f, 1f, 1f);
        characterPage.GetComponent<Image>().color = new Color32(12, 15, 17, 190);

        var scrollRect = characterPage.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 24f;

        GameObject viewport = CreateUiObject("Viewport", typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(characterPage.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.GetComponent<Image>().color = new Color32(12, 15, 17, 0);

        GameObject content = CreateUiObject("Content", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        rowContainer = content.GetComponent<RectTransform>();
        rowContainer.anchorMin = new Vector2(0f, 1f);
        rowContainer.anchorMax = new Vector2(1f, 1f);
        rowContainer.pivot = new Vector2(0.5f, 1f);
        rowContainer.anchoredPosition = Vector2.zero;
        rowContainer.sizeDelta = Vector2.zero;

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
        scrollRect.content = rowContainer;

        TMP_Text emptyText = CreateText(
            "EmptyState",
            viewport.transform,
            "No characters configured",
            18f,
            FontStyles.Normal,
            TextAlignmentOptions.Center);
        Stretch(emptyText.rectTransform);
        emptyText.color = new Color32(170, 180, 180, 255);
        emptyState = emptyText.gameObject;
        emptyState.SetActive(false);
    }

    void BuildConfirmation()
    {
        confirmationRoot = CreateUiObject(ConfirmationRootName, typeof(Image), typeof(LayoutElement));
        confirmationRoot.transform.SetParent(transform, false);
        Stretch(confirmationRoot.GetComponent<RectTransform>());
        confirmationRoot.GetComponent<Image>().color = new Color32(0, 0, 0, 190);
        confirmationRoot.GetComponent<LayoutElement>().ignoreLayout = true;

        GameObject card = CreateUiObject("ConfirmationCard", typeof(Image), typeof(VerticalLayoutGroup));
        card.transform.SetParent(confirmationRoot.transform, false);
        var cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(540f, 210f);
        card.GetComponent<Image>().color = new Color32(28, 34, 36, 255);

        var cardLayout = card.GetComponent<VerticalLayoutGroup>();
        cardLayout.padding = new RectOffset(24, 24, 24, 24);
        cardLayout.spacing = 18f;
        cardLayout.childAlignment = TextAnchor.MiddleCenter;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;

        confirmationText = CreateText(
            "ConfirmationText",
            card.transform,
            "Unlock Character?",
            22f,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
        confirmationText.textWrappingMode = TextWrappingModes.Normal;
        SetLayout(confirmationText.gameObject, 0f, 80f, 1f);

        GameObject actions = CreateUiObject("Actions", typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        actions.transform.SetParent(card.transform, false);
        SetLayout(actions, 0f, 48f, 1f);

        var actionsLayout = actions.GetComponent<HorizontalLayoutGroup>();
        actionsLayout.spacing = 12f;
        actionsLayout.childAlignment = TextAnchor.MiddleCenter;
        actionsLayout.childControlWidth = true;
        actionsLayout.childControlHeight = true;
        actionsLayout.childForceExpandWidth = true;
        actionsLayout.childForceExpandHeight = true;

        Button cancelButton = CreateButton("CancelButton", actions.transform, "CANCEL", 0f, 48f);
        Button confirmButton = CreateButton("ConfirmButton", actions.transform, "CONFIRM", 0f, 48f);
        cancelButton.onClick.AddListener(HideConfirmation);
        confirmButton.onClick.AddListener(ConfirmPurchase);

        confirmationRoot.SetActive(false);
    }

    void ApplySiblingOrder()
    {
        Transform tabs = transform.Find(TabsRootName);
        if (tabs == null)
            return;

        if (itemsPage != null)
        {
            int itemIndex = itemsPage.transform.GetSiblingIndex();
            tabs.SetSiblingIndex(itemIndex);
            itemsPage.transform.SetSiblingIndex(itemIndex + 1);
            characterPage.transform.SetSiblingIndex(itemIndex + 2);
        }

        confirmationRoot.transform.SetAsLastSibling();
    }

    void SetPage(bool showCharacters)
    {
        showingCharacters = showCharacters;
        HideConfirmation();

        if (itemsPage != null)
            itemsPage.SetActive(!showCharacters);

        if (characterPage != null)
            characterPage.SetActive(showCharacters);

        ApplyTabVisual(itemsTabButton, !showCharacters);
        ApplyTabVisual(charactersTabButton, showCharacters);

        shopPanel?.SetStatusMessage(string.Empty);
    }

    void EnsureRowCount(int targetCount)
    {
        while (rows.Count < targetCount)
            rows.Add(CreateRow(rows.Count));

        while (rows.Count > targetCount)
        {
            int lastIndex = rows.Count - 1;
            CharacterRow row = rows[lastIndex];
            rows.RemoveAt(lastIndex);

            if (row?.Root == null)
                continue;

            if (Application.isPlaying)
                Destroy(row.Root);
            else
                DestroyImmediate(row.Root);
        }
    }

    CharacterRow CreateRow(int index)
    {
        GameObject root = CreateUiObject(
            $"CharacterShopRow_{index:00}",
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        root.transform.SetParent(rowContainer, false);
        root.GetComponent<Image>().color = new Color32(31, 36, 38, 245);
        SetLayout(root, 0f, 88f, 1f);

        var layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        Image icon = CreateImage("Icon", root.transform, new Color32(52, 60, 64, 255));
        icon.preserveAspect = true;
        SetLayout(icon.gameObject, 64f, 64f);

        TMP_Text name = CreateText(
            "NameText",
            root.transform,
            "Character",
            20f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        SetLayout(name.gameObject, 190f, 0f, 1f);

        TMP_Text price = CreateText(
            "PriceText",
            root.transform,
            string.Empty,
            17f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineRight);
        price.color = new Color32(255, 214, 102, 255);
        SetLayout(price.gameObject, 130f);

        TMP_Text status = CreateText(
            "StatusText",
            root.transform,
            string.Empty,
            14f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft);
        status.color = new Color32(210, 220, 220, 255);
        status.textWrappingMode = TextWrappingModes.Normal;
        SetLayout(status.gameObject, 190f);

        Button buyButton = CreateButton("BuyButton", root.transform, "BUY", 110f, 48f);
        TMP_Text buttonLabel = buyButton.GetComponentInChildren<TMP_Text>(true);

        var row = new CharacterRow(root, icon, name, price, status, buyButton, buttonLabel);
        buyButton.onClick.AddListener(() => HandleBuyClicked(row));
        return row;
    }

    void BindRow(CharacterRow row, CharacterStats character)
    {
        if (row == null || row.Root == null)
            return;

        row.CharacterId = character != null && !string.IsNullOrWhiteSpace(character.characterId)
            ? character.characterId.Trim()
            : string.Empty;

        row.Icon.sprite = character != null ? character.icon : null;
        row.Icon.enabled = row.Icon.sprite != null;
        row.Name.text = ResolveDisplayName(character);

        CharacterUnlockEntry entry = null;
        bool configured = characterDatabase != null &&
                          !string.IsNullOrWhiteSpace(row.CharacterId) &&
                          characterDatabase.TryGetUnlockEntry(row.CharacterId, out entry);

        if (!configured)
        {
            row.Price.text = "NOT FOR SALE";
            row.Status.text = "Missing unlock configuration";
            SetButtonState(row, "LOCKED", false);
            return;
        }

        if (CharacterUnlockService.IsUnlockedForSelection(row.CharacterId))
        {
            row.Price.text = string.Empty;
            row.Status.text = "OWNED";
            SetButtonState(row, "OWNED", false);
            return;
        }

        int cost = entry.GoldCost;
        row.Price.text = $"{cost:N0} GOLD";

        bool canBuy = CharacterUnlockService.CanUnlock(row.CharacterId, inventorySource, out string reason);
        row.Status.text = canBuy ? string.Empty : reason;
        SetButtonState(row, "BUY", canBuy);
    }

    void HandleBuyClicked(CharacterRow row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.CharacterId))
            return;

        if (characterDatabase == null ||
            !characterDatabase.TryGetUnlockEntry(row.CharacterId, out CharacterUnlockEntry entry))
        {
            shopPanel?.SetStatusMessage("Character is not configured for sale.");
            return;
        }

        if (!CharacterUnlockService.CanUnlock(row.CharacterId, inventorySource, out string reason))
        {
            shopPanel?.SetStatusMessage(reason);
            Refresh();
            return;
        }

        pendingCharacterId = row.CharacterId;
        confirmationText.text = $"Unlock {entry.DisplayName} for {entry.GoldCost:N0} Gold?";
        confirmationRoot.SetActive(true);
        confirmationRoot.transform.SetAsLastSibling();
    }

    void ConfirmPurchase()
    {
        string characterId = pendingCharacterId;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            HideConfirmation();
            return;
        }

        string displayName = CharacterUnlockService.GetDisplayName(characterId);
        if (!CharacterUnlockService.TryUnlockForSelection(characterId, inventorySource, out string reason))
        {
            HideConfirmation();
            shopPanel?.SetStatusMessage(reason);
            Refresh();
            return;
        }

        HideConfirmation();
        shopPanel?.SetStatusMessage($"{displayName} unlocked.");
        shopPanel?.Refresh();
    }

    void HideConfirmation()
    {
        pendingCharacterId = string.Empty;
        if (confirmationRoot != null)
            confirmationRoot.SetActive(false);
    }

    void HandleCharacterUnlocked(string characterId)
    {
        if (showingCharacters)
            Refresh();
    }

    static string ResolveDisplayName(CharacterStats character)
    {
        if (character == null)
            return "Missing Character";

        if (!string.IsNullOrWhiteSpace(character.characterName))
            return character.characterName.Trim();

        if (!string.IsNullOrWhiteSpace(character.characterId))
            return character.characterId.Trim();

        return character.name;
    }

    static void SetButtonState(CharacterRow row, string label, bool interactable)
    {
        row.Button.interactable = interactable;
        if (row.ButtonLabel != null)
            row.ButtonLabel.text = label;
    }

    static void ApplyTabVisual(Button button, bool active)
    {
        if (button == null)
            return;

        Color32 baseColor = active ? ActiveTabColor : InactiveTabColor;
        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.selectedColor = baseColor;
        button.colors = colors;

        if (button.targetGraphic != null)
            button.targetGraphic.color = baseColor;
    }

    GameObject CreateUiObject(string name, params System.Type[] components)
    {
        var objectComponents = new System.Type[components.Length + 1];
        objectComponents[0] = typeof(RectTransform);
        for (int i = 0; i < components.Length; i++)
            objectComponents[i + 1] = components[i];

        var go = new GameObject(name, objectComponents);
        go.layer = gameObject.layer;
        return go;
    }

    Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = CreateUiObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    TMP_Text CreateText(
        string name,
        Transform parent,
        string text,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment)
    {
        GameObject go = CreateUiObject(name, typeof(TextMeshProUGUI));
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

    Button CreateButton(string name, Transform parent, string label, float width, float height)
    {
        GameObject go = CreateUiObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        SetLayout(go, width, height, width <= 0f ? 1f : 0f);

        Image image = go.GetComponent<Image>();
        image.color = new Color32(68, 105, 92, 255);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color32(68, 105, 92, 255);
        colors.highlightedColor = new Color32(84, 132, 115, 255);
        colors.pressedColor = new Color32(45, 78, 67, 255);
        colors.disabledColor = new Color32(43, 48, 49, 160);
        button.colors = colors;

        TMP_Text labelText = CreateText(
            "Label",
            go.transform,
            label,
            18f,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
        Stretch(labelText.rectTransform);
        return button;
    }

    static void SetLayout(
        GameObject go,
        float preferredWidth,
        float preferredHeight = 0f,
        float flexibleWidth = 0f,
        float flexibleHeight = 0f)
    {
        LayoutElement layout = go.GetComponent<LayoutElement>();
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

    sealed class CharacterRow
    {
        public CharacterRow(
            GameObject root,
            Image icon,
            TMP_Text name,
            TMP_Text price,
            TMP_Text status,
            Button button,
            TMP_Text buttonLabel)
        {
            Root = root;
            Icon = icon;
            Name = name;
            Price = price;
            Status = status;
            Button = button;
            ButtonLabel = buttonLabel;
        }

        public GameObject Root { get; }
        public Image Icon { get; }
        public TMP_Text Name { get; }
        public TMP_Text Price { get; }
        public TMP_Text Status { get; }
        public Button Button { get; }
        public TMP_Text ButtonLabel { get; }
        public string CharacterId { get; set; }
    }
}
