#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("GameObject")]
    [Opsive.Shared.Utility.Description("Destroys a GameObject after a delay. Can optionally release pooled objects back to their pool instead of destroying them.")]
    public class Destroy : TargetGameObjectAction
    {
        [Tooltip("The delay before destruction (in seconds).")]
        [SerializeField] protected SharedVariable<float> m_Delay = 0f;
        [Tooltip("Should pooled objects be released back to their pool instead of being destroyed?")]
        [SerializeField] protected SharedVariable<bool> m_ReleaseToPool = false;

        private float m_ElapsedTime;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0f;
        }

        /// <summary>
        /// Destroys the GameObject after delay.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_ElapsedTime += Time.deltaTime;

            if (m_ElapsedTime >= m_Delay.Value) {
                if (m_ReleaseToPool.Value) { // Try to release to pool.
                    if (!PoolManager.Instance.ReleaseToPool(gameObject)) {
                        UnityEngine.Object.Destroy(gameObject);
                    }
                } else { // Destroy the GameObject.
                    UnityEngine.Object.Destroy(gameObject);
                }

                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Delay = 0f;
            m_ReleaseToPool = false;
        }
    }
}
#endif