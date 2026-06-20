using Animancer;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PooledVfxHandle : MonoBehaviour
{
    public GameObject SourcePrefab;
    public VfxPool Pool;

    bool _isPooled;
    bool _stateCaptured;

    ParticleSystem[] _particleSystems;
    bool[] _initialLoopFlags;
    TrailRenderer[] _trailRenderers;
    Vector3 _originalLocalScale;

    public ParticleSystem[] ParticleSystems => _particleSystems;
    public TrailRenderer[] TrailRenderers => _trailRenderers;

    public void CaptureState()
    {
        if (_stateCaptured) return;
        _stateCaptured = true;

        _originalLocalScale = transform.localScale;

        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        _initialLoopFlags = new bool[_particleSystems.Length];
        for (int i = 0; i < _particleSystems.Length; i++)
        {
            var ps = _particleSystems[i];
            if (ps != null)
                _initialLoopFlags[i] = ps.main.loop;
        }

        _trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
    }

    public void PrepareForReuse(Vector3 pos, Quaternion rot, Transform parent, float scale)
    {
        _isPooled = false;
        ClearFollowAnchors();
        StopRuntimeAnimancers();

        for (int i = 0; i < _particleSystems.Length; i++)
        {
            var ps = _particleSystems[i];
            if (ps == null) continue;
            var main = ps.main;
            main.loop = _initialLoopFlags[i];
        }

        transform.SetParent(parent, false);
        transform.SetPositionAndRotation(pos, rot);
        transform.localScale = _originalLocalScale * scale;

        for (int i = 0; i < _particleSystems.Length; i++)
        {
            var ps = _particleSystems[i];
            if (ps == null) continue;
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.randomSeed = (uint)Random.Range(1, int.MaxValue);
        }

        gameObject.SetActive(true);

        for (int i = 0; i < _particleSystems.Length; i++)
        {
            var ps = _particleSystems[i];
            if (ps == null || ps.isPlaying) continue;
            ps.Play(false);
        }

        for (int i = 0; i < _trailRenderers.Length; i++)
        {
            var trail = _trailRenderers[i];
            if (trail == null) continue;
            trail.Clear();
        }
    }

    public void ReturnToPool()
    {
        if (_isPooled) return;
        _isPooled = true;
        ClearFollowAnchors();
        StopRuntimeAnimancers();

        for (int i = 0; i < _particleSystems.Length; i++)
        {
            var ps = _particleSystems[i];
            if (ps == null) continue;
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        for (int i = 0; i < _trailRenderers.Length; i++)
        {
            var trail = _trailRenderers[i];
            if (trail == null) continue;
            trail.Clear();
        }

        transform.SetParent(Pool.GetPoolRoot(), false);
        gameObject.SetActive(false);

        Pool.PushToStack(SourcePrefab, gameObject);
    }

    void ClearFollowAnchors()
    {
        var followers = GetComponentsInChildren<AnimationVfxFollowAnchor>(true);
        for (int i = 0; i < followers.Length; i++)
        {
            var follower = followers[i];
            if (follower == null)
                continue;

            follower.ClearAnchor();
        }
    }

    void StopRuntimeAnimancers()
    {
        var animancers = GetComponentsInChildren<AnimancerComponent>(true);
        for (int i = 0; i < animancers.Length; i++)
        {
            var animancer = animancers[i];
            if (animancer == null)
                continue;

            animancer.Stop();
        }
    }
}
