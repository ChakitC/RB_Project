using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcPresentationTarget : MonoBehaviour
{
    [Header("Framing")]
    [SerializeField] private Transform framingRoot;
    [SerializeField] private Vector3 cameraLocalOffset = new(0f, 1.55f, 5.8f);
    [SerializeField] private Vector3 lookLocalOffset = new(-1.55f, 1.35f, 0f);
    [SerializeField, Range(15f, 70f)] private float fieldOfView = 34f;

    [Header("UI Layout")]
    [SerializeField, Range(0.25f, 0.8f)] private float uiWidthRatio = 0.72f;
    [SerializeField, Min(0f)] private float uiMargin = 24f;

    public float FieldOfView => fieldOfView;
    public float UiWidthRatio => uiWidthRatio;
    public float UiMargin => uiMargin;

    public void GetCameraPose(out Vector3 position, out Quaternion rotation)
    {
        Transform root = framingRoot != null ? framingRoot : transform;
        position = root.TransformPoint(cameraLocalOffset);
        Vector3 lookPosition = root.TransformPoint(lookLocalOffset);
        Vector3 lookDirection = lookPosition - position;
        if (lookDirection.sqrMagnitude < 0.0001f)
            lookDirection = -root.forward;

        rotation = Quaternion.LookRotation(lookDirection.normalized, root.up);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Transform root = framingRoot != null ? framingRoot : transform;
        Vector3 cameraPosition = root.TransformPoint(cameraLocalOffset);
        Vector3 lookPosition = root.TransformPoint(lookLocalOffset);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(cameraPosition, 0.08f);
        Gizmos.DrawLine(cameraPosition, lookPosition);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(lookPosition, 0.06f);
    }
#endif
}
