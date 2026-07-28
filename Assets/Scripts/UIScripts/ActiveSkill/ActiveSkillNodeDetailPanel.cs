using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ActiveSkillNodeDetailPanel : MonoBehaviour
{
    [Header("Drawer")]
    [SerializeField] RectTransform drawerRoot;
    [SerializeField] RectTransform treeViewport;
    [SerializeField] ActiveSkillTreeView treeView;
    [SerializeField] CanvasGroup drawerGroup;
    [SerializeField] Button closeButton;
    [SerializeField, Min(0f)] float drawerWidth = 440f;
    [SerializeField, Min(0f)] float viewportEdgeInset = 18f;
    [SerializeField, Min(0f)] float transitionDuration = 0.2f;

    [Header("Content")]
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text requirementText;
    [SerializeField] TMP_Text statPreviewText;
    [SerializeField] Button unlockButton;
    [SerializeField] TMP_Text unlockButtonText;

    Action _unlock;
    Action _closeRequested;
    Coroutine _transition;
    bool _isOpen;

    public bool IsOpen => _isOpen;
    public bool IsTransitioning => _transition != null;

    void Awake()
    {
        if (drawerRoot == null)
            drawerRoot = transform as RectTransform;
        if (unlockButton != null)
            unlockButton.onClick.AddListener(HandleUnlock);
        if (closeButton != null)
            closeButton.onClick.AddListener(HandleClose);
        ApplyState(false, true);
    }

    void OnDestroy()
    {
        if (unlockButton != null)
            unlockButton.onClick.RemoveListener(HandleUnlock);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(HandleClose);
    }

    public void SetCloseRequested(Action closeRequested)
    {
        _closeRequested = closeRequested;
    }

    public void Hide(bool immediate = false)
    {
        _unlock = null;
        if (!_isOpen && _transition == null)
        {
            if (immediate)
                ApplyState(false, true);
            return;
        }

        SetOpen(false, immediate);
    }

    public void Show(
        SkillUpgradeNodeData node,
        bool unlocked,
        bool canUnlock,
        string reason,
        FinalSkillStats before,
        FinalSkillStats after,
        Action unlock)
    {
        if (node == null)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);
        _unlock = canUnlock ? unlock : null;

        if (titleText != null)
            titleText.text = node.ResolvedDisplayName;
        if (descriptionText != null)
            descriptionText.text = node.description;
        if (requirementText != null)
            requirementText.text = BuildRequirementText(node, unlocked, reason);
        if (statPreviewText != null)
            statPreviewText.text = BuildStatPreview(before, after);
        if (unlockButton != null)
            unlockButton.interactable = canUnlock;
        if (unlockButtonText != null)
            unlockButtonText.text = unlocked ? "Unlocked" : $"Unlock ({Mathf.Max(1, node.cost)})";

        if (!_isOpen)
            SetOpen(true, false);
    }

    void HandleUnlock()
    {
        _unlock?.Invoke();
    }

    void HandleClose()
    {
        if (_transition != null)
            return;

        _closeRequested?.Invoke();
    }

    void SetOpen(bool open, bool immediate)
    {
        if (_transition != null)
        {
            StopCoroutine(_transition);
            _transition = null;
        }

        _isOpen = open;
        treeView?.SetViewportTransitionActive(!immediate);
        if (immediate || transitionDuration <= 0f || !isActiveAndEnabled)
        {
            ApplyState(open, true);
            treeView?.SetViewportTransitionActive(false);
            treeView?.RefreshViewportBounds();
            return;
        }

        _transition = StartCoroutine(AnimateState(open));
    }

    IEnumerator AnimateState(bool open)
    {
        float elapsed = 0f;
        float startDrawerX = drawerRoot != null ? drawerRoot.anchoredPosition.x : DrawerX(!open);
        float startViewportRight = treeViewport != null ? treeViewport.offsetMax.x : ViewportRight(!open);
        float targetDrawerX = DrawerX(open);
        float targetViewportRight = ViewportRight(open);

        if (drawerGroup != null)
        {
            drawerGroup.alpha = 1f;
            drawerGroup.interactable = open;
            drawerGroup.blocksRaycasts = open;
        }
        if (closeButton != null)
            closeButton.interactable = false;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            t = t * t * (3f - 2f * t);
            ApplyPositions(
                Mathf.Lerp(startDrawerX, targetDrawerX, t),
                Mathf.Lerp(startViewportRight, targetViewportRight, t));
            yield return null;
        }

        ApplyState(open, true);
        _transition = null;
        treeView?.SetViewportTransitionActive(false);
        treeView?.RefreshViewportBounds();
    }

    void ApplyState(bool open, bool updateInteraction)
    {
        ApplyPositions(DrawerX(open), ViewportRight(open));
        if (!updateInteraction)
            return;

        if (drawerGroup != null)
        {
            drawerGroup.alpha = 1f;
            drawerGroup.interactable = open;
            drawerGroup.blocksRaycasts = open;
        }
        if (closeButton != null)
            closeButton.interactable = open;
    }

    void ApplyPositions(float drawerX, float viewportRight)
    {
        if (drawerRoot != null)
        {
            Vector2 position = drawerRoot.anchoredPosition;
            position.x = drawerX;
            drawerRoot.anchoredPosition = position;
        }

        if (treeViewport != null)
        {
            Vector2 offsetMax = treeViewport.offsetMax;
            offsetMax.x = viewportRight;
            treeViewport.offsetMax = offsetMax;
        }
    }

    float DrawerX(bool open)
    {
        float halfWidth = drawerWidth * 0.5f;
        return open ? -(halfWidth + viewportEdgeInset) : halfWidth + viewportEdgeInset;
    }

    float ViewportRight(bool open)
    {
        return open ? -(drawerWidth + viewportEdgeInset) : -viewportEdgeInset;
    }

    static string BuildRequirementText(SkillUpgradeNodeData node, bool unlocked, string reason)
    {
        if (unlocked)
            return "Unlocked";

        var builder = new StringBuilder();
        builder.Append("Character level ").Append(Mathf.Max(1, node.requiredCharacterLevel));
        if (node.requiredNodeIds != null && node.requiredNodeIds.Count > 0)
            builder.Append(" | Requires: ").Append(string.Join(", ", node.requiredNodeIds));
        if (!string.IsNullOrWhiteSpace(reason))
            builder.AppendLine().Append(reason);
        return builder.ToString();
    }

    static string BuildStatPreview(FinalSkillStats before, FinalSkillStats after)
    {
        if (before == null || after == null)
            return string.Empty;

        return
            $"Damage {before.damage:0.##} > {after.damage:0.##}\n" +
            $"Cooldown {before.cooldown:0.##}s > {after.cooldown:0.##}s\n" +
            $"Cost {before.manaCost:0.##} > {after.manaCost:0.##}\n" +
            $"Cast Time {before.castTime:0.##}s > {after.castTime:0.##}s\n" +
            $"Radius {before.areaRadius:0.##} > {after.areaRadius:0.##}\n" +
            $"Projectiles {before.projectileCount} > {after.projectileCount}\n" +
            $"Crit {before.critChance:0.##}% > {after.critChance:0.##}%\n" +
            $"Stagger {before.staggerPower:0.##} > {after.staggerPower:0.##}";
    }
}
