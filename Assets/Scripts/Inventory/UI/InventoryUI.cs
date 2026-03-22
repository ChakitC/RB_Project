using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public interface IInventorySlotUIOwner
{
    bool BeginDrag(int slotIndex, PointerEventData eventData);
    void UpdateDrag(PointerEventData eventData);
    void EndDrag(PointerEventData eventData);
    void HandleDrop(int targetIndex);
    void HandleSlotClick(int slotIndex, PointerEventData eventData);
    void HandlePointerEnter(int slotIndex);
    void HandlePointerExit(int slotIndex);
}

public abstract class InventorySlotOwnerBase : MonoBehaviour, IInventorySlotUIOwner
{
    protected const int NoSlotIndex = int.MinValue;

    int draggingSourceIndex = NoSlotIndex;

    protected abstract DragItemUI DragVisual { get; }
    protected abstract Canvas RootCanvas { get; }

    protected virtual void Awake()
    {
        RefreshDragCanvasBinding();
        DragVisual?.Hide();
    }

    protected bool IsDragging => draggingSourceIndex != NoSlotIndex;
    protected int DraggingSourceIndex => draggingSourceIndex;

    public virtual bool BeginDrag(int slotIndex, PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return false;

        if (!TryGetSlotDataForDrag(slotIndex, out var slotData) || slotData == null || slotData.IsEmpty)
            return false;

        draggingSourceIndex = slotIndex;

        var dragVisual = DragVisual;
        if (dragVisual != null)
            dragVisual.Show(slotData, eventData != null ? eventData.position : Vector2.zero, ResolveEventCamera());

        SetDraggingVisual(slotIndex, true);
        return true;
    }

    public virtual void UpdateDrag(PointerEventData eventData)
    {
        var dragVisual = DragVisual;
        if (!IsDragging || dragVisual == null || eventData == null)
            return;

        dragVisual.Move(eventData.position, ResolveEventCamera());
    }

    public virtual void EndDrag(PointerEventData eventData)
    {
        if (!IsDragging)
            return;

        int sourceIndex = draggingSourceIndex;
        draggingSourceIndex = NoSlotIndex;

        DragVisual?.Hide();
        SetDraggingVisual(sourceIndex, false);
        RefreshSlotVisual(sourceIndex);
    }

    public abstract void HandleDrop(int targetIndex);

    public virtual void HandleSlotClick(int slotIndex, PointerEventData eventData) { }
    public virtual void HandlePointerEnter(int slotIndex) { }
    public virtual void HandlePointerExit(int slotIndex) { }

    protected void RefreshDragCanvasBinding()
    {
        var dragVisual = DragVisual;
        if (dragVisual == null)
            return;

        dragVisual.SetCanvas(ResolveCanvas());
    }

    protected virtual void SetDraggingVisual(int slotIndex, bool isDragging) { }
    protected virtual void RefreshSlotVisual(int slotIndex) { }
    protected abstract bool TryGetSlotDataForDrag(int slotIndex, out InventorySlotData slotData);

    protected Camera ResolveEventCamera()
    {
        var canvas = ResolveCanvas();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }

    Canvas ResolveCanvas()
    {
        return RootCanvas != null ? RootCanvas : GetComponentInParent<Canvas>();
    }
}

public class InventoryUI : InventorySlotOwnerBase
{
    [Header("Binding")]
    [SerializeField] private PlayerInventory inventorySource;

    [Header("UI References")]
    [SerializeField] private RectTransform slotContainer;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private DragItemUI dragItemUI;
    [SerializeField] private Canvas rootCanvas;

    readonly List<InventorySlotUI> slotUIs = new();

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
        if (inventorySource != null)
            BindSource(inventorySource);
    }

    void OnDestroy()
    {
        UnbindCurrentSystem();
    }

    public void BindSource(PlayerInventory inventory)
    {
        inventorySource = inventory;
        Bind(inventory != null ? inventory.InventorySystem : null);
    }

    public void Bind(InventorySystem system)
    {
        if (ReferenceEquals(inventorySystem, system))
        {
            RefreshAll();
            return;
        }

        UnbindCurrentSystem();
        inventorySystem = system;

        if (inventorySystem != null)
        {
            inventorySystem.OnSlotChanged += RefreshSlot;
            inventorySystem.OnInventoryReset += HandleInventoryReset;
        }

        RebuildSlots();
        RefreshAll();
    }

    public void RefreshAll()
    {
        RebuildSlots();

        if (inventorySystem == null)
            return;

        for (int i = 0; i < inventorySystem.Slots.Count; i++)
            RefreshSlot(i);
    }

    public void RefreshSlot(int slotIndex)
    {
        if (inventorySystem == null || !IsValidSlotIndex(slotIndex))
            return;

        slotUIs[slotIndex].Bind(this, slotIndex, inventorySystem.GetSlot(slotIndex));
    }

    public override void HandleDrop(int targetIndex)
    {
        if (!IsDragging || inventorySystem == null || !IsValidSlotIndex(targetIndex))
            return;

        if (DraggingSourceIndex == targetIndex)
            return;

        inventorySystem.MoveOrSwap(DraggingSourceIndex, targetIndex);
    }

    public override void HandleSlotClick(int slotIndex, PointerEventData eventData)
    {
        if (inventorySystem == null || !IsValidSlotIndex(slotIndex))
            return;

        var slotData = inventorySystem.GetSlot(slotIndex);
        if (slotData == null || slotData.IsEmpty)
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
        if (inventorySystem == null || !IsValidSlotIndex(slotIndex))
            return;

        var slotData = inventorySystem.GetSlot(slotIndex);
        if (slotData == null || slotData.IsEmpty)
            return;

        OnPointerEnterSlot?.Invoke(slotIndex, slotData);
    }

    public override void HandlePointerExit(int slotIndex)
    {
        if (inventorySystem == null || !IsValidSlotIndex(slotIndex))
            return;

        var slotData = inventorySystem.GetSlot(slotIndex);
        if (slotData == null || slotData.IsEmpty)
            return;

        OnPointerExitSlot?.Invoke(slotIndex, slotData);
    }

    void HandleInventoryReset()
    {
        RebuildSlots();
        RefreshAll();
    }

    void RebuildSlots()
    {
        int targetCount = inventorySystem != null ? inventorySystem.Slots.Count : 0;

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

    void UnbindCurrentSystem()
    {
        if (inventorySystem == null)
            return;

        inventorySystem.OnSlotChanged -= RefreshSlot;
        inventorySystem.OnInventoryReset -= HandleInventoryReset;
        inventorySystem = null;
    }

    bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < slotUIs.Count && inventorySystem != null && slotIndex < inventorySystem.Slots.Count;
    }

    protected override bool TryGetSlotDataForDrag(int slotIndex, out InventorySlotData slotData)
    {
        slotData = null;

        if (inventorySystem == null || !IsValidSlotIndex(slotIndex))
            return false;

        slotData = inventorySystem.GetSlot(slotIndex);
        return slotData != null && !slotData.IsEmpty;
    }

    protected override void SetDraggingVisual(int slotIndex, bool isDragging)
    {
        if (!IsValidSlotIndex(slotIndex))
            return;

        slotUIs[slotIndex].SetDraggingVisual(isDragging);
    }

    protected override void RefreshSlotVisual(int slotIndex)
    {
        RefreshSlot(slotIndex);
    }
}
