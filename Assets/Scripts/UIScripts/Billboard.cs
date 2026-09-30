using UnityEngine;

[DefaultExecutionOrder(10200)]
public class Billboard : MonoBehaviour
{
    private Transform mainCameraTransform;

    void Start()
    {
        ResolveMainCamera();
    }

    void LateUpdate()
    {
        ResolveMainCamera();
        if (mainCameraTransform == null) return;

        transform.rotation = mainCameraTransform.rotation;
    }

    void ResolveMainCamera()
    {
        // Bootstrap scenes can keep another MainCamera alive alongside the gameplay rig.
        Camera mainCamera = GameplayCameraController.Instance != null
            ? GameplayCameraController.Instance.GameplayCamera
            : null;
        if (mainCamera == null || !mainCamera.isActiveAndEnabled)
            mainCamera = Camera.main;

        mainCameraTransform = mainCamera != null ? mainCamera.transform : null;
    }
}
