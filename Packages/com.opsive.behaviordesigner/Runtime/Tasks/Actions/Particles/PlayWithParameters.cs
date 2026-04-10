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
    [Opsive.Shared.Utility.Description("Plays particle system with emission rate, color, and lifetime control.")]
    public class PlayWithParameters : TargetGameObjectAction
    {
        [Tooltip("The emission rate override.")]
        [SerializeField] protected SharedVariable<float> m_EmissionRate = -1.0f;
        [Tooltip("The particle color. Alpha component controls transparency.")]
        [SerializeField] protected SharedVariable<Color> m_ParticleColor;
        [Tooltip("The particle lifetime multiplier.")]
        [SerializeField] protected SharedVariable<float> m_LifetimeMultiplier = 1.0f;
        [Tooltip("Whether to play the particle system.")]
        [SerializeField] protected SharedVariable<bool> m_Play = true;

        private ParticleSystem m_ResolvedParticleSystem;
        private ParticleSystem.EmissionModule m_EmissionModule;
        private ParticleSystem.MainModule m_MainModule;

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
        /// Updates the particle system.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedParticleSystem == null) {
                return TaskStatus.Success;
            }

            m_EmissionModule = m_ResolvedParticleSystem.emission;
            m_MainModule = m_ResolvedParticleSystem.main;

            if (m_EmissionRate.Value >= 0.0f) {
                m_EmissionModule.rateOverTime = m_EmissionRate.Value;
            }

            m_MainModule.startColor = m_ParticleColor.Value;

            if (m_LifetimeMultiplier.Value > 0.0f) {
                m_MainModule.startLifetimeMultiplier = m_LifetimeMultiplier.Value;
            }

            if (m_Play.Value) {
                m_ResolvedParticleSystem.Play();
            } else {
                m_ResolvedParticleSystem.Stop();
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_EmissionRate = -1.0f;
            m_ParticleColor = null;
            m_LifetimeMultiplier = 1.0f;
            m_Play = true;
        }
    }
}
#endif