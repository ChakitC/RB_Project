using UnityEngine;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class WorldUICameraSync : MonoBehaviour
{
    [SerializeField] private Camera sourceCamera;

    Camera worldUICamera;

    void Awake()
    {
        worldUICamera = GetComponent<Camera>();
        SyncCamera();
    }

    void LateUpdate()
    {
        SyncCamera();
    }

    void SyncCamera()
    {
        if (sourceCamera == null || worldUICamera == null || sourceCamera == worldUICamera)
            return;

        transform.SetPositionAndRotation(
            sourceCamera.transform.position,
            sourceCamera.transform.rotation);

        worldUICamera.nearClipPlane = sourceCamera.nearClipPlane;
        worldUICamera.farClipPlane = sourceCamera.farClipPlane;
        worldUICamera.orthographic = sourceCamera.orthographic;
        worldUICamera.projectionMatrix = sourceCamera.projectionMatrix;
    }
}
