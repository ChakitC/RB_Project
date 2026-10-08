using UnityEngine;

[DisallowMultipleComponent]
public sealed class AnimationVfxFollowAnchor : MonoBehaviour
{
    Transform anchor;
    Vector3 localPosition;
    Quaternion localRotation = Quaternion.identity;
    bool inheritScale;
    Vector3 anchorScale;

    public void Configure(Transform target, Vector3 position, Quaternion rotation, bool followScale = false)
    {
        anchor = target;
        localPosition = position;
        localRotation = rotation;
        inheritScale = followScale;
        anchorScale = target != null ? target.lossyScale : Vector3.one;
        enabled = anchor != null;
        ApplyNow();
    }

    public void ClearAnchor()
    {
        anchor = null;
        localPosition = Vector3.zero;
        localRotation = Quaternion.identity;
        inheritScale = false;
        enabled = false;
    }

    public void ApplyNow()
    {
        if (anchor == null)
        {
            enabled = false;
            return;
        }

        transform.SetPositionAndRotation(
            inheritScale ? anchor.TransformPoint(localPosition) : anchor.position + anchor.rotation * localPosition,
            anchor.rotation * localRotation);
        if (inheritScale)
        {
            Vector3 current = anchor.lossyScale;
            transform.localScale = Vector3.Scale(transform.localScale, new Vector3(
                ScaleRatio(current.x, anchorScale.x), ScaleRatio(current.y, anchorScale.y),
                ScaleRatio(current.z, anchorScale.z)));
            anchorScale = current;
        }
    }

    static float ScaleRatio(float current, float initial) => Mathf.Abs(initial) > .00001f ? current / initial : 1f;

    void LateUpdate()
    {
        ApplyNow();
    }
}
