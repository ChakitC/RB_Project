using UnityEngine;

[DisallowMultipleComponent]
public class PickupVisualMotion : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField, Min(0f)] private float bobAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float bobFrequency = 0.9f;
    [SerializeField] private float rotationSpeed = 35f;
    [SerializeField, Min(0f)] private float wobbleAngle = 5f;
    [SerializeField, Min(0f)] private float wobbleFrequency = 0.65f;
    [SerializeField] private bool randomizePhase = true;

    Transform _target;
    Vector3 _baseLocalPosition;
    Quaternion _baseLocalRotation;
    float _phaseOffset;

    void Reset()
    {
        visualRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
    }

    void Awake()
    {
        ResolveTarget();
        CachePose();
        ReseedPhase();
    }

    void OnEnable()
    {
        ResolveTarget();
        CachePose();
    }

    void OnValidate()
    {
        bobAmplitude = Mathf.Max(0f, bobAmplitude);
        bobFrequency = Mathf.Max(0f, bobFrequency);
        wobbleAngle = Mathf.Max(0f, wobbleAngle);
        wobbleFrequency = Mathf.Max(0f, wobbleFrequency);

        if (visualRoot == null && transform.childCount > 0)
            visualRoot = transform.GetChild(0);
    }

    void Update()
    {
        if (!Application.isPlaying)
            return;

        ResolveTarget();
        if (_target == null)
            return;

        float time = Time.time + _phaseOffset;
        float bobWave = Mathf.Sin(time * bobFrequency * Mathf.PI * 2f);
        float wobbleWave = Mathf.Sin(time * wobbleFrequency * Mathf.PI * 2f);
        float wobbleWaveOffset = Mathf.Cos((time + 0.35f) * wobbleFrequency * Mathf.PI * 2f);

        Vector3 animatedOffset = Vector3.up * (bobWave * bobAmplitude);
        Quaternion animatedRotation = Quaternion.Euler(
            wobbleWave * wobbleAngle,
            time * rotationSpeed,
            wobbleWaveOffset * wobbleAngle);

        _target.localPosition = _baseLocalPosition + animatedOffset;
        _target.localRotation = _baseLocalRotation * animatedRotation;
    }

    void ResolveTarget()
    {
        if (visualRoot == null && transform.childCount > 0)
            visualRoot = transform.GetChild(0);

        _target = visualRoot != null ? visualRoot : transform;
    }

    void CachePose()
    {
        if (_target == null)
            return;

        _baseLocalPosition = _target.localPosition;
        _baseLocalRotation = _target.localRotation;
    }

    void ReseedPhase()
    {
        if (!randomizePhase)
        {
            _phaseOffset = 0f;
            return;
        }

        _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }
}
