#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.ParticlesTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Particles")]
    [Opsive.Shared.Utility.Description("Sets particle velocity with mode selection and variation.")]
    public class SetVelocity : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the velocity mode.
        /// </summary>
        public enum VelocityMode
        {
            Linear,
            Random
        }

        [Tooltip("The velocity mode.")]
        [SerializeField] protected VelocityMode m_VelocityMode = VelocityMode.Linear;
        [Tooltip("The velocity vector.")]
        [SerializeField] protected SharedVariable<Vector3> m_Velocity = Vector3.zero;
        [Tooltip("The velocity variation (for random mode).")]
        [SerializeField] protected SharedVariable<Vector3> m_VelocityVariation = Vector3.zero;

        private ParticleSystem m_ResolvedParticleSystem;
        private ParticleSystem.VelocityOverLifetimeModule m_VelocityOverLifetime;

        /// <summary>
        /// Called when the state machine is initialized.
        /// </summary>
        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedParticleSystem = m_ResolvedGameObject.GetComponent<ParticleSystem>();
        }

        /// <summary>
        /// Updates the velocity settings.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedParticleSystem == null) {
                return TaskStatus.Success;
            }

            m_VelocityOverLifetime = m_ResolvedParticleSystem.velocityOverLifetime;
            m_VelocityOverLifetime.enabled = true;

            if (m_VelocityMode == VelocityMode.Linear) {
                m_VelocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
                m_VelocityOverLifetime.x = m_Velocity.Value.x;
                m_VelocityOverLifetime.y = m_Velocity.Value.y;
                m_VelocityOverLifetime.z = m_Velocity.Value.z;
            } else {
                m_VelocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
                m_VelocityOverLifetime.x = new ParticleSystem.MinMaxCurve(m_Velocity.Value.x - m_VelocityVariation.Value.x, m_Velocity.Value.x + m_VelocityVariation.Value.x);
                m_VelocityOverLifetime.y = new ParticleSystem.MinMaxCurve(m_Velocity.Value.y - m_VelocityVariation.Value.y, m_Velocity.Value.y + m_VelocityVariation.Value.y);
                m_VelocityOverLifetime.z = new ParticleSystem.MinMaxCurve(m_Velocity.Value.z - m_VelocityVariation.Value.z, m_Velocity.Value.z + m_VelocityVariation.Value.z);
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_VelocityMode = VelocityMode.Linear;
            m_Velocity = Vector3.zero;
            m_VelocityVariation = Vector3.zero;
        }
    }
}
#endif