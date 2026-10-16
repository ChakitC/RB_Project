using UnityEngine;

/// <summary>Camera-plane spring motion, independent of the preview's animation and placement.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class BasementDragSway : MonoBehaviour
{
    [SerializeField] private Transform swayRoot;
    [SerializeField, Min(0f)] private float sensitivity = 15f;
    [SerializeField, Range(0f, 45f)] private float maxAngle = 20f;
    [SerializeField, Min(0.001f)] private float referenceHeight = 1f;
    [SerializeField, Min(0.01f)] private float frequency = 2f;
    [SerializeField, Min(0f)] private float dampingRatio = 0.6f;

    BasementDragRig rig;
    Camera dragCamera;
    Vector3 restPosition;
    Quaternion restRotation;
    Vector2 pointer;
    Vector2 previousPointer;
    float angle;
    float angularVelocity;
    float dragFrequency;
    bool bound;
    bool dragging;
    bool resetPointer;
    bool focused = true;

    public void Bind(GameObject model)
    {
        EndDrag();
        bound = false;
        rig = model != null ? model.GetComponent<BasementDragRig>() : null;
        if (!swayRoot || !model || !model.transform.IsChildOf(swayRoot) || !rig || !rig.CaptureSize())
        {
            if (model != null)
                Debug.LogWarning("[Basement Drag] Bind SwayRoot and the model's head/feet rig. Dragging remains available without sway.", this);
            return;
        }
        restPosition = swayRoot.localPosition;
        restRotation = swayRoot.localRotation;
        bound = true;
    }

    public void BeginDrag(Camera camera, Vector2 screenPosition)
    {
        EndDrag();
        if (!bound || !rig || !rig.IsValid || camera == null)
            return;
        dragCamera = camera;
        pointer = previousPointer = screenPosition;
        dragFrequency = Mathf.Clamp(frequency * Mathf.Sqrt(referenceHeight / Mathf.Max(0.0001f, rig.Height)), 1.4f, 2.8f);
        dragging = true;
    }

    public void UpdateDrag(Vector2 screenPosition) => pointer = screenPosition;

    public void EndDrag()
    {
        dragging = false;
        angle = angularVelocity = 0f;
        dragCamera = null;
        resetPointer = false;
        RestorePose();
    }

    public void Unbind()
    {
        EndDrag();
        bound = false;
        rig = null;
    }

    void OnDisable() => EndDrag();
    void OnApplicationFocus(bool value)
    {
        focused = value;
        resetPointer = true;
    }

    void LateUpdate() => Advance(Time.unscaledDeltaTime);

    // Kept deterministic so the same pointer trace can be verified at different frame rates.
    void Advance(float deltaTime)
    {
        if (!dragging)
            return;
        if (!rig || !rig.IsValid || !swayRoot || !dragCamera)
        {
            EndDrag();
            return;
        }
        if (!focused || resetPointer || deltaTime <= 0f || deltaTime > 0.1f)
        {
            previousPointer = pointer;
            resetPointer = false;
            return;
        }

        float speed = (pointer.x - previousPointer.x) / Mathf.Max(1f, dragCamera.pixelHeight) / deltaTime;
        previousPointer = pointer;
        // Positive camera-forward rotation moves the feet right, so use the opposite sign.
        float target = Mathf.Clamp(-speed * sensitivity, -maxAngle, maxAngle);
        float omega = 2f * Mathf.PI * dragFrequency;
        int steps = Mathf.Max(1, Mathf.CeilToInt(deltaTime * 120f));
        float step = deltaTime / steps;
        for (int i = 0; i < steps; i++)
        {
            angularVelocity += (omega * omega * (target - angle) - 2f * dampingRatio * omega * angularVelocity) * step;
            angle += angularVelocity * step;
            if (Mathf.Abs(angle) > maxAngle)
            {
                angle = Mathf.Clamp(angle, -maxAngle, maxAngle);
                if (angle * angularVelocity > 0f) angularVelocity = 0f;
            }
        }

        // Remove last frame's effect before reading the animated anchor. Never accumulate offsets.
        RestorePose();
        Vector3 anchor = rig.GripPosition;
        swayRoot.rotation = Quaternion.AngleAxis(angle, dragCamera.transform.forward) * swayRoot.rotation;
        swayRoot.position += anchor - rig.GripPosition;
    }

    void RestorePose()
    {
        if (!bound || !swayRoot) return;
        swayRoot.localPosition = restPosition;
        swayRoot.localRotation = restRotation;
    }
}
