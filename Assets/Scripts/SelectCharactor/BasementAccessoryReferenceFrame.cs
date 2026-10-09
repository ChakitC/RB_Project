using UnityEngine;

/// <summary>Keep the authored 1920x1080 coordinates intact at every window aspect.</summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class BasementAccessoryReferenceFrame : MonoBehaviour
{
    void OnEnable() => Fit();
    void LateUpdate() => Fit();
    void Fit()
    {
        var rect = (RectTransform)transform;
        if (rect.parent is not RectTransform parent) return;
        var referenceSize = new Vector2(1920, 1080);
        float scale = Mathf.Min(parent.rect.width / referenceSize.x, parent.rect.height / referenceSize.y);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = referenceSize;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one * Mathf.Max(0.0001f, scale);
    }
}
