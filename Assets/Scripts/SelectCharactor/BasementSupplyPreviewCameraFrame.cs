using UnityEngine;

/// <summary>Keep the authored camera composition aligned with the preview's 16:9 artboard.</summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public sealed class BasementSupplyPreviewCameraFrame : MonoBehaviour
{
    private Camera previewCamera;
    private Rect originalRect;

    void OnEnable()
    {
        previewCamera = GetComponent<Camera>();
        originalRect = previewCamera.rect;
        LateUpdate();
    }

    void LateUpdate()
    {
        if (previewCamera == null || Screen.width <= 0 || Screen.height <= 0) return;
        float relativeAspect = (Screen.width / (float)Screen.height) / (16f / 9f);
        previewCamera.rect = relativeAspect > 1f
            ? new Rect((1f - 1f / relativeAspect) * .5f, 0, 1f / relativeAspect, 1)
            : new Rect(0, (1f - relativeAspect) * .5f, 1, relativeAspect);
    }

    void OnDisable()
    {
        if (previewCamera != null) previewCamera.rect = originalRect;
    }
}
