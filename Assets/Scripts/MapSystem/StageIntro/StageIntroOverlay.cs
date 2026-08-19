using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space overlay used by <see cref="StageIntroRig"/>: a fullscreen black fade image,
/// top/bottom letterbox bars, and the hold-to-skip prompt with its progress bar.
/// Built lazily at runtime so authors do not have to hand-assemble the UI in the rig prefab.
/// </summary>
internal sealed class StageIntroOverlay
{
    readonly Color barColor;
    readonly int sortingOrder;

    GameObject root;
    CanvasGroup fadeGroup;
    Image fadeImage;
    RectTransform topBar;
    RectTransform bottomBar;
    CanvasGroup skipGroup;
    TMP_Text skipLabel;
    RectTransform skipProgressFill;
    RectTransform skipProgressTrack;

    float barThickness;

    public StageIntroOverlay(int sortingOrder, Color barColor)
    {
        this.sortingOrder = sortingOrder;
        this.barColor = barColor;
    }

    public bool IsBuilt => root != null;

    public void EnsureBuilt(Transform parent)
    {
        if (root != null)
            return;

        root = new GameObject("StageIntroOverlay",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        scaler.referencePixelsPerUnit = 100f;

        var rootGroup = root.GetComponent<CanvasGroup>();
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        topBar = CreateBar("TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        bottomBar = CreateBar("BottomBar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));

        BuildSkipPrompt();
        BuildFadeLayer();

        SetLetterboxVisible(false);
        SetSkipPromptVisible(false);
        SetFadeAlpha(0f);
    }

    public void Destroy()
    {
        if (root == null)
            return;

        Object.Destroy(root);
        root = null;
        fadeGroup = null;
        fadeImage = null;
        topBar = null;
        bottomBar = null;
        skipGroup = null;
        skipLabel = null;
        skipProgressFill = null;
        skipProgressTrack = null;
    }

    public void SetFadeAlpha(float alpha)
    {
        if (fadeGroup != null)
            fadeGroup.alpha = Mathf.Clamp01(alpha);
    }

    public void SetLetterbox(float thickness01, bool visible)
    {
        barThickness = Mathf.Clamp(thickness01, 0f, 0.45f);
        SetLetterboxVisible(visible);
        UpdateBarHeights();
    }

    public void SetLetterboxVisible(bool visible)
    {
        if (topBar != null)
            topBar.gameObject.SetActive(visible);
        if (bottomBar != null)
            bottomBar.gameObject.SetActive(visible);
    }

    public void SetSkipPromptVisible(bool visible)
    {
        if (skipGroup != null)
            skipGroup.alpha = visible ? 1f : 0f;
    }

    public void SetSkipPrompt(string bindingLabel, float progress01)
    {
        if (skipLabel != null)
            skipLabel.text = string.IsNullOrEmpty(bindingLabel)
                ? "Hold to skip"
                : $"Hold [{bindingLabel}] to skip";

        if (skipProgressFill != null && skipProgressTrack != null)
        {
            float width = skipProgressTrack.rect.width * Mathf.Clamp01(progress01);
            skipProgressFill.sizeDelta = new Vector2(width, skipProgressFill.sizeDelta.y);
        }
    }

    /// <summary>Call every frame while the intro runs so the bars track screen resolution changes.</summary>
    public void Tick()
    {
        UpdateBarHeights();
    }

    void UpdateBarHeights()
    {
        float height = Screen.height * barThickness;
        if (topBar != null)
            topBar.sizeDelta = new Vector2(0f, height);
        if (bottomBar != null)
            bottomBar.sizeDelta = new Vector2(0f, height);
    }

    RectTransform CreateBar(string barName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var barObject = new GameObject(barName, typeof(RectTransform), typeof(Image));
        barObject.transform.SetParent(root.transform, false);

        var image = barObject.GetComponent<Image>();
        image.color = barColor;
        image.raycastTarget = false;

        var rect = barObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    void BuildSkipPrompt()
    {
        var promptObject = new GameObject("SkipPrompt", typeof(RectTransform), typeof(CanvasGroup));
        promptObject.transform.SetParent(root.transform, false);

        var promptRect = promptObject.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(1f, 0f);
        promptRect.anchorMax = new Vector2(1f, 0f);
        promptRect.pivot = new Vector2(1f, 0f);
        promptRect.anchoredPosition = new Vector2(-64f, 96f);
        promptRect.sizeDelta = new Vector2(320f, 48f);

        skipGroup = promptObject.GetComponent<CanvasGroup>();
        skipGroup.interactable = false;
        skipGroup.blocksRaycasts = false;

        var labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(promptObject.transform, false);
        skipLabel = labelObject.AddComponent<TextMeshProUGUI>();
        skipLabel.fontSize = 22f;
        skipLabel.alignment = TextAlignmentOptions.MidlineRight;
        skipLabel.color = new Color(1f, 1f, 1f, 0.85f);
        skipLabel.raycastTarget = false;
        skipLabel.text = "Hold to skip";

        var labelRect = skipLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0.35f);
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        skipProgressTrack = CreateSolidRect(promptObject.transform, "ProgressTrack",
            new Color(1f, 1f, 1f, 0.18f));
        skipProgressTrack.anchorMin = new Vector2(0f, 0f);
        skipProgressTrack.anchorMax = new Vector2(1f, 0f);
        skipProgressTrack.pivot = new Vector2(0f, 0f);
        skipProgressTrack.anchoredPosition = Vector2.zero;
        skipProgressTrack.sizeDelta = new Vector2(0f, 4f);

        skipProgressFill = CreateSolidRect(skipProgressTrack, "ProgressFill",
            new Color(1f, 1f, 1f, 0.9f));
        skipProgressFill.anchorMin = new Vector2(0f, 0f);
        skipProgressFill.anchorMax = new Vector2(0f, 1f);
        skipProgressFill.pivot = new Vector2(0f, 0f);
        skipProgressFill.anchoredPosition = Vector2.zero;
        skipProgressFill.sizeDelta = new Vector2(0f, 0f);
    }

    void BuildFadeLayer()
    {
        var fadeObject = new GameObject("Fade", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        fadeObject.transform.SetParent(root.transform, false);
        fadeObject.transform.SetAsLastSibling();

        fadeGroup = fadeObject.GetComponent<CanvasGroup>();
        fadeGroup.interactable = false;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.alpha = 0f;

        fadeImage = fadeObject.GetComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;

        var rect = fadeObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static RectTransform CreateSolidRect(Transform parent, string objectName, Color color)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return go.GetComponent<RectTransform>();
    }
}
