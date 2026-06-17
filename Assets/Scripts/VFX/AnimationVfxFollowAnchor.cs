using UnityEngine;

[DisallowMultipleComponent]
public sealed class AnimationVfxFollowAnchor : MonoBehaviour
{
    Transform anchor;
    Vector3 localPosition;
    Quaternion localRotation = Quaternion.identity;

    public void Configure(Transform target, Vector3 position, Quaternion rotation)
    {
        anchor = target;
        localPosition = position;
        localRotation = rotation;
        ApplyNow();
    }

    public void ApplyNow()
    {
        if (anchor == null)
            return;

        transform.SetPositionAndRotation(
            anchor.position + anchor.rotation * localPosition,
            anchor.rotation * localRotation);
    }

    void LateUpdate()
    {
        ApplyNow();
    }
}
