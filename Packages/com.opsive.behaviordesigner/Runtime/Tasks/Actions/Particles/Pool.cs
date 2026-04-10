#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.ParticlesTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Pool;

    [Opsive.Shared.Utility.Category("Particles")]
    [Opsive.Shared.Utility.Description("Manages particle system pooling with automatic cleanup.")]
    public class Pool : Action
    {
        [Tooltip("The parent GameObject to create pooled particle systems under.")]
        [SerializeField] protected SharedVariable<GameObject> m_PoolParent;
        [Tooltip("The ParticleSystem prefab to pool.")]
        [SerializeField] protected SharedVariable<GameObject> m_ParticleSystemPrefab;
        [Tooltip("The initial pool size.")]
        [SerializeField] protected SharedVariable<int> m_PoolSize = 5;
        [Tooltip("The maximum pool size.")]
        [SerializeField] protected SharedVariable<int> m_MaxPoolSize = 10;
        [Tooltip("The currently active ParticleSystem from the pool.")]
        [SerializeField] [RequireShared] protected SharedVariable<ParticleSystem> m_ActiveParticleSystem;

        private ObjectPool<GameObject> m_Pool;
        private List<ParticleSystem> m_ActiveSystems = new List<ParticleSystem>();

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();
            if (m_ParticleSystemPrefab.Value != null) {
                InitializePool();
            }
        }

        /// <summary>
        /// Initializes the particle system pool.
        /// </summary>
        private void InitializePool()
        {
            if (m_Pool != null) {
                return;
            }

            var poolParent = m_PoolParent.Value != null ? m_PoolParent.Value : m_GameObject;

            m_Pool = new ObjectPool<GameObject>(
                createFunc: () => {
                    var go = Object.Instantiate(m_ParticleSystemPrefab.Value);
                    go.transform.SetParent(poolParent != null ? poolParent.transform : null);
                    go.SetActive(false);
                    return go;
                },
                actionOnGet: (obj) => {
                    obj.SetActive(true);
                },
                actionOnRelease: (obj) => {
                    obj.SetActive(false);
                    var particleSystem = obj.GetComponent<ParticleSystem>();
                    if (particleSystem != null) {
                        particleSystem.Stop();
                        particleSystem.Clear();
                    }
                    PoolManager.Instance.Unregister(obj);
                },
                actionOnDestroy: (obj) => {
                    Object.Destroy(obj);
                },
                collectionCheck: true,
                defaultCapacity: m_PoolSize.Value,
                maxSize: m_MaxPoolSize.Value
            );
        }

        /// <summary>
        /// Gets an available particle system from the pool.
        /// </summary>
        private ParticleSystem GetPooledParticleSystem()
        {
            if (m_Pool == null) {
                return null;
            }

            var go = m_Pool.Get();
            if (go != null) {
                PoolManager.Instance.Register(go, m_Pool);
                return go.GetComponent<ParticleSystem>();
            }

            return null;
        }

        /// <summary>
        /// Updates the particle system pool.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            for (int i = m_ActiveSystems.Count - 1; i >= 0; i--) {
                if (m_ActiveSystems[i] == null || !m_ActiveSystems[i].isPlaying) {
                    if (m_ActiveSystems[i] != null && m_Pool != null) {
                        m_Pool.Release(m_ActiveSystems[i].gameObject);
                    }
                    m_ActiveSystems.RemoveAt(i);
                }
            }

            if (m_ActiveParticleSystem.Value != null && !m_ActiveParticleSystem.Value.isPlaying) {
                m_ActiveParticleSystem.Value = null;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Plays a particle system from the pool.
        /// </summary>
        public void PlayFromPool(Vector3 position)
        {
            var particleSystem = GetPooledParticleSystem();
            if (particleSystem != null) {
                particleSystem.transform.position = position;
                particleSystem.Play();
                m_ActiveSystems.Add(particleSystem);
                m_ActiveParticleSystem.Value = particleSystem;
            }
        }

        /// <summary>
        /// Called when the state machine is destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();
            // Clean up pool when state machine is destroyed.
            if (m_Pool != null) {
                m_Pool.Dispose();
                m_Pool = null;
            }
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_PoolParent = null;
            m_ParticleSystemPrefab = null;
            m_PoolSize = 5;
            m_MaxPoolSize = 10;
            m_ActiveParticleSystem = null;
        }
    }
}
#endif