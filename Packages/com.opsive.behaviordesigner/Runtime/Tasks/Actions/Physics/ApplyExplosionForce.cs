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
    [Opsive.Shared.Utility.Description("Applies explosion force with radius, upward modifier, and force mode.")]
    public class ApplyExplosionForce : TargetGameObjectAction
    {
        [Tooltip("The explosion force.")]
        [SerializeField] protected SharedVariable<float> m_Force = 1000.0f;
        [Tooltip("The explosion position.")]
        [SerializeField] protected SharedVariable<Vector3> m_Position;
        [Tooltip("The explosion radius.")]
        [SerializeField] protected SharedVariable<float> m_Radius = 5.0f;
        [Tooltip("The upward modifier (0 = no upward force, 1 = full upward force).")]
        [SerializeField] protected SharedVariable<float> m_UpwardModifier = 0.0f;
        [Tooltip("The force mode.")]
        [SerializeField] protected ForceMode m_ForceMode = ForceMode.Force;

        private UnityEngine.Rigidbody m_ResolvedRigidbody;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedRigidbody = m_ResolvedGameObject.GetComponent<UnityEngine.Rigidbody>();
        }

        /// <summary>
        /// Applies the explosion force.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody == null) {
                return TaskStatus.Success;
            }

            m_ResolvedRigidbody.AddExplosionForce(m_Force.Value, m_Position.Value, m_Radius.Value, m_UpwardModifier.Value, m_ForceMode);

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Force = 1000.0f;
            m_Position = null;
            m_Radius = 5.0f;
            m_UpwardModifier = 0.0f;
            m_ForceMode = ForceMode.Force;
        }
    }
}
#endif