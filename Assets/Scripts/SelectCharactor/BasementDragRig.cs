using UnityEngine;

/// <summary>Explicit preview-only bindings; accessories never determine body size.</summary>
[DisallowMultipleComponent]
public sealed class BasementDragRig : MonoBehaviour
{
    [SerializeField] private Transform head;
    [SerializeField] private Transform leftFoot;
    [SerializeField] private Transform rightFoot;
    [Tooltip("Offset in model-local axes, as a fraction of the initial head-to-feet height.")]
    [SerializeField] private Vector3 gripOffset;

    Vector3 initialBodyVector;

    public bool CaptureSize()
    {
        if (!Owns(head) || !Owns(leftFoot) || !Owns(rightFoot))
            return false;
        initialBodyVector = transform.InverseTransformVector(
            head.position - (leftFoot.position + rightFoot.position) * 0.5f);
        return Height > 0.0001f;
    }

    public float Height => transform.TransformVector(initialBodyVector).magnitude;
    public Vector3 GripPosition => head.position + transform.TransformVector(gripOffset * initialBodyVector.magnitude);
    public bool IsValid => Owns(head) && Owns(leftFoot) && Owns(rightFoot);

    bool Owns(Transform bone) => bone != null && bone.IsChildOf(transform);
}
