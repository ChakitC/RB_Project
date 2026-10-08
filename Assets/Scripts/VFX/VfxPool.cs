using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class VfxPool : MonoBehaviour
{
    [SerializeField] int maxPerPool = 20;
    readonly Dictionary<GameObject, Stack<GameObject>> _pools = new();
    readonly HashSet<PooledVfxHandle> active = new();
    readonly List<PooledVfxHandle> pendingReturns = new();
    Transform _poolRoot;
    Transform stagingRoot;

    void OnEnable() => SceneManager.sceneUnloaded += OnSceneUnloaded;
    void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        foreach (var handle in new List<PooledVfxHandle>(active))
            if (handle != null) handle.ReturnToPool();
    }

    void OnSceneUnloaded(Scene scene)
    {
        foreach (var handle in new List<PooledVfxHandle>(active))
            if (handle == null) active.Remove(handle);
            else if (handle.OwnerSceneHandle == scene.handle) handle.ReturnToPool();
    }

    internal void Recycle(PooledVfxHandle handle)
    {
        active.Remove(handle);
        // Unity forbids hierarchy mutations inside a parent's disable callback.
        if (!gameObject.activeInHierarchy ||
            (handle.transform.parent != null && !handle.transform.parent.gameObject.activeInHierarchy))
        {
            pendingReturns.Add(handle);
            return;
        }
        handle.transform.SetParent(GetPoolRoot(), false);
        PushToStack(handle.SourcePrefab, handle.gameObject);
    }

    void LateUpdate()
    {
        foreach (var handle in pendingReturns)
        {
            if (handle == null) continue;
            handle.transform.SetParent(GetPoolRoot(), false);
            PushToStack(handle.SourcePrefab, handle.gameObject);
        }
        pendingReturns.Clear();
    }

    void OnDestroy()
    {
        foreach (var handle in pendingReturns)
        {
            if (handle == null || handle.transform.IsChildOf(transform)) continue;
            if (Application.isPlaying) Destroy(handle.gameObject);
            else DestroyImmediate(handle.gameObject);
        }
        pendingReturns.Clear();
    }

    public GameObject Get(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent, float scale,
        Action<GameObject> configure = null)
    {
        GameObject instance = null;
        if (_pools.TryGetValue(prefab, out var stack))
            while (stack.Count > 0 && instance == null) instance = stack.Pop();
        if (instance == null) instance = CreateNew(prefab);
        var handle = instance.GetComponent<PooledVfxHandle>();
        try
        {
            handle.PrepareForReuse(pos, rot, parent, scale);
            active.Add(handle);
            configure?.Invoke(instance);
            handle.Activate();
            return instance;
        }
        catch
        {
            handle.ReturnToPool();
            throw;
        }
    }

    public void PushToStack(GameObject prefab, GameObject instance)
    {
        active.Remove(instance.GetComponent<PooledVfxHandle>());
        if (!_pools.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<GameObject>();
            _pools[prefab] = stack;
        }
        if (stack.Count >= maxPerPool)
        {
            if (Application.isPlaying) Destroy(instance);
            else DestroyImmediate(instance);
            return;
        }
        stack.Push(instance);
    }

    internal void Forget(PooledVfxHandle handle) => active.Remove(handle);

    public Transform GetPoolRoot()
    {
        if (_poolRoot != null) return _poolRoot;
        var existing = transform.Find("VfxPoolRoot");
        if (existing != null) { _poolRoot = existing; return _poolRoot; }
        var go = new GameObject("VfxPoolRoot");
        go.transform.SetParent(transform, false);
        _poolRoot = go.transform;
        return _poolRoot;
    }

    GameObject CreateNew(GameObject prefab)
    {
        if (stagingRoot == null)
        {
            var staging = new GameObject("VfxInactiveStaging");
            staging.SetActive(false);
            staging.transform.SetParent(transform, false);
            stagingRoot = staging.transform;
        }
        // No OnEnable/playOnAwake before lifetime, placement and reset are configured.
        GameObject instance = Instantiate(prefab, stagingRoot);
        instance.SetActive(false);
        if (instance.GetComponentInChildren<CartoonFX.CFXR_Effect>(true) != null &&
            instance.GetComponent<PooledCartoonFxLifecycle>() == null)
            instance.AddComponent<PooledCartoonFxLifecycle>();
        var handle = instance.GetComponent<PooledVfxHandle>();
        if (handle == null) handle = instance.AddComponent<PooledVfxHandle>();
        handle.SourcePrefab = prefab;
        handle.Pool = this;
        handle.CaptureState();
        return instance;
    }
}
