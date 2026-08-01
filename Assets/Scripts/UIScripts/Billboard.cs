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

        transform.LookAt(transform.position + mainCameraTransform.forward);
    }

    void ResolveMainCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCameraTransform != mainCamera.transform)
            mainCameraTransform = mainCamera.transform;
    }
}
