using Animancer;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PooledVfxHandle : MonoBehaviour
{
    public GameObject SourcePrefab;
    public VfxPool Pool;

    bool _isPooled;
    bool _stateCaptured;
    bool stopping;
    IPooledVfxLifecycle[] lifecycle;
    Vector3[] childPositions;
    Quaternion[] childRotations;
    Vector3[] childScales;
    bool[] childActive;
    public ulong Generation { get; private set; }
    public int OwnerSceneHandle { get; private set; }
    public bool IsCurrent(ulong generation) => !_isPooled && !stopping && Generation == generation;
    internal void SetOwnerScene(int sceneHandle) => OwnerSceneHandle = sceneHandle;

    internal bool BeginStop()
    {
        if (_isPooled || stopping) return false;
        stopping = true;
        return true;
    }

    ParticleSystem[] _particleSystems;
    bool[] _initialLoopFlags;
    TrailRenderer[] _trailRenderers;
    Vector3 _originalLocalScale;
    Transform[] _childTransforms;
    int[] _initialLayers;

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

        _childTransforms = GetComponentsInChildren<Transform>(true);
        childPositions = new Vector3[_childTransforms.Length];
        childRotations = new Quaternion[_childTransforms.Length];
        childScales = new Vector3[_childTransforms.Length];
        childActive = new bool[_childTransforms.Length];
        for (int i = 0; i < _childTransforms.Length; i++)
        {
            var child = _childTransforms[i];
            childPositions[i] = child.localPosition;
            childRotations[i] = child.localRotation;
            childScales[i] = child.localScale;
            childActive[i] = child.gameObject.activeSelf;
        }
        var callbacks = new System.Collections.Generic.List<IPooledVfxLifecycle>();
        foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour is IPooledVfxLifecycle callback) callbacks.Add(callback);
        lifecycle = callbacks.ToArray();
        _initialLayers = new int[_childTransforms.Length];
        for (int i = 0; i < _childTransforms.Length; i++)
            if (_childTransforms[i] != null)
                _initialLayers[i] = _childTransforms[i].gameObject.layer;
    }

    public void PrepareForReuse(Vector3 pos, Quaternion rot, Transform parent, float scale)
    {
        CaptureState();
        gameObject.SetActive(false);
        _isPooled = false;
        stopping = false;
        Generation++;
        OwnerSceneHandle = parent != null ? parent.gameObject.scene.handle
            : UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        var scaler = GetComponent<WorldTimeScaledVfx>();
        if (scaler == null) scaler = gameObject.AddComponent<WorldTimeScaledVfx>();
        scaler.ResetForReuse();
        ClearFollowAnchors();
        StopRuntimeAnimancers();

        for (int i = 0; i < _particleSystems.Length; i++)
        {
            var ps = _particleSystems[i];
            if (ps == null) continue;
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.None;
            main.loop = _initialLoopFlags[i];
        }

        for (int i = 0; i < _childTransforms.Length; i++)
            if (_childTransforms[i] != null)
            {
                var child = _childTransforms[i];
                child.gameObject.layer = _initialLayers[i];
                if (child != transform)
                {
                    child.localPosition = childPositions[i];
                    child.localRotation = childRotations[i];
                    child.localScale = childScales[i];
                    child.gameObject.SetActive(childActive[i]);
                }
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

        foreach (var callback in lifecycle) callback.OnVfxRent();
    }

    internal void Activate()
    {
        gameObject.SetActive(true);

        for (int i = 0; i < _particleSystems.Length; i++)
        {
            var ps = _particleSystems[i];
            if (ps == null || !ps.gameObject.activeInHierarchy || ps.isPlaying) continue;
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
        gameObject.SetActive(false);
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

        foreach (var callback in lifecycle)
        {
            try { callback.OnVfxReturn(); }
            catch (System.Exception exception) { Debug.LogException(exception, this); }
        }
        if (Pool == null) { Destroy(gameObject); return; }
        Pool.Recycle(this);
    }

    void OnDestroy() { if (Pool != null) Pool.Forget(this); }

    internal void DetachForTail()
    {
        ClearFollowAnchors();
        transform.SetParent(Pool != null ? Pool.GetPoolRoot() : null, true);
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
