using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIWeaponEquipment : InventorySlotOwnerBase
{
    const int EquippedSlotVirtualIndex = -1;

    [Header("Binding")]
    [SerializeField] private PlayerInventory inventorySource;

    [Header("UI References")]
    [SerializeField] private RectTransform slotContainer;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private InventorySlotUI equippedSlotUI;
    [SerializeField] private DragItemUI dragItemUI;
    [SerializeField] private Canvas rootCanvas;

    readonly List<InventorySlotUI> slotUIs = new();
    readonly List<int> weaponSlotIndices = new();

    InventorySystem inventorySystem;

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

        base.Awake();
    }

    void Start()
    {
        if (inventorySource == null)
            inventorySource = ResolveInventorySource();

        BindSource(inventorySource);
    }

    void OnDestroy()
    {
        UnbindCurrentSource();
    }

    public void BindSource(PlayerInventory inventory)
    {
        UnbindCurrentSource();

        inventorySource = inventory;
        inventorySystem = inventorySource != null ? inventorySource.InventorySystem : null;

        if (inventorySystem != null)
        {
            inventorySystem.OnSlotChanged += HandleInventoryChanged;
            inventorySystem.OnInventoryReset += HandleInventoryReset;
        }

        if (inventorySource != null)
            inventorySource.OnEquippedWeaponChanged += HandleEquippedWeaponChanged;

        RefreshAll();
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

    public void RefreshAll()
    {
        RebuildWeaponSlotIndexCache();
        RebuildSlots();

        for (int i = 0; i < weaponSlotIndices.Count; i++)
            RefreshSlot(i);

        RefreshEquippedSlot();
    }

    public void RefreshSlot(int slotIndex)
    {
        if (!TryGetInventorySlotIndex(slotIndex, out var inventorySlotIndex))
            return;

        slotUIs[slotIndex].Bind(this, slotIndex, inventorySystem.GetSlot(inventorySlotIndex));
    }

    public void RefreshEquippedSlot()
    {
        if (equippedSlotUI == null)
            return;

        equippedSlotUI.Bind(this, EquippedSlotVirtualIndex, GetEquippedSlotData());
        equippedSlotUI.SetDraggingVisual(false);
    }

    public override void HandleDrop(int targetIndex)
    {
        if (!IsDragging || inventorySystem == null || inventorySource == null)
            return;

        if (targetIndex == EquippedSlotVirtualIndex)
        {
            HandleEquipDrop();
            return;
        }

        if (!TryGetInventorySlotIndex(DraggingSourceIndex, out var sourceIndex))
            return;

        if (!TryGetInventorySlotIndex(targetIndex, out var targetIndexInInventory))
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

    void HandleInventoryChanged(int slotIndex)
    {
        RefreshAll();
    }

    void HandleInventoryReset()
    {
        RefreshAll();
    }

    void HandleEquippedWeaponChanged(string equippedInstanceId)
    {
        RefreshEquippedSlot();
    }

    void HandleEquipDrop()
    {
        if (!TryGetInventorySlotIndex(DraggingSourceIndex, out var inventorySlotIndex))
            return;

        var slotData = inventorySystem.GetSlot(inventorySlotIndex);
        if (slotData == null || !slotData.HasWeaponInstance || slotData.weaponInstance == null)
            return;

        if (inventorySource.EquipWeaponInstance(slotData.weaponInstance.instanceId))
            RefreshEquippedSlot();
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

    void UnbindCurrentSource()
    {
        if (inventorySource != null)
            inventorySource.OnEquippedWeaponChanged -= HandleEquippedWeaponChanged;

        if (inventorySystem == null)
            return;

        inventorySystem.OnSlotChanged -= HandleInventoryChanged;
        inventorySystem.OnInventoryReset -= HandleInventoryReset;
        inventorySystem = null;
    }

    bool TryGetInventorySlotIndex(int displaySlotIndex, out int inventorySlotIndex)
    {
        inventorySlotIndex = -1;

        if (inventorySystem == null || displaySlotIndex < 0 || displaySlotIndex >= weaponSlotIndices.Count || displaySlotIndex >= slotUIs.Count)
            return false;

        inventorySlotIndex = weaponSlotIndices[displaySlotIndex];
        return inventorySlotIndex >= 0 && inventorySlotIndex < inventorySystem.Slots.Count;
    }

    bool TryGetBoundSlotData(int slotIndex, out InventorySlotData slotData)
    {
        slotData = null;

        if (slotIndex == EquippedSlotVirtualIndex)
        {
            slotData = GetEquippedSlotData();
            return slotData != null && !slotData.IsEmpty;
        }

        if (!TryGetInventorySlotIndex(slotIndex, out var inventorySlotIndex))
            return false;

        slotData = inventorySystem.GetSlot(inventorySlotIndex);
        return slotData != null && !slotData.IsEmpty;
    }

    InventorySlotData GetEquippedSlotData()
    {
        if (inventorySystem == null || inventorySource == null || string.IsNullOrWhiteSpace(inventorySource.EquippedWeaponInstanceId))
            return new InventorySlotData();

        for (int i = 0; i < inventorySystem.Slots.Count; i++)
        {
            var slot = inventorySystem.GetSlot(i);
            if (slot == null || !slot.HasWeaponInstance || slot.weaponInstance == null)
                continue;

            if (string.Equals(slot.weaponInstance.instanceId, inventorySource.EquippedWeaponInstanceId, StringComparison.Ordinal))
                return slot;
        }

        return new InventorySlotData();
    }

    static bool IsWeaponSlot(InventorySlotData slotData)
    {
        return slotData != null &&
               !slotData.IsEmpty &&
               (slotData.HasWeaponInstance || (slotData.item != null && slotData.item.itemType == ItemType.Weapon));
    }

    protected override bool TryGetSlotDataForDrag(int slotIndex, out InventorySlotData slotData)
    {
        slotData = null;

        if (slotIndex == EquippedSlotVirtualIndex)
            return false;

        return TryGetBoundSlotData(slotIndex, out slotData);
    }

    protected override void SetDraggingVisual(int slotIndex, bool isDragging)
    {
        if (!TryGetInventorySlotIndex(slotIndex, out _))
            return;

        slotUIs[slotIndex].SetDraggingVisual(isDragging);
    }

    protected override void RefreshSlotVisual(int slotIndex)
    {
        RefreshSlot(slotIndex);
    }
}
