using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    [Header("Scroll And Layout")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField, Min(1)] private int columnCount = 9;
    [SerializeField, Min(1)] private int visibleRowCount = 4;
    [SerializeField, Min(0f)] private float slotSpacing = 8f;
    [SerializeField, Min(0f)] private float autoScrollEdgeSize = 72f;
    [SerializeField, Min(0f)] private float autoScrollSpeed = 0.8f;

    readonly List<InventorySlotUI> slotUIs = new();

    InventorySystem inventorySystem;
    Vector2 lastViewportSize = new(float.NaN, float.NaN);
    Vector2 dragPointerScreenPosition;
    bool hasDragPointerPosition;
    bool layoutDirty = true;

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

        if (gridLayout == null && slotContainer != null)
            gridLayout = slotContainer.GetComponent<GridLayoutGroup>();

        EnsureScrollView();

        base.Awake();
    }

    void LateUpdate()
    {
        RefreshLayoutIfNeeded();
        ApplyDragAutoScroll();
    }

    void OnRectTransformDimensionsChange()
    {
        layoutDirty = true;
    }

    void Start()
    {
        if (inventorySource == null)
            inventorySource = ResolveInventorySource();

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

    public override bool BeginDrag(int slotIndex, PointerEventData eventData)
    {
        bool beganDrag = base.BeginDrag(slotIndex, eventData);
        if (!beganDrag)
            return false;

        CacheDragPointerPosition(eventData);
        return true;
    }

    public override void UpdateDrag(PointerEventData eventData)
    {
        base.UpdateDrag(eventData);
        CacheDragPointerPosition(eventData);
    }

    public override void EndDrag(PointerEventData eventData)
    {
        base.EndDrag(eventData);
        hasDragPointerPosition = false;
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

        layoutDirty = true;
    }

    void EnsureScrollView()
    {
        if (scrollRect != null && viewport != null)
            return;

        if (slotContainer == null || slotContainer.parent is not RectTransform window)
            return;

        var scrollObject = new GameObject(
            "InventoryScrollView",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(ScrollRect));
        scrollObject.layer = gameObject.layer;
        scrollObject.transform.SetParent(window, false);
        scrollObject.transform.SetSiblingIndex(transform.GetSiblingIndex());

        var scrollTransform = scrollObject.GetComponent<RectTransform>();
        scrollTransform.anchorMin = new Vector2(0f, 1f);
        scrollTransform.anchorMax = Vector2.one;
        scrollTransform.pivot = new Vector2(0.5f, 1f);
        scrollTransform.anchoredPosition = new Vector2(0f, -165f);
        scrollTransform.sizeDelta = new Vector2(-290f, 599f);

        var scrollBackground = scrollObject.GetComponent<Image>();
        scrollBackground.color = new Color(0.055f, 0.065f, 0.075f, 0.92f);

        viewport = CreateViewport(scrollTransform);
        Scrollbar scrollbar = CreateVerticalScrollbar(scrollTransform);

        slotContainer.SetParent(viewport, false);
        slotContainer.anchorMin = new Vector2(0f, 1f);
        slotContainer.anchorMax = new Vector2(1f, 1f);
        slotContainer.pivot = new Vector2(0.5f, 1f);
        slotContainer.anchoredPosition = Vector2.zero;

        scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.content = slotContainer;
        scrollRect.viewport = viewport;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 36f;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = 8f;

        layoutDirty = true;
    }

    RectTransform CreateViewport(RectTransform parent)
    {
        var viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(RectMask2D));
        viewportObject.layer = gameObject.layer;
        viewportObject.transform.SetParent(parent, false);

        var viewportTransform = viewportObject.GetComponent<RectTransform>();
        viewportTransform.anchorMin = Vector2.zero;
        viewportTransform.anchorMax = Vector2.one;
        viewportTransform.offsetMin = new Vector2(12f, 12f);
        viewportTransform.offsetMax = new Vector2(-12f, -12f);

        var viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = Color.clear;
        return viewportTransform;
    }

    Scrollbar CreateVerticalScrollbar(RectTransform parent)
    {
        var scrollbarObject = new GameObject(
            "Scrollbar Vertical",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Scrollbar));
        scrollbarObject.layer = gameObject.layer;
        scrollbarObject.transform.SetParent(parent, false);

        var scrollbarTransform = scrollbarObject.GetComponent<RectTransform>();
        scrollbarTransform.anchorMin = new Vector2(1f, 0f);
        scrollbarTransform.anchorMax = Vector2.one;
        scrollbarTransform.pivot = new Vector2(1f, 1f);
        scrollbarTransform.offsetMin = new Vector2(-16f, 4f);
        scrollbarTransform.offsetMax = new Vector2(-6f, -4f);

        var trackImage = scrollbarObject.GetComponent<Image>();
        trackImage.color = new Color(0.11f, 0.13f, 0.15f, 0.55f);

        var handleObject = new GameObject(
            "Handle",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        handleObject.layer = gameObject.layer;
        handleObject.transform.SetParent(scrollbarTransform, false);

        var handleTransform = handleObject.GetComponent<RectTransform>();
        handleTransform.anchorMin = Vector2.zero;
        handleTransform.anchorMax = Vector2.one;
        handleTransform.offsetMin = new Vector2(2f, 2f);
        handleTransform.offsetMax = new Vector2(-2f, -2f);

        var handleImage = handleObject.GetComponent<Image>();
        handleImage.color = new Color(0.72f, 0.48f, 0.14f, 0.9f);

        var scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleTransform;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }

    void RefreshLayoutIfNeeded()
    {
        if (viewport == null || slotContainer == null || gridLayout == null)
            return;

        Vector2 viewportSize = viewport.rect.size;
        if (!layoutDirty && Approximately(viewportSize, lastViewportSize))
            return;

        layoutDirty = false;
        lastViewportSize = viewportSize;

        int columns = Mathf.Max(1, columnCount);
        int visibleRows = Mathf.Max(1, visibleRowCount);
        float spacing = Mathf.Max(0f, slotSpacing);
        bool requiresScrollbar = slotUIs.Count > columns * visibleRows;
        float scrollbarGutter = 0f;
        if (requiresScrollbar &&
            scrollRect != null &&
            scrollRect.verticalScrollbar != null &&
            scrollRect.verticalScrollbar.transform is RectTransform scrollbarTransform)
        {
            scrollbarGutter = Mathf.Max(0f, scrollbarTransform.rect.width + scrollRect.verticalScrollbarSpacing);
        }

        float availableWidth = viewportSize.x - scrollbarGutter - spacing * (columns - 1);
        float cellSize = Mathf.Floor(availableWidth / columns);
        if (cellSize <= 0f)
            return;

        float gridWidth = cellSize * columns + spacing * (columns - 1);
        float remainingWidth = Mathf.Max(0f, viewportSize.x - scrollbarGutter - gridWidth);
        int leftPadding = Mathf.FloorToInt(remainingWidth * 0.5f + scrollbarGutter);
        int rightPadding = Mathf.FloorToInt(remainingWidth * 0.5f);
        float visibleContentHeight = visibleRows * cellSize + Mathf.Max(0, visibleRows - 1) * spacing;
        int rowCount = Mathf.CeilToInt(slotUIs.Count / (float)columns);
        float contentHeight = rowCount > 0
            ? rowCount * cellSize + Mathf.Max(0, rowCount - 1) * spacing
            : 0f;

        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = columns;
        gridLayout.cellSize = new Vector2(cellSize, cellSize);
        gridLayout.spacing = new Vector2(spacing, spacing);
        gridLayout.padding.left = leftPadding;
        gridLayout.padding.right = rightPadding;
        gridLayout.padding.top = 0;
        gridLayout.padding.bottom = 0;

        if (scrollRect != null && scrollRect.transform is RectTransform scrollTransform)
        {
            float viewportVerticalPadding = Mathf.Max(0f, viewport.offsetMin.y - viewport.offsetMax.y);
            scrollTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                visibleContentHeight + viewportVerticalPadding);
        }

        slotContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(slotContainer);
    }

    void CacheDragPointerPosition(PointerEventData eventData)
    {
        if (eventData == null)
        {
            hasDragPointerPosition = false;
            return;
        }

        dragPointerScreenPosition = eventData.position;
        hasDragPointerPosition = true;
    }

    void ApplyDragAutoScroll()
    {
        if (!IsDragging || !hasDragPointerPosition || scrollRect == null || viewport == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport,
                dragPointerScreenPosition,
                ResolveEventCamera(),
                out Vector2 localPointer))
        {
            return;
        }

        float edgeSize = Mathf.Min(Mathf.Max(1f, autoScrollEdgeSize), viewport.rect.height * 0.5f);
        float scrollDirection = 0f;

        if (localPointer.y > viewport.rect.yMax - edgeSize)
            scrollDirection = Mathf.InverseLerp(viewport.rect.yMax - edgeSize, viewport.rect.yMax, localPointer.y);
        else if (localPointer.y < viewport.rect.yMin + edgeSize)
            scrollDirection = -Mathf.InverseLerp(viewport.rect.yMin + edgeSize, viewport.rect.yMin, localPointer.y);

        if (Mathf.Approximately(scrollDirection, 0f))
            return;

        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
            scrollRect.verticalNormalizedPosition + scrollDirection * autoScrollSpeed * Time.unscaledDeltaTime);
    }

    static bool Approximately(Vector2 left, Vector2 right)
    {
        return Mathf.Approximately(left.x, right.x) && Mathf.Approximately(left.y, right.y);
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
