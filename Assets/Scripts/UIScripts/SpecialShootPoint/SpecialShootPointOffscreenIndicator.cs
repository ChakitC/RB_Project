using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Directional markers for Special Shoot Points that are outside the camera frame.
///
/// Strictly off-screen only. A point that is inside the frame but occluded by the enemy or the
/// environment deliberately gets no helper: the locked design has no through-model rendering and no
/// occlusion indicator, and the player is expected to move to see it. There is also no aim assist
/// or projectile magnetism anywhere in this feature — the reticle has to be put on the collider.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpecialShootPointOffscreenIndicator : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Parent for pooled markers. Should be a screen-space canvas RectTransform.")]
    [SerializeField] private RectTransform markerRoot;

    [Tooltip("Marker prefab. One is pooled per concurrent off-screen point.")]
    [SerializeField] private RectTransform markerPrefab;

    [Tooltip("Camera used for the projection. Falls back to Camera.main.")]
    [SerializeField] private Camera worldCamera;

    [Header("Layout")]
    [Tooltip("Inset from the screen edge, in pixels.")]
    [Min(0f)][SerializeField] private float edgePadding = 48f;

    readonly List<RectTransform> _markerPool = new();

    SpecialShootPointController _bound;

    void OnEnable()
    {
        SpecialShootPointController.AnyRoundStarted += OnAnyRoundStarted;
        SpecialShootPointController.AnyRoundResolved += OnAnyRoundResolved;
        HideAllMarkers();
    }

    void OnDisable()
    {
        SpecialShootPointController.AnyRoundStarted -= OnAnyRoundStarted;
        SpecialShootPointController.AnyRoundResolved -= OnAnyRoundResolved;
        _bound = null;
        HideAllMarkers();
    }

    void OnAnyRoundStarted(SpecialShootPointController controller)
    {
        _bound = controller;
    }

    void OnAnyRoundResolved(SpecialShootPointController controller, SpecialShootPointOutcome outcome)
    {
        if (controller != _bound)
            return;

        _bound = null;
        HideAllMarkers();
    }

    void LateUpdate()
    {
        // No live round is the overwhelmingly common case, and it costs one null check.
        if (_bound == null || !_bound.IsRoundActive)
            return;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null || markerRoot == null || markerPrefab == null)
            return;

        IReadOnlyList<SpecialShootPointInstance> points = _bound.ActivePoints;
        int used = 0;

        for (int i = 0; i < points.Count; i++)
        {
            SpecialShootPointInstance point = points[i];
            if (point == null || !point.IsAlive)
                continue;

            Vector3 viewport = cam.WorldToViewportPoint(point.WorldPosition);
            bool onScreen = viewport.z > 0f &&
                            viewport.x >= 0f && viewport.x <= 1f &&
                            viewport.y >= 0f && viewport.y <= 1f;

            if (onScreen)
                continue;

            RectTransform marker = RentMarker(used++);
            PlaceMarker(marker, cam, viewport);
        }

        for (int i = used; i < _markerPool.Count; i++)
        {
            RectTransform marker = _markerPool[i];
            if (marker != null && marker.gameObject.activeSelf)
                marker.gameObject.SetActive(false);
        }
    }

    void PlaceMarker(RectTransform marker, Camera cam, Vector3 viewport)
    {
        // A point behind the camera projects to a mirrored viewport position, so it has to be
        // flipped before it is clamped or the marker points the wrong way.
        if (viewport.z < 0f)
        {
            viewport.x = 1f - viewport.x;
            viewport.y = 1f - viewport.y;
        }

        Vector2 screen = new(viewport.x * Screen.width, viewport.y * Screen.height);
        Vector2 center = new(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 fromCenter = screen - center;

        if (fromCenter.sqrMagnitude < 0.0001f)
            fromCenter = Vector2.up;

        float maxX = Mathf.Max(0f, center.x - edgePadding);
        float maxY = Mathf.Max(0f, center.y - edgePadding);

        // Scale the direction so it lands exactly on the padded screen rectangle.
        float scaleX = Mathf.Abs(fromCenter.x) > 0.0001f ? maxX / Mathf.Abs(fromCenter.x) : float.MaxValue;
        float scaleY = Mathf.Abs(fromCenter.y) > 0.0001f ? maxY / Mathf.Abs(fromCenter.y) : float.MaxValue;
        float scale = Mathf.Min(scaleX, scaleY);

        Vector2 edgePosition = center + fromCenter * scale;

        marker.position = edgePosition;
        marker.rotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg);
    }

    RectTransform RentMarker(int index)
    {
        while (_markerPool.Count <= index)
        {
            RectTransform created = Instantiate(markerPrefab, markerRoot);
            created.gameObject.SetActive(false);
            _markerPool.Add(created);
        }

        RectTransform marker = _markerPool[index];
        if (!marker.gameObject.activeSelf)
            marker.gameObject.SetActive(true);

        return marker;
    }

    void HideAllMarkers()
    {
        for (int i = 0; i < _markerPool.Count; i++)
        {
            RectTransform marker = _markerPool[i];
            if (marker != null && marker.gameObject.activeSelf)
                marker.gameObject.SetActive(false);
        }
    }
}
