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

    // Fresh instances are created underneath this permanently deactivated object. An object
    // parented to an inactive transform is never active in the hierarchy, so Unity does not run
    // Awake/OnEnable on it. That is the only way to hand a caller a brand-new projectile whose
    // OnEnable has not fired yet, which the atomic spawn lifecycle depends on.
    Transform _inactiveRoot;

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

    Transform GetInactiveRoot()
    {
        if (_inactiveRoot != null) return _inactiveRoot;
        var existing = transform.Find("ProjectileInactiveRoot");
        if (existing != null)
        {
            existing.gameObject.SetActive(false);
            _inactiveRoot = existing;
            return _inactiveRoot;
        }

        var go = new GameObject("ProjectileInactiveRoot");
        go.transform.SetParent(transform, false);
        go.SetActive(false);
        _inactiveRoot = go.transform;
        return _inactiveRoot;
    }

    void OnApplicationQuit() => _isQuitting = true;

    void OnDestroy() { if (_instance == this) _instance = null; }

    /// <summary>
    /// Step 1 of the atomic spawn lifecycle: hand back an instance that is placed and fully reset
    /// but still <b>inactive</b>. The caller applies runtime state (context, stats, layer) and then
    /// calls <see cref="ActivateForSpawn"/>, so no OnEnable ever observes half-initialized state.
    /// </summary>
    public Projectile AcquireInactive(Projectile prefab, Vector3 pos, Quaternion rot)
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

        if (instance == null) instance = CreateInactiveInstance(prefab);
        if (instance == null) return null;

        instance.BeginSpawn(this, prefab, pos, rot);
        return instance;
    }

    /// <summary>
    /// Step 2 of the atomic spawn lifecycle: activate an instance handed out by
    /// <see cref="AcquireInactive"/> once its runtime state is complete.
    /// </summary>
    public void ActivateForSpawn(Projectile instance)
    {
        if (instance == null) return;
        instance.CompleteSpawn();
    }

    Projectile CreateInactiveInstance(Projectile prefab)
    {
        var instance = Instantiate(prefab, GetInactiveRoot());
        if (instance == null) return null;

        // Clear the object's own active flag while it is still shielded by the inactive root, so
        // the later reparent to the scene root cannot wake it up early.
        instance.gameObject.SetActive(false);
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

        instance.gameObject.SetActive(false);
        instance.transform.SetParent(GetPoolRoot(), false);
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
            var p = CreateInactiveInstance(prefab);
            if (p == null) break;
            p.MarkPooledSource(this, prefab);
            p.transform.SetParent(GetPoolRoot(), false);
            stack.Push(p);
        }
    }
}
