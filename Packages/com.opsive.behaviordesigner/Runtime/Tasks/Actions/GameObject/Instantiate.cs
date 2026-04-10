#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections;
    using UnityEngine;
    using UnityEngine.Pool;

    [Opsive.Shared.Utility.Category("GameObject")]
    [Opsive.Shared.Utility.Description("Instantiates a GameObject prefab with configurable delay, position, rotation, scale (or prefab scale), and parent. Can optionally auto-destroy after a duration.")]
    public class Instantiate : Action
    {
        [Tooltip("The prefab to instantiate.")]
        [SerializeField] protected SharedVariable<GameObject> m_Prefab;
        [Tooltip("The delay before instantiation (in seconds).")]
        [SerializeField] protected SharedVariable<float> m_Delay = 0f;
        [Tooltip("The GameObject to use for position and rotation. If null, uses Position and Rotation values.")]
        [SerializeField] protected SharedVariable<GameObject> m_Location;
        [Tooltip("Should local position and rotation be used when Transform is specified?")]
        [SerializeField] protected SharedVariable<bool> m_UseLocalTransform = false;
        [Tooltip("The position to instantiate at. Only used if Location is null.")]
        [SerializeField] protected SharedVariable<Vector3> m_Position;
        [Tooltip("The rotation to instantiate with. Only used if Location is null.")]
        [SerializeField] protected SharedVariable<Vector3> m_Rotation;
        [Tooltip("Use the prefab's local scale instead of the Scale value.")]
        [SerializeField] protected SharedVariable<bool> m_UsePrefabScale = false;
        [Tooltip("The scale to instantiate with. Only used if Use Prefab Scale is disabled.")]
        [SerializeField] protected SharedVariable<Vector3> m_Scale = Vector3.one;
        [Tooltip("The parent GameObject to assign. If null, no parent is assigned.")]
        [SerializeField] protected SharedVariable<GameObject> m_Parent;
        [Tooltip("Should object pooling be used?")]
        [SerializeField] protected SharedVariable<bool> m_UsePooling = false;
        [Tooltip("The initial pool size. Only used if Use Pooling is enabled.")]
        [SerializeField] protected SharedVariable<int> m_PoolSize = 10;
        [Tooltip("The maximum pool size. Only used if Use Pooling is enabled.")]
        [SerializeField] protected SharedVariable<int> m_MaxPoolSize = 20;
        [Tooltip("Should the GameObject be auto-destroyed after a duration?")]
        [SerializeField] protected SharedVariable<bool> m_AutoDestroy = false;
        [Tooltip("The duration before auto-destroy. Only used if Auto Destroy is enabled.")]
        [SerializeField] protected SharedVariable<float> m_DestroyDuration = 5f;
        [Tooltip("The name to assign to the instantiated GameObject. If empty, uses the prefab name.")]
        [SerializeField] protected SharedVariable<string> m_ObjectName;
        [Tooltip("The instantiated GameObject.")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject> m_InstantiatedObject;

        private float m_ElapsedTime;
        private bool m_HasInstantiated;
        private ObjectPool<GameObject> m_Pool;

        /// <summary>
        /// Called when the state machine is initialized.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();
            if (m_UsePooling.Value && m_Prefab.Value != null) {
                InitializePool();
            }
        }

        /// <summary>
        /// Initializes the object pool.
        /// </summary>
        private void InitializePool()
        {
            if (m_Pool != null) {
                return;
            }

            m_Pool = new ObjectPool<GameObject>(
                createFunc: () => {
                    var instance = m_Parent.Value != null ? UnityEngine.Object.Instantiate(m_Prefab.Value, m_Parent.Value.transform) : UnityEngine.Object.Instantiate(m_Prefab.Value);
                    instance.SetActive(false);
                    return instance;
                },
                actionOnGet: (obj) => {
                    obj.SetActive(true);
                },
                actionOnRelease: (obj) => {
                    obj.SetActive(false);
                    PoolManager.Instance.Unregister(obj);
                },
                actionOnDestroy: (obj) => {
                    UnityEngine.Object.Destroy(obj);
                },
                collectionCheck: true,
                defaultCapacity: m_PoolSize.Value,
                maxSize: m_MaxPoolSize.Value
            );
        }

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0f;
            m_HasInstantiated = false;
        }

        /// <summary>
        /// Instantiates the GameObject after delay.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Prefab.Value == null) {
                Debug.LogError("Instantiate: Prefab is not assigned.");
                return TaskStatus.Success;
            }

            m_ElapsedTime += Time.deltaTime;

            if (!m_HasInstantiated && m_ElapsedTime >= m_Delay.Value) {
                // Initialize pool if needed.
                if (m_UsePooling.Value && m_Pool == null && m_Prefab.Value != null) {
                    InitializePool();
                }

                // Determine position and rotation.
                Vector3 position;
                Quaternion rotation;
                if (m_Location.Value != null) {
                    position = m_UseLocalTransform.Value ? m_Location.Value.transform.localPosition : m_Location.Value.transform.position;
                    rotation = m_UseLocalTransform.Value ? m_Location.Value.transform.localRotation : m_Location.Value.transform.rotation;
                } else {
                    position = m_Position.Value;
                    rotation = Quaternion.Euler(m_Rotation.Value);
                }

                // Get or instantiate the GameObject.
                GameObject instantiated;
                if (m_UsePooling.Value && m_Pool != null) {
                    instantiated = m_Pool.Get();
                    PoolManager.Instance.Register(instantiated, m_Pool);

                    if (m_Parent.Value != null) {
                        instantiated.transform.SetParent(m_Parent.Value.transform);
                    }
                    instantiated.transform.position = position;
                    instantiated.transform.rotation = rotation;
                } else {
                    instantiated = m_Parent.Value != null ? UnityEngine.Object.Instantiate(m_Prefab.Value, position, rotation, m_Parent.Value.transform) : UnityEngine.Object.Instantiate(m_Prefab.Value, position, rotation);
                }

                instantiated.transform.localScale = m_UsePrefabScale.Value ? m_Prefab.Value.transform.localScale : m_Scale.Value;
                if (!string.IsNullOrEmpty(m_ObjectName.Value)) {
                    instantiated.name = m_ObjectName.Value;
                }

                m_InstantiatedObject.Value = instantiated;
                m_HasInstantiated = true;

                // Setup auto-destroy if enabled.
                if (m_AutoDestroy.Value) {
                    StartCoroutine(ReleaseOrDestroyAfterDelay(instantiated, m_DestroyDuration.Value, m_UsePooling.Value && m_Pool != null));
                }
            }

            return m_HasInstantiated ? TaskStatus.Success : TaskStatus.Running;
        }

        /// <summary>
        /// Releases the GameObject back to the pool or destroys it after a delay.
        /// </summary>
        /// <param name="obj">The GameObject to release or destroy.</param>
        /// <param name="delay">The delay before releasing or destroying.</param>
        /// <param name="usePooling">Should the object be released to the pool? If false, it will be destroyed.</param>
        /// <returns>The coroutine enumerator.</returns>
        private IEnumerator ReleaseOrDestroyAfterDelay(GameObject obj, float delay, bool usePooling)
        {
            yield return new WaitForSeconds(delay);

            if (obj == null) {
                yield break;
            }

            if (usePooling) {
                if (!obj.activeSelf) {
                    yield break;
                }
                // Check if object is no longer registered with PoolManager (already released).
                if (PoolManager.Instance.GetPool(obj) == null) {
                    yield break;
                }
                PoolManager.Instance.ReleaseToPool(obj);
            } else {
                UnityEngine.Object.Destroy(obj);
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
            m_Prefab = null;
            m_Delay = 0f;
            m_Location = null;
            m_UseLocalTransform = false;
            m_Position = Vector3.zero;
            m_Rotation = Vector3.zero;
            m_UsePrefabScale = false;
            m_Scale = Vector3.one;
            m_Parent = null;
            m_UsePooling = false;
            m_PoolSize = 10;
            m_MaxPoolSize = 20;
            m_AutoDestroy = false;
            m_DestroyDuration = 5f;
            m_ObjectName = null;
            m_InstantiatedObject = null;
        }
    }
}
#endif