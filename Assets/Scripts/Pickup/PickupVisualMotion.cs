using System;
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
    [SerializeField, Min(0.01f)] private float collectDuration = 0.35f;
    [SerializeField] private Vector3 collectTargetOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField, Min(0f)] private float collectArcHeight = 0.6f;
    [SerializeField, Range(0f, 1f)] private float collectEndScale = 0.15f;

    Transform _target;
    Vector3 _baseLocalPosition;
    Quaternion _baseLocalRotation;
    float _phaseOffset;
    bool _collecting;
    Transform _collectTarget;
    Vector3 _collectStartPosition;
    Vector3 _collectStartScale;
    float _collectElapsed;
    Action _collectComplete;

    public bool IsCollecting => _collecting;

    public void PlayCollectTo(Transform target, Action onComplete)
    {
        if (_collecting)
            return;

        if (target == null || collectDuration <= 0f)
        {
            onComplete?.Invoke();
            return;
        }

        ResolveTarget();
        if (_target != null && _target != transform)
        {
            _target.localPosition = _baseLocalPosition;
            _target.localRotation = _baseLocalRotation;
        }

        _collecting = true;
        _collectTarget = target;
        _collectStartPosition = transform.position;
        _collectStartScale = transform.localScale;
        _collectElapsed = 0f;
        _collectComplete = onComplete;
    }

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
        collectDuration = Mathf.Max(0.01f, collectDuration);
        collectArcHeight = Mathf.Max(0f, collectArcHeight);
        collectEndScale = Mathf.Clamp01(collectEndScale);

        if (visualRoot == null && transform.childCount > 0)
            visualRoot = transform.GetChild(0);
    }

    void Update()
    {
        if (!Application.isPlaying)
            return;

        if (_collecting)
        {
            UpdateCollectMotion();
            return;
        }

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

    void UpdateCollectMotion()
    {
        _collectElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_collectElapsed / collectDuration);
        float easedProgress = progress * progress * (3f - 2f * progress);

        Vector3 targetPosition = _collectTarget != null
            ? _collectTarget.position + collectTargetOffset
            : transform.position;

        Vector3 position = Vector3.Lerp(_collectStartPosition, targetPosition, easedProgress);
        position.y += Mathf.Sin(progress * Mathf.PI) * collectArcHeight;

        transform.position = position;
        transform.localScale = Vector3.Lerp(_collectStartScale, _collectStartScale * collectEndScale, easedProgress);

        if (progress < 1f)
            return;

        _collecting = false;
        _collectTarget = null;

        var onComplete = _collectComplete;
        _collectComplete = null;
        onComplete?.Invoke();
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

        _phaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
    }
}
