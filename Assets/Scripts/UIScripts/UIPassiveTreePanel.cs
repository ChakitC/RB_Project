using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIPassiveTreePanel : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private PlayerPassiveProgress passiveProgress;

    [Header("Tree View")]
    [SerializeField] private RectTransform nodeContainer;
    [SerializeField] private UIPassiveNodeItem nodePrefab;
    [SerializeField] private RectTransform connectionContainer;
    [SerializeField] private Image connectionPrefab;

    [Header("Detail")]
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private TMP_Text nodeTitleText;
    [SerializeField] private TMP_Text nodeDescriptionText;
    [SerializeField] private TMP_Text requirementText;
    [SerializeField] private Button unlockButton;

    [Header("Visual State")]
    [SerializeField] private Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color availableColor = new Color(0.85f, 0.65f, 0.2f, 1f);
    [SerializeField] private Color unlockedColor = new Color(0.2f, 0.75f, 0.3f, 1f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.95f, 0.55f, 1f);

    readonly List<UIPassiveNodeItem> _nodeItems = new();
    readonly List<Image> _connectionImages = new();

    string _selectedNodeId;
    bool _subscribed;

    void Awake()
    {
        if (nodeContainer == null)
            nodeContainer = transform as RectTransform;

        if (connectionContainer == null)
            connectionContainer = nodeContainer;

        if (unlockButton != null)
            unlockButton.onClick.AddListener(HandleUnlockClicked);
    }

    void Start()
    {
        if (passiveProgress == null)
            passiveProgress = GetComponentInParent<PlayerPassiveProgress>();

        Bind(passiveProgress);
    }

    void OnEnable()
    {
        SubscribeToProgress();
        RefreshAll();
    }

    void OnDisable()
    {
        UnsubscribeFromProgress();
    }

    void OnDestroy()
    {
        if (unlockButton != null)
            unlockButton.onClick.RemoveListener(HandleUnlockClicked);
    }

    public void Bind(PlayerPassiveProgress progress)
    {
        if (ReferenceEquals(passiveProgress, progress))
        {
            if (_nodeItems.Count == 0)
                RebuildTree();

            RefreshAll();
            return;
        }

        UnsubscribeFromProgress();
        passiveProgress = progress;
        SubscribeToProgress();
        RebuildTree();
        RefreshAll();
    }

    public void SelectNode(string nodeId)
    {
        _selectedNodeId = nodeId;
        RefreshAll();
    }

    void HandleProgressChanged()
    {
        RefreshAll();
    }

    void SubscribeToProgress()
    {
        if (_subscribed || passiveProgress == null)
            return;

        passiveProgress.Changed += HandleProgressChanged;
        _subscribed = true;
    }

    void UnsubscribeFromProgress()
    {
        if (!_subscribed || passiveProgress == null)
            return;

        passiveProgress.Changed -= HandleProgressChanged;
        _subscribed = false;
    }

    void RebuildTree()
    {
        ClearNodeItems();
        ClearConnections();

        if (passiveProgress == null || passiveProgress.PassiveTree == null || nodePrefab == null || nodeContainer == null)
            return;

        var nodes = passiveProgress.PassiveTree.nodes;
        PassiveTreeNodeData firstNode = null;
        for (int i = 0; i < nodes.Count; i++)
        {
            var nodeData = nodes[i];
            if (nodeData == null)
                continue;

            if (firstNode == null)
                firstNode = nodeData;

            var nodeItem = Instantiate(nodePrefab, nodeContainer);
            nodeItem.gameObject.SetActive(true);
            _nodeItems.Add(nodeItem);
        }

        if (string.IsNullOrWhiteSpace(_selectedNodeId) && firstNode != null)
            _selectedNodeId = firstNode.RuntimeNodeId;
    }

    void RefreshAll()
    {
        if (passiveProgress == null || passiveProgress.PassiveTree == null)
        {
            SetSummaryTexts(0, "-", "-", "Missing passive progress or tree.");
            SetUnlockInteractable(false);
            return;
        }

        EnsureNodeCountMatchesTree();

        var nodes = passiveProgress.PassiveTree.nodes;
        int itemIndex = 0;
        for (int i = 0; i < nodes.Count && itemIndex < _nodeItems.Count; i++)
        {
            var nodeData = nodes[i];
            if (nodeData == null)
                continue;

            var nodeItem = _nodeItems[itemIndex++];
            if (nodeItem == null)
                continue;

            bool unlocked = passiveProgress.IsUnlocked(nodeData.RuntimeNodeId);
            bool canUnlock = passiveProgress.CanUnlockNode(nodeData.RuntimeNodeId, out _);
            bool selected = string.Equals(_selectedNodeId, nodeData.RuntimeNodeId, System.StringComparison.Ordinal);

            nodeItem.Bind(nodeData == null ? null : this, nodeData, unlocked, canUnlock, selected, lockedColor, availableColor, unlockedColor, selectedColor);
        }

        RebuildConnections();
        RefreshDetailPanel();
    }

    void RefreshDetailPanel()
    {
        if (passiveProgress == null || passiveProgress.PassiveTree == null)
            return;

        if (!passiveProgress.PassiveTree.TryGetNode(_selectedNodeId, out var node) || node == null)
        {
            SetSummaryTexts(passiveProgress.AvailablePoints, "-", "-", "Select a node.");
            SetUnlockInteractable(false);
            return;
        }

        string requirement = BuildRequirementText(node);
        SetSummaryTexts(
            passiveProgress.AvailablePoints,
            node.ResolvedDisplayName,
            node.ResolvedDescription,
            requirement);

        bool canUnlock = passiveProgress.CanUnlockNode(node.RuntimeNodeId, out _);
        SetUnlockInteractable(canUnlock);
    }

    void EnsureNodeCountMatchesTree()
    {
        int targetCount = 0;
        if (passiveProgress != null && passiveProgress.PassiveTree != null && passiveProgress.PassiveTree.nodes != null)
        {
            for (int i = 0; i < passiveProgress.PassiveTree.nodes.Count; i++)
            {
                if (passiveProgress.PassiveTree.nodes[i] != null)
                    targetCount++;
            }
        }

        if (_nodeItems.Count == targetCount)
            return;

        RebuildTree();
    }

    void RebuildConnections()
    {
        ClearConnections();

        if (passiveProgress == null || passiveProgress.PassiveTree == null || connectionPrefab == null || connectionContainer == null)
            return;

        var itemsById = new Dictionary<string, UIPassiveNodeItem>();
        for (int i = 0; i < _nodeItems.Count; i++)
        {
            var nodeItem = _nodeItems[i];
            if (nodeItem == null || string.IsNullOrWhiteSpace(nodeItem.NodeId))
                continue;

            itemsById[nodeItem.NodeId] = nodeItem;
        }

        var nodes = passiveProgress.PassiveTree.nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node == null || node.requiredNodeIds == null)
                continue;

            if (!itemsById.TryGetValue(node.RuntimeNodeId, out var childItem))
                continue;

            for (int j = 0; j < node.requiredNodeIds.Count; j++)
            {
                var requiredNodeId = node.requiredNodeIds[j];
                if (string.IsNullOrWhiteSpace(requiredNodeId))
                    continue;

                if (!itemsById.TryGetValue(requiredNodeId, out var parentItem))
                    continue;

                var line = Instantiate(connectionPrefab, connectionContainer);
                line.gameObject.SetActive(true);
                _connectionImages.Add(line);

                bool unlocked = passiveProgress.IsUnlocked(requiredNodeId) && passiveProgress.IsUnlocked(node.RuntimeNodeId);
                ConfigureConnection(line.rectTransform, parentItem.AnchoredPosition, childItem.AnchoredPosition, unlocked ? unlockedColor : lockedColor);
            }
        }
    }

    void ConfigureConnection(RectTransform connectionRect, Vector2 from, Vector2 to, Color color)
    {
        if (connectionRect == null)
            return;

        Vector2 delta = to - from;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        connectionRect.anchorMin = new Vector2(0.5f, 0.5f);
        connectionRect.anchorMax = new Vector2(0.5f, 0.5f);
        connectionRect.pivot = new Vector2(0f, 0.5f);
        connectionRect.anchoredPosition = from;
        connectionRect.localRotation = Quaternion.Euler(0f, 0f, angle);

        float thickness = connectionRect.sizeDelta.y > 0f ? connectionRect.sizeDelta.y : 4f;
        connectionRect.sizeDelta = new Vector2(length, thickness);

        var image = connectionRect.GetComponent<Image>();
        if (image != null)
            image.color = color;
    }

    string BuildRequirementText(PassiveTreeNodeData node)
    {
        string prerequisiteSummary = "None";
        if (node.requiredNodeIds != null && node.requiredNodeIds.Count > 0)
            prerequisiteSummary = string.Join(", ", node.requiredNodeIds);

        string status;
        if (passiveProgress.IsUnlocked(node.RuntimeNodeId))
            status = "Unlocked";
        else if (passiveProgress.CanUnlockNode(node.RuntimeNodeId, out var reason))
            status = "Ready to unlock";
        else
            status = reason;

        return
            $"Cost: {Mathf.Max(1, node.cost)}\n" +
            $"Required Level: {Mathf.Max(1, node.requiredLevel)}\n" +
            $"Prerequisites: {prerequisiteSummary}\n" +
            $"Status: {status}";
    }

    void HandleUnlockClicked()
    {
        if (passiveProgress == null || string.IsNullOrWhiteSpace(_selectedNodeId))
            return;

        if (!passiveProgress.TryUnlockNode(_selectedNodeId))
            return;

        RebuildConnections();
        RefreshAll();
    }

    void SetSummaryTexts(int points, string title, string description, string requirement)
    {
        if (pointsText != null)
            pointsText.text = points.ToString();

        if (nodeTitleText != null)
            nodeTitleText.text = title;

        if (nodeDescriptionText != null)
            nodeDescriptionText.text = description;

        if (requirementText != null)
            requirementText.text = requirement;
    }

    void SetUnlockInteractable(bool isInteractable)
    {
        if (unlockButton != null)
            unlockButton.interactable = isInteractable;
    }

    void ClearNodeItems()
    {
        for (int i = 0; i < _nodeItems.Count; i++)
        {
            var nodeItem = _nodeItems[i];
            if (nodeItem == null)
                continue;

            if (Application.isPlaying)
                Destroy(nodeItem.gameObject);
            else
                DestroyImmediate(nodeItem.gameObject);
        }

        _nodeItems.Clear();
    }

    void ClearConnections()
    {
        for (int i = 0; i < _connectionImages.Count; i++)
        {
            var connection = _connectionImages[i];
            if (connection == null)
                continue;

            if (Application.isPlaying)
                Destroy(connection.gameObject);
            else
                DestroyImmediate(connection.gameObject);
        }

        _connectionImages.Clear();
    }
}
