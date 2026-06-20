using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldTimeScaledVfx : MonoBehaviour
{
    ParticleSystem[] _particleSystems;
    float[] _particleSimulationSpeeds;
    TrailRenderer[] _trailRenderers;
    float[] _trailTimes;
    Animator[] _animators;
    float[] _animatorSpeeds;
    float _destroyAfterWorldSeconds;
    float _age;
    bool _destroyAfterLifetime;
    PooledVfxHandle _handle;

    void Awake()
    {
        CacheComponents();
    }

    void OnEnable()
    {
        _age = 0f;
        _destroyAfterLifetime = false;
        _handle = GetComponent<PooledVfxHandle>();
    }

    void Update()
    {
        ApplyWorldScale();

        if (!_destroyAfterLifetime)
            return;

        _age += TimeSlowManager.Instance.WorldDeltaTime;
        if (_age >= _destroyAfterWorldSeconds)
        {
            if (_handle != null)
                _handle.ReturnToPool();
            else
                Destroy(gameObject);
        }
    }

    public void DestroyAfterWorldSeconds(float seconds)
    {
        _destroyAfterWorldSeconds = Mathf.Max(0f, seconds);
        _age = 0f;
        _destroyAfterLifetime = _destroyAfterWorldSeconds > 0f;
    }

    public void RestoreBaseSpeeds()
    {
        CacheComponents();
        ApplyScale(1f);
    }

    void CacheComponents()
    {
        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        _particleSimulationSpeeds = new float[_particleSystems.Length];
        for (int i = 0; i < _particleSystems.Length; i++)
        {
            var ps = _particleSystems[i];
            if (ps == null)
                continue;

            _particleSimulationSpeeds[i] = ps.main.simulationSpeed;
        }

        _trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
        _trailTimes = new float[_trailRenderers.Length];
        for (int i = 0; i < _trailRenderers.Length; i++)
        {
            TrailRenderer trail = _trailRenderers[i];
            _trailTimes[i] = trail != null ? trail.time : 0f;
        }

        _animators = GetComponentsInChildren<Animator>(true);
        _animatorSpeeds = new float[_animators.Length];
        for (int i = 0; i < _animators.Length; i++)
        {
            Animator animator = _animators[i];
            _animatorSpeeds[i] = animator != null ? animator.speed : 1f;
        }
    }

    void ApplyWorldScale()
    {
        ApplyScale(TimeSlowManager.Instance.WorldTimeScale);
    }

    void ApplyScale(float scale)
    {
        scale = Mathf.Max(0.05f, scale);

        for (int i = 0; i < _particleSystems.Length; i++)
        {
            var ps = _particleSystems[i];
            if (ps == null)
                continue;

            var main = ps.main;
            main.simulationSpeed = _particleSimulationSpeeds[i] * scale;
        }

        for (int i = 0; i < _trailRenderers.Length; i++)
        {
            TrailRenderer trail = _trailRenderers[i];
            if (trail == null)
                continue;

            trail.time = _trailTimes[i] / scale;
        }

        for (int i = 0; i < _animators.Length; i++)
        {
            Animator animator = _animators[i];
            if (animator == null)
                continue;

            animator.speed = _animatorSpeeds[i] * scale;
        }
    }

    void OnDisable()
    {
        if (_particleSystems == null)
            return;

        ApplyScale(1f);
    }
}
