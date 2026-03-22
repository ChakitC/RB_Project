using UnityEngine;

public class CameraF : MonoBehaviour
{
    public Transform taget;
    public float smooth;
    public Vector3 offset;
    private Vector3 velocity = Vector3.zero;
    void Start()
    {
        
    }

   
    void Update()
    {
        if (taget != null)
        {
            Vector3 targetPositon = taget.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPositon, ref velocity, smooth);
        }
            
    }
}
