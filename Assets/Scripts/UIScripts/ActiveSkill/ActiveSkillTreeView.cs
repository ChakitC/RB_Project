using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ActiveSkillTreeView : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] RectTransform viewport;
    [SerializeField] RectTransform contentRoot;
    [SerializeField] RectTransform connectionRoot;
    [SerializeField] RectTransform nodeRoot;
    [SerializeField] ActiveSkillUpgradeNodeView nodePrefab;
    [SerializeField] ActiveSkillTreeConnectionView connectionPrefab;
    [SerializeField] SkillScreenTheme theme;
    [SerializeField, Min(0f)] float fitPadding = 120f;
    [SerializeField, Min(1f)] float maxZoomScale = 2f;
    [SerializeField, Min(1.01f)] float zoomFactorPerNotch = 1.1f;

    readonly List<ActiveSkillUpgradeNodeView> _nodePool = new();
    readonly List<ActiveSkillTreeConnectionView> _connectionPool = new();
    readonly Dictionary<string, ActiveSkillUpgradeNodeView> _nodesById = new();

    ActiveSkillLoadoutSession _session;
    SkillUpgradeTreeDefinition _tree;
    Action<string> _nodeClicked;
    int _slotIndex;
    int _optionIndex;
    string _selectedNodeId;
    Vector2 _graphBoundsSize;
    Vector2 _lastPointerLocal;
    float _fitScale = 1f;
    float _currentScale = 1f;
    bool _hasGraphLayout;
    bool _isPanning;

    public void Bind(
        ActiveSkillLoadoutSession session,
        int slotIndex,
        int optionIndex,
        SkillUpgradeTreeDefinition tree,
        string selectedNodeId,
        Action<string> nodeClicked)
    {
        _session = session;
        _slotIndex = slotIndex;
        _optionIndex = optionIndex;
        _tree = tree;
        _selectedNodeId = selectedNodeId;
        _nodeClicked = nodeClicked;
        Rebuild();
    }

    public void Refresh(string selectedNodeId)
    {
        _selectedNodeId = selectedNodeId;
        RefreshNodes();
        RefreshConnections();
    }

    public void ResetView()
    {
        if (!_hasGraphLayout || contentRoot == null)
            return;

        _currentScale = _fitScale;
        ApplyScale();
        contentRoot.anchoredPosition = Vector2.zero;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (!_hasGraphLayout || contentRoot == null || viewport == null ||
            eventData == null || Mathf.Approximately(eventData.scrollDelta.y, 0f))
        {
            return;
        }

        float factor = Mathf.Max(1.01f, zoomFactorPerNotch);
        float requestedScale = eventData.scrollDelta.y > 0f
            ? _currentScale * factor
            : _currentScale / factor;
        float newScale = Mathf.Clamp(requestedScale, _fitScale, ResolvedMaxZoomScale);
        if (Mathf.Approximately(newScale, _currentScale) ||
            !TryGetViewportLocalPoint(eventData.position, eventData.enterEventCamera, out Vector2 pointerLocal))
        {
            return;
        }

        Vector2 zoomedPosition = CalculateZoomedPosition(
            contentRoot.anchoredPosition,
            pointerLocal,
            _currentScale,
            newScale);
        _currentScale = newScale;
        ApplyScale();
        contentRoot.anchoredPosition = ClampPan(zoomedPosition);
        eventData.Use();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _isPanning = false;
        if (!_hasGraphLayout || eventData == null || eventData.button != PointerEventData.InputButton.Left ||
            eventData.pointerPressRaycast.gameObject != gameObject)
        {
            return;
        }

        if (!TryGetViewportLocalPoint(eventData.position, eventData.pressEventCamera, out _lastPointerLocal))
            return;

        _isPanning = true;
        eventData.Use();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isPanning || contentRoot == null || eventData == null ||
            !TryGetViewportLocalPoint(eventData.position, eventData.pressEventCamera, out Vector2 pointerLocal))
        {
            return;
        }

        Vector2 delta = pointerLocal - _lastPointerLocal;
        _lastPointerLocal = pointerLocal;
        contentRoot.anchoredPosition = ClampPan(contentRoot.anchoredPosition + delta);
        eventData.Use();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isPanning)
            return;

        _isPanning = false;
        eventData?.Use();
    }

    void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled && _tree != null && _hasGraphLayout)
            LayoutGraph(false);
    }

    void Rebuild()
    {
        SetPoolInactive(_nodePool);
        SetPoolInactive(_connectionPool);
        _nodesById.Clear();
        _hasGraphLayout = false;
        _isPanning = false;

        if (_session == null || _tree == null || _tree.nodes == null)
        {
            ResetContentTransform();
            return;
        }

        int nodeIndex = 0;
        for (int i = 0; i < _tree.nodes.Count; i++)
        {
            SkillUpgradeNodeData node = _tree.nodes[i];
            if (node == null || string.IsNullOrWhiteSpace(node.RuntimeNodeId))
                continue;

            ActiveSkillUpgradeNodeView view = GetNode(nodeIndex++);
            view.gameObject.SetActive(true);
            _nodesById[node.RuntimeNodeId] = view;
        }

        RefreshNodes();
        RefreshConnections();
        LayoutGraph(true);
    }

    void RefreshNodes()
    {
        if (_tree == null || _tree.nodes == null)
            return;

        int viewIndex = 0;
        for (int i = 0; i < _tree.nodes.Count; i++)
        {
            SkillUpgradeNodeData node = _tree.nodes[i];
            if (node == null || string.IsNullOrWhiteSpace(node.RuntimeNodeId) || viewIndex >= _nodePool.Count)
                continue;

            bool unlocked = _session.IsUnlocked(_slotIndex, _optionIndex, node.RuntimeNodeId);
            bool available = !unlocked && _session.CanUnlock(_slotIndex, _optionIndex, node.RuntimeNodeId, out _);
            ActiveSkillNodeVisualState state = unlocked
                ? ActiveSkillNodeVisualState.Unlocked
                : available ? ActiveSkillNodeVisualState.Available : ActiveSkillNodeVisualState.Locked;

            ActiveSkillUpgradeNodeView view = _nodePool[viewIndex++];
            view.Bind(
                node,
                state,
                string.Equals(_selectedNodeId, node.RuntimeNodeId, StringComparison.Ordinal),
                theme,
                _nodeClicked);
        }
    }

    void RefreshConnections()
    {
        SetPoolInactive(_connectionPool);
        if (_tree == null || _tree.nodes == null)
            return;

        int connectionIndex = 0;
        for (int i = 0; i < _tree.nodes.Count; i++)
        {
            SkillUpgradeNodeData child = _tree.nodes[i];
            if (child == null || child.requiredNodeIds == null ||
                !_nodesById.TryGetValue(child.RuntimeNodeId, out ActiveSkillUpgradeNodeView childView))
            {
                continue;
            }

            for (int j = 0; j < child.requiredNodeIds.Count; j++)
            {
                string parentId = child.requiredNodeIds[j];
                if (string.IsNullOrWhiteSpace(parentId) ||
                    !_nodesById.TryGetValue(parentId.Trim(), out ActiveSkillUpgradeNodeView parentView))
                {
                    continue;
                }

                ActiveSkillTreeConnectionView connection = GetConnection(connectionIndex++);
                connection.gameObject.SetActive(true);
                connection.Bind(
                    parentView.AnchoredPosition,
                    childView.AnchoredPosition,
                    _session.IsUnlocked(_slotIndex, _optionIndex, parentId),
                    theme);
            }
        }
    }

    void LayoutGraph(bool resetView)
    {
        if (viewport == null || contentRoot == null || _tree == null || _tree.nodes == null || _tree.nodes.Count == 0)
            return;

        bool hasBounds = false;
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;
        for (int i = 0; i < _tree.nodes.Count; i++)
        {
            SkillUpgradeNodeData node = _tree.nodes[i];
            if (node == null)
                continue;

            Rect bounds = node.RuntimeUiBounds;
            if (!hasBounds)
            {
                min = bounds.min;
                max = bounds.max;
                hasBounds = true;
            }
            else
            {
                min = Vector2.Min(min, bounds.min);
                max = Vector2.Max(max, bounds.max);
            }
        }

        if (!hasBounds)
        {
            _hasGraphLayout = false;
            ResetContentTransform();
            return;
        }

        Vector2 center = (min + max) * 0.5f;
        for (int i = 0; i < _tree.nodes.Count; i++)
        {
            SkillUpgradeNodeData node = _tree.nodes[i];
            if (node != null && _nodesById.TryGetValue(node.RuntimeNodeId, out ActiveSkillUpgradeNodeView view) &&
                view.transform is RectTransform rect)
            {
                Vector2 editorOffset = node.uiPosition - center;
                rect.anchoredPosition = new Vector2(editorOffset.x, -editorOffset.y);
            }
        }

        RefreshConnections();

        _graphBoundsSize = max - min + Vector2.one * fitPadding;
        Vector2 viewportSize = viewport.rect.size;
        float scaleX = _graphBoundsSize.x > 0f ? viewportSize.x / _graphBoundsSize.x : 1f;
        float scaleY = _graphBoundsSize.y > 0f ? viewportSize.y / _graphBoundsSize.y : 1f;
        _fitScale = Mathf.Min(1f, scaleX, scaleY);
        if (!float.IsFinite(_fitScale) || _fitScale <= 0f)
            _fitScale = 1f;

        contentRoot.anchorMin = contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        contentRoot.pivot = new Vector2(0.5f, 0.5f);
        _hasGraphLayout = true;

        if (resetView)
        {
            ResetView();
            return;
        }

        _currentScale = Mathf.Clamp(_currentScale, _fitScale, ResolvedMaxZoomScale);
        ApplyScale();
        contentRoot.anchoredPosition = ClampPan(contentRoot.anchoredPosition);
    }

    float ResolvedMaxZoomScale => Mathf.Max(_fitScale, Mathf.Max(1f, maxZoomScale));

    void ApplyScale()
    {
        contentRoot.localScale = new Vector3(_currentScale, _currentScale, 1f);
    }

    Vector2 ClampPan(Vector2 requestedPosition)
    {
        if (viewport == null)
            return Vector2.zero;

        return CalculateClampedPan(requestedPosition, _graphBoundsSize, viewport.rect.size, _currentScale);
    }

    static Vector2 CalculateZoomedPosition(
        Vector2 currentPosition,
        Vector2 pointerLocal,
        float oldScale,
        float newScale)
    {
        if (oldScale <= 0f || newScale <= 0f)
            return currentPosition;

        Vector2 contentPoint = (pointerLocal - currentPosition) / oldScale;
        return pointerLocal - contentPoint * newScale;
    }

    static Vector2 CalculateClampedPan(
        Vector2 requestedPosition,
        Vector2 graphBoundsSize,
        Vector2 viewportSize,
        float scale)
    {
        Vector2 overflow = Vector2.Max(
            Vector2.zero,
            (graphBoundsSize * scale - viewportSize) * 0.5f);
        return new Vector2(
            overflow.x > 0f ? Mathf.Clamp(requestedPosition.x, -overflow.x, overflow.x) : 0f,
            overflow.y > 0f ? Mathf.Clamp(requestedPosition.y, -overflow.y, overflow.y) : 0f);
    }

    bool TryGetViewportLocalPoint(Vector2 screenPoint, Camera eventCamera, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        return viewport != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPoint, eventCamera, out localPoint);
    }

    void ResetContentTransform()
    {
        if (contentRoot == null)
            return;

        _fitScale = 1f;
        _currentScale = 1f;
        _graphBoundsSize = Vector2.zero;
        contentRoot.anchoredPosition = Vector2.zero;
        contentRoot.localScale = Vector3.one;
    }

    ActiveSkillUpgradeNodeView GetNode(int index)
    {
        while (_nodePool.Count <= index)
            _nodePool.Add(Instantiate(nodePrefab, nodeRoot));
        return _nodePool[index];
    }

    ActiveSkillTreeConnectionView GetConnection(int index)
    {
        while (_connectionPool.Count <= index)
            _connectionPool.Add(Instantiate(connectionPrefab, connectionRoot));
        return _connectionPool[index];
    }

    static void SetPoolInactive<T>(List<T> pool) where T : Component
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null)
                pool[i].gameObject.SetActive(false);
        }
    }
}
