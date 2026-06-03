using UnityEngine;

[DisallowMultipleComponent]
public class PickupDropArcMotion : MonoBehaviour
{
    Collider[] _colliders;
    bool[] _colliderEnabledStates;
    Rigidbody[] _rigidbodies;
    bool[] _rigidbodyKinematicStates;
    bool[] _rigidbodyGravityStates;
    Vector3 _startPosition;
    Vector3 _endPosition;
    float _duration;
    float _arcHeight;
    float _delay;
    float _elapsed;
    bool _playing;
    bool _collidersSuppressed;

    public bool IsPlaying => _playing;

    public void Play(Vector3 startPosition, Vector3 endPosition, float duration, float arcHeight, float delay)
    {
        if (_playing)
        {
            RestoreRigidbodies();
            RestoreColliders();
        }

        _startPosition = startPosition;
        _endPosition = endPosition;
        _duration = Mathf.Max(0.01f, duration);
        _arcHeight = Mathf.Max(0f, arcHeight);
        _delay = Mathf.Max(0f, delay);
        _elapsed = -_delay;
        _playing = true;
        transform.position = _startPosition;

        SuppressColliders();
        SuppressRigidbodies();
        enabled = true;
    }

    void Update()
    {
        if (!_playing)
            return;

        _elapsed += Time.deltaTime;
        if (_elapsed < 0f)
            return;

        float progress = Mathf.Clamp01(_elapsed / _duration);
        float easedProgress = progress * progress * (3f - 2f * progress);

        Vector3 position = Vector3.Lerp(_startPosition, _endPosition, easedProgress);
        position.y += Mathf.Sin(progress * Mathf.PI) * _arcHeight;
        transform.position = position;

        if (progress >= 1f)
            Complete();
    }

    void OnDisable()
    {
        if (!_playing)
            return;

        _playing = false;
        RestoreRigidbodies();
        RestoreColliders();
    }

    void SuppressColliders()
    {
        _colliders = GetComponentsInChildren<Collider>(true);
        _colliderEnabledStates = new bool[_colliders.Length];

        for (int i = 0; i < _colliders.Length; i++)
        {
            Collider pickupCollider = _colliders[i];
            if (pickupCollider == null)
                continue;

            _colliderEnabledStates[i] = pickupCollider.enabled;
            pickupCollider.enabled = false;
        }

        _collidersSuppressed = true;
    }

    void SuppressRigidbodies()
    {
        _rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        _rigidbodyKinematicStates = new bool[_rigidbodies.Length];
        _rigidbodyGravityStates = new bool[_rigidbodies.Length];

        for (int i = 0; i < _rigidbodies.Length; i++)
        {
            Rigidbody body = _rigidbodies[i];
            if (body == null)
                continue;

            _rigidbodyKinematicStates[i] = body.isKinematic;
            _rigidbodyGravityStates[i] = body.useGravity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    void RestoreColliders()
    {
        if (!_collidersSuppressed || _colliders == null || _colliderEnabledStates == null)
            return;

        int count = Mathf.Min(_colliders.Length, _colliderEnabledStates.Length);
        for (int i = 0; i < count; i++)
        {
            Collider pickupCollider = _colliders[i];
            if (pickupCollider != null)
                pickupCollider.enabled = _colliderEnabledStates[i];
        }

        _collidersSuppressed = false;
    }

    void RestoreRigidbodies()
    {
        if (_rigidbodies == null || _rigidbodyKinematicStates == null || _rigidbodyGravityStates == null)
            return;

        int count = Mathf.Min(_rigidbodies.Length, _rigidbodyKinematicStates.Length, _rigidbodyGravityStates.Length);
        for (int i = 0; i < count; i++)
        {
            Rigidbody body = _rigidbodies[i];
            if (body == null)
                continue;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = _rigidbodyGravityStates[i];
            body.isKinematic = _rigidbodyKinematicStates[i];
        }
    }

    void Complete()
    {
        _playing = false;
        transform.position = _endPosition;
        RestoreRigidbodies();
        RestoreColliders();
    }
}
