using System.Collections.Generic;
using UnityEngine;

public sealed class ProjectilePool : MonoBehaviour
{
    const int DefaultMaxPerPool = 128;

    static ProjectilePool _instance;
    static bool _isQuitting;

    public static ProjectilePool Instance
    {
        get
        {
            if (_instance != null) return _instance;
            if (_isQuitting) return null;
            _instance = FindAnyObjectByType<ProjectilePool>();
            if (_instance != null) return _instance;
            var go = new GameObject(nameof(ProjectilePool));
            _instance = go.AddComponent<ProjectilePool>();
            return _instance;
        }
    }

    [Tooltip("Maximum number of idle projectiles kept per prefab; excess instances are destroyed")]
    [SerializeField] int maxPerPool = DefaultMaxPerPool;

    readonly Dictionary<Projectile, Stack<Projectile>> _pools = new();

    // Not [SerializeField] — resolved lazily via GetPoolRoot() so domain reloads and
    // early Instance accesses (before Awake) both find the existing child rather than
    // parenting idle projectiles to scene root where a scene change would destroy them.
    Transform _poolRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        _instance = null;
        _isQuitting = false;
        Application.quitting -= HandleApplicationQuitting;
        Application.quitting += HandleApplicationQuitting;
    }

    static void HandleApplicationQuitting() => _isQuitting = true;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    Transform GetPoolRoot()
    {
        if (_poolRoot != null) return _poolRoot;
        var existing = transform.Find("ProjectilePoolRoot");
        if (existing != null) { _poolRoot = existing; return _poolRoot; }
        var go = new GameObject("ProjectilePoolRoot");
        go.transform.SetParent(transform, false);
        _poolRoot = go.transform;
        return _poolRoot;
    }

    void OnApplicationQuit() => _isQuitting = true;

    void OnDestroy() { if (_instance == this) _instance = null; }

    public Projectile Get(Projectile prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return null;

        Projectile instance = null;
        if (_pools.TryGetValue(prefab, out var stack))
        {
            while (stack.Count > 0)
            {
                instance = stack.Pop();
                if (instance != null) break;
                instance = null;
            }
        }

        if (instance == null) instance = Instantiate(prefab);
        instance.PrepareForSpawn(this, prefab, pos, rot);
        return instance;
    }

    public void Return(Projectile instance)
    {
        if (instance == null) return;
        var prefab = instance.SourcePrefab;
        if (prefab == null) { Destroy(instance.gameObject); return; }

        if (!_pools.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<Projectile>();
            _pools[prefab] = stack;
        }

        if (stack.Count >= maxPerPool) { Destroy(instance.gameObject); return; }

        instance.transform.SetParent(GetPoolRoot(), false);
        instance.gameObject.SetActive(false);
        stack.Push(instance);
    }

    public void Prewarm(Projectile prefab, int count)
    {
        if (prefab == null || count <= 0) return;
        if (!_pools.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<Projectile>();
            _pools[prefab] = stack;
        }

        for (int i = 0; i < count && stack.Count < maxPerPool; i++)
        {
            var p = Instantiate(prefab);
            p.MarkPooledSource(this, prefab);
            p.transform.SetParent(GetPoolRoot(), false);
            p.gameObject.SetActive(false);
            stack.Push(p);
        }
    }
}
