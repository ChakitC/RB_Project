using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ShopPanelUI : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private ShopCatalogBase catalog;
    [SerializeField] private PlayerInventory inventorySource;
    [SerializeField] private ShopService shopService;

    [Header("UI")]
    [SerializeField] private RectTransform rowContainer;
    [SerializeField] private ShopItemRowUI rowPrefab;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private GameObject emptyState;
    [SerializeField] private Button closeButton;

    readonly List<ShopItemRowUI> rows = new();

    ShopService boundService;
    PlayerInventory boundInventorySource;

    public bool IsOpen => gameObject.activeSelf;
    public ShopCatalogBase CurrentCatalog => catalog;

    public void ConfigureReferences(
        ShopCatalogBase newCatalog,
        PlayerInventory inventory,
        ShopService service,
        RectTransform rows,
        ShopItemRowUI row,
        TMP_Text gold,
        TMP_Text message,
        GameObject empty,
        Button close)
    {
        if (newCatalog != null)
            catalog = newCatalog;

        rowContainer = rows;
        rowPrefab = row;
        goldText = gold;
        messageText = message;
        emptyState = empty;

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        closeButton = close;

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        shopService = service;
        BindService(shopService);
        BindSource(inventory);
        PrepareCatalogForOpen();
        Refresh();
    }

    void Awake()
    {
        ResolveReferences();
        BindService(shopService);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        RebuildRows();
    }

    void OnEnable()
    {
        ResolveReferences();
        BindService(shopService);
        BindSource(inventorySource);
        PrepareCatalogForOpen();
        Refresh();
    }

    void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        BindService(null);
        BindSource(null);
    }

    public void Open()
    {
        Open(null, null);
    }

    public void Open(ShopCatalogBase catalogOverride, PlayerInventory inventoryOverride)
    {
        if (catalogOverride != null)
            catalog = catalogOverride;

        ResolveReferences();

        if (inventoryOverride != null)
            BindSource(inventoryOverride);

        PrepareCatalogForOpen();
        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void SetCatalog(ShopCatalogBase newCatalog)
    {
        catalog = newCatalog;
        PrepareCatalogForOpen();
        Refresh();
    }

    public void BindSource(PlayerInventory inventory)
    {
        if (boundInventorySource == inventory)
        {
            inventorySource = inventory;
            RefreshGold();
            return;
        }

        if (boundInventorySource != null)
            boundInventorySource.OnGoldChanged -= HandleGoldChanged;

        inventorySource = inventory;
        boundInventorySource = inventory;

        if (boundInventorySource != null)
            boundInventorySource.OnGoldChanged += HandleGoldChanged;

        RefreshGold();
    }

    public void HandleBuyClicked(int entryIndex)
    {
        ResolveReferences();
        BindService(shopService);

        if (boundService == null)
        {
            SetMessage("Missing shop service.");
            return;
        }

        if (!boundService.TryBuy(inventorySource, catalog, entryIndex, out string reason))
        {
            SetMessage(reason);
            Refresh();
            return;
        }

        SetMessage(string.Empty);
        Refresh();
    }

    public void Refresh()
    {
        ResolveReferences();
        BindService(shopService);
        RefreshGold();
        RebuildRows();

        int entryCount = catalog != null ? catalog.EntryCount : 0;
        if (emptyState != null)
            emptyState.SetActive(entryCount == 0);

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null)
                continue;

            var entry = catalog != null ? catalog.GetEntry(i) : null;
            int stock = boundService != null ? boundService.GetRemainingStock(catalog, i) : -1;
            string reason = string.Empty;
            bool canBuy = boundService != null && boundService.CanBuy(inventorySource, catalog, i, out reason);
            row.Bind(this, i, entry, stock, canBuy, reason);
        }
    }

    void ResolveReferences()
    {
        if (rowContainer == null)
            rowContainer = transform as RectTransform;

        if (shopService == null)
            shopService = GetComponent<ShopService>();

        if (shopService == null)
            shopService = GetComponentInParent<ShopService>(true);

        if (shopService == null)
            shopService = FindFirstObjectByType<ShopService>(FindObjectsInactive.Include);

        if (shopService == null)
            shopService = gameObject.AddComponent<ShopService>();

        if (catalog == null && shopService.DefaultCatalog != null)
            catalog = shopService.DefaultCatalog;

        if (inventorySource == null)
            inventorySource = ResolveInventorySource();
    }

    void BindService(ShopService service)
    {
        if (boundService == service)
            return;

        if (boundService != null)
            boundService.OnStockChanged -= HandleStockChanged;

        boundService = service;

        if (boundService != null)
            boundService.OnStockChanged += HandleStockChanged;
    }

    void RebuildRows()
    {
        int targetCount = catalog != null ? catalog.EntryCount : 0;

        if (rowPrefab == null || rowContainer == null)
            return;

        while (rows.Count < targetCount)
        {
            var row = Instantiate(rowPrefab, rowContainer);
            row.name = $"{rowPrefab.name}_{rows.Count:00}";
            rows.Add(row);
        }

        while (rows.Count > targetCount)
        {
            int lastIndex = rows.Count - 1;
            var row = rows[lastIndex];
            rows.RemoveAt(lastIndex);

            if (Application.isPlaying)
                Destroy(row.gameObject);
            else
                DestroyImmediate(row.gameObject);
        }
    }

    void RefreshGold()
    {
        if (goldText != null)
            goldText.text = inventorySource != null ? inventorySource.Gold.ToString("N0") : "0";
    }

    void SetMessage(string message)
    {
        if (messageText != null)
            messageText.text = message ?? string.Empty;
    }

    void PrepareCatalogForOpen()
    {
        if (catalog != null)
            catalog.PrepareForOpen();
    }

    void HandleGoldChanged(int gold)
    {
        Refresh();
    }

    void HandleStockChanged(ShopCatalogBase changedCatalog, int entryIndex)
    {
        if (changedCatalog == null || changedCatalog == catalog)
            Refresh();
    }

    PlayerInventory ResolveInventorySource()
    {
        var fromParent = GetComponentInParent<PlayerInventory>(true);
        if (fromParent != null)
            return fromParent;

        if (transform.root != null)
        {
            var fromRoot = transform.root.GetComponentInChildren<PlayerInventory>(true);
            if (fromRoot != null)
                return fromRoot;
        }

        return FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
    }
}
