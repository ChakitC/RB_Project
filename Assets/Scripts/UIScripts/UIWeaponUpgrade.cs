using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIWeaponUpgrade : InventorySlotOwnerBase
{
    const int UpgradeSlotVirtualIndex = -1;

    [Header("Binding")]
    [SerializeField] private PlayerInventory inventorySource;
    [SerializeField] private WeaponUpgradeService upgradeService;
    [SerializeField] private WeaponUpgradeCurve fallbackUpgradeCurve;

    [Header("Inventory List")]
    [SerializeField] private RectTransform slotContainer;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private InventorySlotUI upgradeSlotUI;
    [SerializeField] private DragItemUI dragItemUI;
    [SerializeField] private Canvas rootCanvas;

    [Header("Details")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private TMP_Text scrapText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text previewText;
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button dismantleButton;

    readonly List<InventorySlotUI> slotUIs = new();
    readonly List<int> weaponSlotIndices = new();
    readonly List<WeaponUpgradeMilestone> _milestoneBuffer = new();

    InventorySystem inventorySystem;
    string selectedInstanceId;

    protected override DragItemUI DragVisual => dragItemUI;
    protected override Canvas RootCanvas => rootCanvas;

    public event Action<int, InventorySlotData> OnLeftClick;
    public event Action<int, InventorySlotData> OnRightClick;
    public event Action<int, InventorySlotData> OnPointerEnterSlot;
    public event Action<int, InventorySlotData> OnPointerExitSlot;

    protected override void Awake()
    {
        if (slotContainer == null)
            slotContainer = transform as RectTransform;

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        if (!inventorySource)
            inventorySource = ResolveInventorySource();

        if (!upgradeService)
            upgradeService = GetComponent<WeaponUpgradeService>();

        if (!upgradeService)
            upgradeService = GetComponentInParent<WeaponUpgradeService>(true);

        base.Awake();

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(HandleUpgradeClicked);

        if (dismantleButton != null)
            dismantleButton.onClick.AddListener(HandleDismantleClicked);

        BindSource(inventorySource);
    }

    void OnDestroy()
    {
        if (upgradeButton != null)
            upgradeButton.onClick.RemoveListener(HandleUpgradeClicked);

        if (dismantleButton != null)
            dismantleButton.onClick.RemoveListener(HandleDismantleClicked);

        UnbindInventoryEvents();
    }

    public void BindSource(PlayerInventory inventory)
    {
        UnbindInventoryEvents();

        inventorySource = inventory;
        inventorySystem = inventorySource != null ? inventorySource.InventorySystem : null;

        if (inventorySystem != null)
        {
            inventorySystem.OnSlotChanged += HandleInventorySlotChanged;
            inventorySystem.OnInventoryReset += HandleInventoryReset;
        }

        if (inventorySource != null)
        {
            inventorySource.OnGoldChanged += HandleGoldChanged;
            inventorySource.OnScrapChanged += HandleScrapChanged;
        }

        RefreshAll();
    }

    public void SelectWeaponInstance(string instanceId)
    {
        selectedInstanceId = instanceId;
        RefreshUpgradeSlot();
        RefreshDetails();
    }

    public void SelectSlotData(InventorySlotData slotData)
    {
        if (slotData == null || !slotData.HasWeaponInstance || slotData.weaponInstance == null)
        {
            SelectWeaponInstance(null);
            return;
        }

        SelectWeaponInstance(slotData.weaponInstance.instanceId);
    }

    public void ClearSelection()
    {
        SelectWeaponInstance(null);
    }

    public void RefreshAll()
    {
        RebuildWeaponSlotIndexCache();
        RebuildSlots();

        for (int i = 0; i < weaponSlotIndices.Count; i++)
            RefreshSlot(i);

        RefreshUpgradeSlot();
        RefreshDetails();
        RefreshScrapText();
    }

    public void RefreshSlot(int slotIndex)
    {
        if (!TryGetInventorySlotIndex(slotIndex, out int inventorySlotIndex))
            return;

        slotUIs[slotIndex].Bind(this, slotIndex, inventorySystem.GetSlot(inventorySlotIndex));
    }

    public void RefreshUpgradeSlot()
    {
        if (upgradeSlotUI == null)
            return;

        upgradeSlotUI.Bind(this, UpgradeSlotVirtualIndex, GetSelectedSlotData());
        upgradeSlotUI.SetDraggingVisual(false);
    }

    public override void HandleDrop(int targetIndex)
    {
        if (!IsDragging || inventorySystem == null || inventorySource == null)
            return;

        if (targetIndex == UpgradeSlotVirtualIndex)
        {
            HandleUpgradeSlotDrop();
            return;
        }

        if (!TryGetInventorySlotIndex(DraggingSourceIndex, out int sourceIndex))
            return;

        if (!TryGetInventorySlotIndex(targetIndex, out int targetIndexInInventory))
            return;

        if (sourceIndex == targetIndexInInventory)
            return;

        inventorySystem.MoveOrSwap(sourceIndex, targetIndexInInventory);
    }

    public override void HandleSlotClick(int slotIndex, PointerEventData eventData)
    {
        if (!TryGetBoundSlotData(slotIndex, out var slotData))
            return;

        switch (eventData.button)
        {
            case PointerEventData.InputButton.Left:
                if (slotIndex != UpgradeSlotVirtualIndex)
                    SelectSlotData(slotData);

                OnLeftClick?.Invoke(slotIndex, slotData);
                break;

            case PointerEventData.InputButton.Right:
                OnRightClick?.Invoke(slotIndex, slotData);
                break;
        }
    }

    public override void HandlePointerEnter(int slotIndex)
    {
        if (!TryGetBoundSlotData(slotIndex, out var slotData))
            return;

        OnPointerEnterSlot?.Invoke(slotIndex, slotData);
    }

    public override void HandlePointerExit(int slotIndex)
    {
        if (!TryGetBoundSlotData(slotIndex, out var slotData))
            return;

        OnPointerExitSlot?.Invoke(slotIndex, slotData);
    }

    void HandleUpgradeSlotDrop()
    {
        if (!TryGetInventorySlotIndex(DraggingSourceIndex, out int inventorySlotIndex))
            return;

        var slotData = inventorySystem.GetSlot(inventorySlotIndex);
        if (slotData == null || !slotData.HasWeaponInstance || slotData.weaponInstance == null)
            return;

        SelectWeaponInstance(slotData.weaponInstance.instanceId);
    }

    void HandleUpgradeClicked()
    {
        if (inventorySource == null || string.IsNullOrWhiteSpace(selectedInstanceId))
            return;

        string reason;
        bool upgraded = upgradeService != null
            ? upgradeService.TryUpgrade(inventorySource, selectedInstanceId, out reason)
            : WeaponUpgradeService.TryUpgrade(inventorySource, selectedInstanceId, ResolveFallbackCurve(), out reason);

        if (!upgraded && reasonText != null)
            reasonText.text = reason ?? string.Empty;

        RefreshAll();
    }

    void HandleDismantleClicked()
    {
        if (inventorySource == null || string.IsNullOrWhiteSpace(selectedInstanceId))
            return;

        int scrapGranted;
        string dismantleReason;
        bool dismantled = upgradeService != null
            ? upgradeService.TryDismantle(inventorySource, selectedInstanceId, out scrapGranted, out dismantleReason)
            : WeaponUpgradeService.TryDismantleWithDefaultReward(inventorySource, selectedInstanceId, out scrapGranted, out dismantleReason);

        if (dismantled)
            selectedInstanceId = null;

        RefreshAll();

        if (reasonText != null)
            reasonText.text = dismantled
                ? $"Gained {scrapGranted} Scrap."
                : dismantleReason ?? string.Empty;
    }

    void RefreshDetails()
    {
        if (!TryResolveSelected(out var weapon, out var instance))
        {
            ApplyEmptyState();
            return;
        }

        var curve = WeaponUpgradeService.ResolveUpgradeCurve(weapon, ResolveFallbackCurve());
        bool canUpgrade = WeaponUpgradeService.CanUpgrade(
            inventorySource,
            selectedInstanceId,
            ResolveFallbackCurve(),
            out var cost,
            out string reason);

        int dismantleReward = 0;
        bool canDismantle = upgradeService != null
            ? upgradeService.CanDismantle(inventorySource, selectedInstanceId, out dismantleReward, out _)
            : WeaponUpgradeService.CanDismantleWithDefaultReward(inventorySource, selectedInstanceId, out dismantleReward, out _);

        if (titleText != null)
            titleText.text = ResolveItemTitle(weapon);

        if (levelText != null)
            levelText.text = curve != null
                ? $"+{Mathf.Max(0, instance.upgradeLevel)} / +{curve.GetMaxLevel(instance.rarity)}"
                : $"+{Mathf.Max(0, instance.upgradeLevel)}";

        if (tierText != null)
            tierText.text = $"Tier {Mathf.Max(0, instance.upgradeTier)}";

        if (costText != null)
            costText.text = cost != null ? BuildCostText(cost) : string.Empty;

        if (previewText != null)
            previewText.text = BuildPreviewText(weapon, instance, curve, dismantleReward);

        if (reasonText != null)
            reasonText.text = canUpgrade ? string.Empty : reason ?? string.Empty;

        if (upgradeButton != null)
            upgradeButton.interactable = canUpgrade;

        if (dismantleButton != null)
            dismantleButton.interactable = canDismantle;
    }

    bool TryResolveSelected(out GunConfig weapon, out WeaponInstanceData instance)
    {
        weapon = null;
        instance = null;

        return inventorySource != null &&
               !string.IsNullOrWhiteSpace(selectedInstanceId) &&
               inventorySource.TryGetWeaponInstanceWithDefinition(selectedInstanceId, out weapon, out instance) &&
               weapon != null &&
               instance != null;
    }

    void ApplyEmptyState()
    {
        if (titleText != null)
            titleText.text = string.Empty;

        if (levelText != null)
            levelText.text = string.Empty;

        if (tierText != null)
            tierText.text = string.Empty;

        if (costText != null)
            costText.text = string.Empty;

        if (previewText != null)
            previewText.text = string.Empty;

        if (reasonText != null)
            reasonText.text = string.Empty;

        if (upgradeButton != null)
            upgradeButton.interactable = false;

        if (dismantleButton != null)
            dismantleButton.interactable = false;
    }

    string BuildCostText(WeaponUpgradeResolvedCost cost)
    {
        if (cost == null)
            return string.Empty;

        var builder = new StringBuilder();
        builder.Append("Cost:");

        bool hasCurrencyCost = false;
        if (cost.goldCost > 0)
        {
            builder.Append(" ").Append(cost.goldCost).Append(" Gold");
            hasCurrencyCost = true;
        }

        if (cost.scrapCost > 0)
        {
            if (hasCurrencyCost)
                builder.Append(",");

            builder.Append(" ").Append(cost.scrapCost).Append(" Scrap");
            hasCurrencyCost = true;
        }

        if (cost.materials != null)
        {
            for (int i = 0; i < cost.materials.Count; i++)
            {
                var material = cost.materials[i];
                if (material == null || !material.IsValid)
                    continue;

                builder.AppendLine();
                builder.Append("- ")
                    .Append(ResolveItemTitle(material.item))
                    .Append(" x")
                    .Append(material.amount);
            }
        }

        if (!hasCurrencyCost && !HasMaterialCost(cost.materials))
            builder.Append(" Free");

        return builder.ToString();
    }

    string BuildPreviewText(
        GunConfig weapon,
        WeaponInstanceData instance,
        WeaponUpgradeCurve curve,
        int dismantleReward)
    {
        if (!weapon || instance == null)
            return string.Empty;

        if (curve == null)
            return dismantleReward > 0 ? $"Dismantle: +{dismantleReward} Scrap" : string.Empty;

        int currentLevel = Mathf.Max(0, instance.upgradeLevel);
        int maxLevel = curve.GetMaxLevel(instance.rarity);
        int nextLevel = Mathf.Min(maxLevel, currentLevel + 1);

        var builder = new StringBuilder();
        builder.Append("+").Append(currentLevel).Append(" -> +").Append(nextLevel);

        curve.GetActiveMilestones(_milestoneBuffer, weapon.WeaponType, nextLevel);
        for (int i = 0; i < _milestoneBuffer.Count; i++)
        {
            var milestone = _milestoneBuffer[i];
            if (milestone == null || currentLevel >= milestone.requiredLevel)
                continue;

            builder.AppendLine();
            builder.Append("Unlock: ").Append(ResolveMilestoneTitle(milestone));
        }

        if (dismantleReward > 0)
        {
            builder.AppendLine();
            builder.Append("Dismantle: +").Append(dismantleReward).Append(" Scrap");
        }

        return builder.ToString();
    }

    static bool HasMaterialCost(List<WeaponUpgradeMaterialCost> materials)
    {
        if (materials == null)
            return false;

        for (int i = 0; i < materials.Count; i++)
        {
            var material = materials[i];
            if (material != null && material.IsValid)
                return true;
        }

        return false;
    }

    WeaponUpgradeCurve ResolveFallbackCurve()
    {
        if (upgradeService != null && upgradeService.DefaultUpgradeCurve != null)
            return upgradeService.DefaultUpgradeCurve;

        return fallbackUpgradeCurve;
    }

    void HandleInventorySlotChanged(int slotIndex)
    {
        if (inventorySource == null || inventorySource.FindWeaponInstanceSlotIndex(selectedInstanceId) < 0)
            selectedInstanceId = null;

        RefreshAll();
    }

    void HandleInventoryReset()
    {
        if (inventorySource == null || inventorySource.FindWeaponInstanceSlotIndex(selectedInstanceId) < 0)
            selectedInstanceId = null;

        RefreshAll();
    }

    void HandleGoldChanged(int gold)
    {
        RefreshDetails();
    }

    void HandleScrapChanged(int scrap)
    {
        RefreshDetails();
        RefreshScrapText();
    }

    void RefreshScrapText()
    {
        if (scrapText != null)
            scrapText.text = inventorySource != null ? inventorySource.Scrap.ToString("N0") : "0";
    }

    void RebuildWeaponSlotIndexCache()
    {
        weaponSlotIndices.Clear();

        if (inventorySystem == null)
            return;

        for (int i = 0; i < inventorySystem.Slots.Count; i++)
        {
            var slot = inventorySystem.GetSlot(i);
            if (IsWeaponSlot(slot))
                weaponSlotIndices.Add(i);
        }
    }

    void RebuildSlots()
    {
        int targetCount = weaponSlotIndices.Count;

        if (slotPrefab == null || slotContainer == null)
            return;

        while (slotUIs.Count < targetCount)
        {
            var slotUI = Instantiate(slotPrefab, slotContainer);
            slotUI.name = $"{slotPrefab.name}_{slotUIs.Count:00}";
            slotUIs.Add(slotUI);
        }

        while (slotUIs.Count > targetCount)
        {
            int lastIndex = slotUIs.Count - 1;
            var slotUI = slotUIs[lastIndex];
            slotUIs.RemoveAt(lastIndex);

            if (Application.isPlaying)
                Destroy(slotUI.gameObject);
            else
                DestroyImmediate(slotUI.gameObject);
        }
    }

    bool TryGetInventorySlotIndex(int displaySlotIndex, out int inventorySlotIndex)
    {
        inventorySlotIndex = -1;

        if (inventorySystem == null ||
            displaySlotIndex < 0 ||
            displaySlotIndex >= weaponSlotIndices.Count ||
            displaySlotIndex >= slotUIs.Count)
        {
            return false;
        }

        inventorySlotIndex = weaponSlotIndices[displaySlotIndex];
        return inventorySlotIndex >= 0 && inventorySlotIndex < inventorySystem.Slots.Count;
    }

    bool TryGetBoundSlotData(int slotIndex, out InventorySlotData slotData)
    {
        slotData = null;

        if (slotIndex == UpgradeSlotVirtualIndex)
        {
            slotData = GetSelectedSlotData();
            return slotData != null && !slotData.IsEmpty;
        }

        if (!TryGetInventorySlotIndex(slotIndex, out int inventorySlotIndex))
            return false;

        slotData = inventorySystem.GetSlot(inventorySlotIndex);
        return slotData != null && !slotData.IsEmpty;
    }

    InventorySlotData GetSelectedSlotData()
    {
        if (inventorySource == null || string.IsNullOrWhiteSpace(selectedInstanceId))
            return new InventorySlotData();

        int inventorySlotIndex = inventorySource.FindWeaponInstanceSlotIndex(selectedInstanceId);
        if (inventorySlotIndex < 0 || inventorySystem == null)
            return new InventorySlotData();

        var slot = inventorySystem.GetSlot(inventorySlotIndex);
        return slot != null ? slot : new InventorySlotData();
    }

    void UnbindInventoryEvents()
    {
        if (inventorySystem != null)
        {
            inventorySystem.OnSlotChanged -= HandleInventorySlotChanged;
            inventorySystem.OnInventoryReset -= HandleInventoryReset;
            inventorySystem = null;
        }

        if (inventorySource != null)
        {
            inventorySource.OnGoldChanged -= HandleGoldChanged;
            inventorySource.OnScrapChanged -= HandleScrapChanged;
        }
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

    static bool IsWeaponSlot(InventorySlotData slotData)
    {
        return slotData != null &&
               !slotData.IsEmpty &&
               slotData.HasWeaponInstance;
    }

    protected override bool TryGetSlotDataForDrag(int slotIndex, out InventorySlotData slotData)
    {
        slotData = null;

        if (slotIndex == UpgradeSlotVirtualIndex)
            return false;

        return TryGetBoundSlotData(slotIndex, out slotData);
    }

    protected override void SetDraggingVisual(int slotIndex, bool isDragging)
    {
        if (slotIndex == UpgradeSlotVirtualIndex)
        {
            upgradeSlotUI?.SetDraggingVisual(isDragging);
            return;
        }

        if (!TryGetInventorySlotIndex(slotIndex, out _))
            return;

        slotUIs[slotIndex].SetDraggingVisual(isDragging);
    }

    protected override void RefreshSlotVisual(int slotIndex)
    {
        if (slotIndex == UpgradeSlotVirtualIndex)
        {
            RefreshUpgradeSlot();
            return;
        }

        RefreshSlot(slotIndex);
    }

    static string ResolveItemTitle(ItemDefinition item)
    {
        if (item == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(item.displayName))
            return item.displayName.Trim();

        return item.name;
    }

    static string ResolveMilestoneTitle(WeaponUpgradeMilestone milestone)
    {
        if (milestone == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(milestone.displayName))
            return milestone.displayName.Trim();

        if (!string.IsNullOrWhiteSpace(milestone.milestoneId))
            return milestone.milestoneId.Trim();

        return $"+{Mathf.Max(1, milestone.requiredLevel)}";
    }
}
