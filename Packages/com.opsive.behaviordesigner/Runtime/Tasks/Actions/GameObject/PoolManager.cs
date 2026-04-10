#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Pool;

    /// <summary>
    /// Singleton manager that tracks which pool each GameObject belongs to.
    /// </summary>
    public class PoolManager
    {
        private static PoolManager s_Instance;
        private Dictionary<GameObject, ObjectPool<GameObject>> m_PoolMap = new Dictionary<GameObject, ObjectPool<GameObject>>();

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static PoolManager Instance
        {
            get {
                if (s_Instance == null) {
                    s_Instance = new PoolManager();
                }
                return s_Instance;
            }
        }

        /// <summary>
        /// Registers a GameObject with its pool.
        /// </summary>
        /// <param name="obj">The GameObject to register.</param>
        /// <param name="pool">The pool that the GameObject belongs to.</param>
        public void Register(GameObject obj, ObjectPool<GameObject> pool)
        {
            if (obj != null && pool != null) {
                m_PoolMap[obj] = pool;
            }
        }

        /// <summary>
        /// Unregisters a GameObject from the pool manager.
        /// </summary>
        /// <param name="obj">The GameObject to unregister.</param>
        public void Unregister(GameObject obj)
        {
            if (obj != null) {
                m_PoolMap.Remove(obj);
            }
        }

        /// <summary>
        /// Releases a GameObject back to its pool.
        /// </summary>
        /// <param name="obj">The GameObject to release.</param>
        /// <returns>True if the object was released to a pool, false otherwise.</returns>
        public bool ReleaseToPool(GameObject obj)
        {
            if (obj != null && m_PoolMap.TryGetValue(obj, out var pool)) {
                pool.Release(obj);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the pool for a GameObject.
        /// </summary>
        /// <param name="obj">The GameObject to get the pool for.</param>
        /// <returns>The pool, or null if not found.</returns>
        public ObjectPool<GameObject> GetPool(GameObject obj)
        {
            if (obj != null && m_PoolMap.TryGetValue(obj, out var pool)) {
                return pool;
            }
            return null;
        }

        /// <summary>
        /// Clears all registered pools.
        /// </summary>
        public void Clear()
        {
            m_PoolMap.Clear();
        }
    }
}
#endif