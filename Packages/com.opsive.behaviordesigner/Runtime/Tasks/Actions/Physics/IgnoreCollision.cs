#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.PhysicsTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Physics")]
    [Opsive.Shared.Utility.Description("Ignores collision between two colliders with optional duration.")]
    public class IgnoreCollision : Action
    {
        [Tooltip("The first Collider.")]
        [SerializeField] protected SharedVariable<Collider> m_Collider1;
        [Tooltip("The second Collider.")]
        [SerializeField] protected SharedVariable<Collider> m_Collider2;
        [Tooltip("Whether to ignore the collision.")]
        [SerializeField] protected SharedVariable<bool> m_Ignore = true;
        [Tooltip("The duration to ignore collision (0 = permanent).")]
        [SerializeField] protected SharedVariable<float> m_Duration = 0.0f;

        private float m_ElapsedTime;
        private bool m_HasSetIgnore;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;
            m_HasSetIgnore = false;
        }

        /// <summary>
        /// Updates the collision ignore state.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Collider1.Value == null || m_Collider2.Value == null) {
                return TaskStatus.Success;
            }

            if (!m_HasSetIgnore) {
                Physics.IgnoreCollision(m_Collider1.Value, m_Collider2.Value, m_Ignore.Value);
                m_HasSetIgnore = true;
            }

            if (m_Duration.Value > 0.0f) {
                m_ElapsedTime += Time.deltaTime;
                if (m_ElapsedTime >= m_Duration.Value) {
                    Physics.IgnoreCollision(m_Collider1.Value, m_Collider2.Value, !m_Ignore.Value);
                    return TaskStatus.Success;
                }
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Collider1 = null;
            m_Collider2 = null;
            m_Ignore = true;
            m_Duration = 0.0f;
        }
    }
}
#endif