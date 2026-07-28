using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ActiveSkillScreenController : MonoBehaviour
{
    [SerializeField, HideInInspector] int placeholderLayoutVersion;

    [Header("Screen")]
    [SerializeField] CanvasGroup screenGroup;
    [SerializeField] Button backButton;
    [SerializeField] TMP_Text pointsText;
    [SerializeField] TMP_Text emptyStateText;
    [SerializeField] Button resetTreeButton;
    [SerializeField] SkillScreenTheme theme;

    [Header("Slots")]
    [SerializeField] RectTransform slotTabRoot;
    [SerializeField] ActiveSkillSlotTabView slotTabPrefab;

    [Header("Variants")]
    [SerializeField] ActiveSkillVariantCardView selectedVariantCard;
    [SerializeField] RectTransform variantListRoot;
    [SerializeField] ActiveSkillVariantCardView variantCardPrefab;

    [Header("Tree")]
    [SerializeField] ActiveSkillTreeView treeView;
    [SerializeField] Button fitTreeButton;
    [SerializeField] GameObject treeNavigationTooltip;
    [SerializeField] ActiveSkillNodeDetailPanel detailPanel;
    [SerializeField] ActiveSkillConfirmationDialog confirmationDialog;

    readonly List<ActiveSkillSlotTabView> _slotTabs = new();
    readonly List<ActiveSkillVariantCardView> _variantCards = new();

    ActiveSkillLoadoutSession _session;
    int _selectedSlotIndex;
    int _selectedOptionIndex = -1;
    string _selectedNodeId;
    UIStateId _previousUiState;
    bool _changedUiState;
    bool _fallbackPaused;
    bool _isOpen;

    public event Action Closed;
    public int PlaceholderLayoutVersion => placeholderLayoutVersion;

    void Awake()
    {
        if (backButton != null)
            backButton.onClick.AddListener(Close);
        if (resetTreeButton != null)
            resetTreeButton.onClick.AddListener(RequestResetTree);
        if (fitTreeButton != null)
            fitTreeButton.onClick.AddListener(ResetTreeView);
        detailPanel?.SetCloseRequested(CloseNodeDetail);
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (backButton != null)
            backButton.onClick.RemoveListener(Close);
        if (resetTreeButton != null)
            resetTreeButton.onClick.RemoveListener(RequestResetTree);
        if (fitTreeButton != null)
            fitTreeButton.onClick.RemoveListener(ResetTreeView);
        detailPanel?.SetCloseRequested(null);
        ReleaseSession();
    }

    public void BindRuntime(CharacteContext context)
    {
        BindSession(ActiveSkillLoadoutSession.CreateRuntime(context));
    }

    public void BindLobby(CharacterStats stats)
    {
        BindSession(ActiveSkillLoadoutSession.CreateLobby(stats));
    }

    public void Open()
    {
        if (_session == null)
        {
            Debug.LogWarning("[ActiveSkillScreen] Bind a character before opening the screen.", this);
            return;
        }

        PauseRuntimeIfNeeded();
        _isOpen = true;
        SetVisible(true);
        RebuildScreen();
    }

    public void Close()
    {
        if (!_isOpen)
            return;

        _isOpen = false;
        confirmationDialog?.Hide();
        _selectedNodeId = null;
        detailPanel?.Hide(true);
        treeView?.Refresh(null);
        SetVisible(false);
        RestoreRuntimeState();
        Closed?.Invoke();
    }

    void BindSession(ActiveSkillLoadoutSession session)
    {
        ReleaseSession();
        _session = session;
        if (_session != null)
        {
            _session.Changed += HandleSessionChanged;
            _session.ProgressChanged += HandleProgressChanged;
        }

        _selectedSlotIndex = 0;
        _selectedOptionIndex = -1;
        _selectedNodeId = null;
        if (_isOpen)
            RebuildScreen();
    }

    void ReleaseSession()
    {
        if (_session == null)
            return;

        _session.Changed -= HandleSessionChanged;
        _session.ProgressChanged -= HandleProgressChanged;
        _session.Dispose();
        _session = null;
    }

    void RebuildScreen()
    {
        CharacterStats stats = _session != null ? _session.Stats : null;
        int slotCount = stats != null && stats.skillSlots != null ? stats.skillSlots.Count : 0;
        _selectedSlotIndex = slotCount > 0 ? Mathf.Clamp(_selectedSlotIndex, 0, slotCount - 1) : 0;

        RebuildSlots(slotCount);
        RefreshPoints();

        if (slotCount == 0 || stats.skillSlots[_selectedSlotIndex] == null)
        {
            ShowEmpty("No skill slots configured.");
            ClearVariantsAndTree();
            return;
        }

        CharacterSkillLoadoutSlot slot = stats.skillSlots[_selectedSlotIndex];
        _selectedOptionIndex = _session.GetSelectedOptionIndex(_selectedSlotIndex);
        if (_selectedOptionIndex < 0 || !slot.TryGetOption(_selectedOptionIndex, out CharacterSkillLoadoutOption selectedOption))
        {
            ShowEmpty("No skill variants configured for this slot.");
            ClearVariantsAndTree();
            return;
        }

        ShowEmpty(null);
        RebuildVariants(slot, selectedOption);
        RebuildTree(selectedOption);
    }

    void RebuildSlots(int slotCount)
    {
        EnsurePool(_slotTabs, slotCount, slotTabPrefab, slotTabRoot);
        for (int i = 0; i < _slotTabs.Count; i++)
        {
            bool active = i < slotCount;
            _slotTabs[i].gameObject.SetActive(active);
            if (!active)
                continue;

            CharacterSkillLoadoutSlot slot = _session.Stats.skillSlots[i];
            string label = slot != null && !string.IsNullOrWhiteSpace(slot.displayName)
                ? slot.displayName.Trim()
                : BuildSlotLabel(i);
            _slotTabs[i].Bind(i, label, i == _selectedSlotIndex, theme, SelectSlot);
        }
    }

    void RebuildVariants(CharacterSkillLoadoutSlot slot, CharacterSkillLoadoutOption selectedOption)
    {
        if (selectedVariantCard != null)
            selectedVariantCard.gameObject.SetActive(true);
        selectedVariantCard?.Bind(selectedOption, _selectedOptionIndex, true, theme, null);
        int otherCount = Mathf.Max(0, slot.Options.Count - 1);
        EnsurePool(_variantCards, otherCount, variantCardPrefab, variantListRoot);

        int viewIndex = 0;
        for (int optionIndex = 0; optionIndex < slot.Options.Count; optionIndex++)
        {
            if (optionIndex == _selectedOptionIndex)
                continue;

            CharacterSkillLoadoutOption option = slot.Options[optionIndex];
            ActiveSkillVariantCardView view = _variantCards[viewIndex++];
            view.gameObject.SetActive(true);
            view.Bind(option, optionIndex, false, theme, SelectVariant);
        }

        for (int i = viewIndex; i < _variantCards.Count; i++)
            _variantCards[i].gameObject.SetActive(false);
    }

    void RebuildTree(CharacterSkillLoadoutOption option)
    {
        _selectedNodeId = null;
        detailPanel?.Hide();

        SkillUpgradeTreeDefinition tree = option != null ? option.ResolvedUpgradeTree : null;
        bool hasTree = tree != null;
        if (resetTreeButton != null)
            resetTreeButton.interactable = hasTree;
        if (fitTreeButton != null)
            fitTreeButton.interactable = hasTree;

        if (!hasTree)
        {
            ShowEmpty("No Active Skill Tree assigned to this variant.");
            treeView?.Bind(_session, _selectedSlotIndex, _selectedOptionIndex, null, null, SelectNode);
            return;
        }

        treeView?.Bind(
            _session,
            _selectedSlotIndex,
            _selectedOptionIndex,
            tree,
            _selectedNodeId,
            SelectNode);
    }

    void SelectSlot(int slotIndex)
    {
        _selectedSlotIndex = slotIndex;
        _selectedNodeId = null;
        confirmationDialog?.Hide();
        RebuildScreen();
    }

    void SelectVariant(int optionIndex)
    {
        if (_session != null && _session.SelectOption(_selectedSlotIndex, optionIndex))
        {
            _selectedOptionIndex = optionIndex;
            _selectedNodeId = null;
        }
    }

    void SelectNode(string nodeId)
    {
        if (!TryGetSelectedNode(nodeId, out SkillUpgradeNodeData node))
            return;

        _selectedNodeId = node.RuntimeNodeId;
        bool unlocked = _session.IsUnlocked(_selectedSlotIndex, _selectedOptionIndex, node.RuntimeNodeId);
        string reason = unlocked ? "Already unlocked." : string.Empty;
        bool canUnlock = !unlocked && _session.CanUnlock(
            _selectedSlotIndex,
            _selectedOptionIndex,
            node.RuntimeNodeId,
            out reason);

        FinalSkillStats before = _session.BuildStatsPreview(_selectedSlotIndex, _selectedOptionIndex);
        FinalSkillStats after = unlocked
            ? before
            : _session.BuildStatsPreview(_selectedSlotIndex, _selectedOptionIndex, node);

        detailPanel?.Show(
            node,
            unlocked,
            canUnlock,
            reason,
            before,
            after,
            () => RequestUnlockNode(node.RuntimeNodeId));
        treeView?.Refresh(_selectedNodeId);
    }

    void CloseNodeDetail()
    {
        _selectedNodeId = null;
        detailPanel?.Hide();
        treeView?.Refresh(null);
    }

    void RequestUnlockNode(string nodeId)
    {
        if (!TryGetSelectedNode(nodeId, out SkillUpgradeNodeData node))
            return;

        if (confirmationDialog == null)
        {
            UnlockSelectedNode(node.RuntimeNodeId);
            return;
        }

        confirmationDialog.Show(
            $"Unlock '{node.ResolvedDisplayName}' for {Mathf.Max(1, node.cost)} Active Skill Point" +
            (Mathf.Max(1, node.cost) == 1 ? "?" : "s?"),
            () => UnlockSelectedNode(node.RuntimeNodeId));
    }

    void UnlockSelectedNode(string nodeId)
    {
        if (!_session.TryUnlock(_selectedSlotIndex, _selectedOptionIndex, nodeId, out string reason))
        {
            if (TryGetSelectedNode(nodeId, out SkillUpgradeNodeData node))
                SelectNode(node.RuntimeNodeId);
            Debug.LogWarning($"[ActiveSkillScreen] Could not unlock node '{nodeId}': {reason}", this);
            return;
        }

        RefreshPoints();
        SelectNode(nodeId);
    }

    void RequestResetTree()
    {
        if (_session == null || _selectedOptionIndex < 0)
            return;

        if (confirmationDialog != null)
        {
            confirmationDialog.Show(
                "Reset this variant's entire Active Skill Tree and refund all spent points?",
                ResetSelectedTree);
        }
        else
        {
            ResetSelectedTree();
        }
    }

    void ResetTreeView()
    {
        treeView?.ResetView();
    }

    public void ShowTreeNavigationTooltip(BaseEventData eventData)
    {
        if (treeNavigationTooltip != null)
            treeNavigationTooltip.SetActive(true);
    }

    public void HideTreeNavigationTooltip(BaseEventData eventData)
    {
        if (treeNavigationTooltip != null)
            treeNavigationTooltip.SetActive(false);
    }

    public void HandleTreeNavigationScroll(BaseEventData eventData)
    {
        if (eventData is PointerEventData pointerEventData)
            treeView?.OnScroll(pointerEventData);
    }

    void ResetSelectedTree()
    {
        if (_session == null || !_session.ResetTree(_selectedSlotIndex, _selectedOptionIndex, out _))
            return;

        _selectedNodeId = null;
        detailPanel?.Hide();
        RefreshPoints();
        treeView?.Refresh(null);
    }

    void HandleSessionChanged()
    {
        if (_isOpen)
            RebuildScreen();
    }

    void HandleProgressChanged()
    {
        if (!_isOpen)
            return;

        RefreshPoints();
        treeView?.Refresh(_selectedNodeId);
        if (!string.IsNullOrWhiteSpace(_selectedNodeId))
            SelectNode(_selectedNodeId);
    }

    void PauseRuntimeIfNeeded()
    {
        CharacteContext runtime = _session.RuntimeContext;
        if (runtime == null)
            return;

        runtime.ResolveReferences();
        if (runtime.stateHub != null && runtime.stateHub.UISM != null)
        {
            _previousUiState = runtime.stateHub.UISM.CurrentId;
            _changedUiState = _previousUiState != UIStateId.Pause && runtime.stateHub.UISM.TryChange(UIStateId.Pause);
            return;
        }

        GlobalTimeScaleManager.Instance.SetPaused(true);
        _fallbackPaused = true;
    }

    void RestoreRuntimeState()
    {
        CharacteContext runtime = _session != null ? _session.RuntimeContext : null;
        if (_changedUiState && runtime != null && runtime.stateHub != null && runtime.stateHub.UISM != null)
            runtime.stateHub.UISM.TryChange(_previousUiState);
        if (_fallbackPaused)
            GlobalTimeScaleManager.Instance.SetPaused(false);

        _changedUiState = false;
        _fallbackPaused = false;
    }

    bool TryGetSelectedNode(string nodeId, out SkillUpgradeNodeData node)
    {
        node = null;
        if (_session == null || _session.Stats == null || _session.Stats.skillSlots == null ||
            _selectedSlotIndex < 0 || _selectedSlotIndex >= _session.Stats.skillSlots.Count)
        {
            return false;
        }

        CharacterSkillLoadoutSlot slot = _session.Stats.skillSlots[_selectedSlotIndex];
        return slot != null &&
               slot.TryGetOption(_selectedOptionIndex, out CharacterSkillLoadoutOption option) &&
               option.ResolvedUpgradeTree != null &&
               option.ResolvedUpgradeTree.TryGetNode(nodeId, out node);
    }

    void RefreshPoints()
    {
        if (pointsText != null)
            pointsText.text = $"Active Skill Points: {_session?.AvailablePoints ?? 0}";
    }

    void ClearVariantsAndTree()
    {
        _selectedNodeId = null;
        if (selectedVariantCard != null)
            selectedVariantCard.gameObject.SetActive(false);
        for (int i = 0; i < _variantCards.Count; i++)
            _variantCards[i].gameObject.SetActive(false);
        detailPanel?.Hide();
        treeView?.Bind(_session, _selectedSlotIndex, -1, null, null, SelectNode);
        if (resetTreeButton != null)
            resetTreeButton.interactable = false;
        if (fitTreeButton != null)
            fitTreeButton.interactable = false;
    }

    void ShowEmpty(string message)
    {
        if (emptyStateText == null)
            return;

        emptyStateText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        emptyStateText.text = message ?? string.Empty;
    }

    void SetVisible(bool visible)
    {
        if (!visible)
            HideTreeNavigationTooltip(null);

        if (screenGroup != null)
        {
            screenGroup.alpha = visible ? 1f : 0f;
            screenGroup.interactable = visible;
            screenGroup.blocksRaycasts = visible;
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    static void EnsurePool<T>(List<T> pool, int count, T prefab, Transform parent) where T : Component
    {
        if (prefab == null || parent == null)
            return;

        while (pool.Count < count)
            pool.Add(Instantiate(prefab, parent));
    }

    static string BuildSlotLabel(int index)
    {
        return index switch
        {
            0 => "SKILL I",
            1 => "SKILL II",
            2 => "SKILL III",
            _ => $"SKILL {index + 1}",
        };
    }
}
