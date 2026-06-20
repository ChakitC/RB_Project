using System.Collections.Generic;
using UnityEngine;

public sealed class VfxPool : MonoBehaviour
{
    [SerializeField] int maxPerPool = 20;

    readonly Dictionary<GameObject, Stack<GameObject>> _pools = new();

    Transform _poolRoot;

    public GameObject Get(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent, float scale)
    {
        if (_pools.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            while (stack.Count > 0)
            {
                GameObject instance = stack.Pop();
                if (instance == null) continue;

                instance.GetComponent<PooledVfxHandle>().PrepareForReuse(pos, rot, parent, scale);
                return instance;
            }
        }

        return CreateNew(prefab, pos, rot, parent, scale);
    }

    public void PushToStack(GameObject prefab, GameObject instance)
    {
        if (!_pools.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            _pools[prefab] = stack;
        }

        if (stack.Count >= maxPerPool)
        {
            Destroy(instance);
            return;
        }

        stack.Push(instance);
    }

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

    GameObject CreateNew(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent, float scale)
    {
        GameObject instance = Instantiate(prefab, pos, rot, parent);
        PooledVfxHandle handle = instance.AddComponent<PooledVfxHandle>();
        handle.SourcePrefab = prefab;
        handle.Pool = this;
        handle.CaptureState();
        handle.PrepareForReuse(pos, rot, parent, scale);
        return instance;
    }
}
