using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform mainCameraTransform;

    void Start()
    {
        
        if (UnityEngine.Camera.main != null)
        {
            mainCameraTransform = UnityEngine.Camera.main.transform;
        }
        else
        {
            Debug.LogError("Main Camera not found! Cannot billboard.");
            enabled = false; 
        }
    }

    void LateUpdate()
    {
        if (mainCameraTransform == null) return;
        
        
        transform.LookAt(transform.position + mainCameraTransform.forward);
    }
}