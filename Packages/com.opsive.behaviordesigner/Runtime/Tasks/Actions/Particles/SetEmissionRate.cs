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
    [Opsive.Shared.Utility.Description("Sets emission rate with smooth transition.")]
    public class SetEmissionRate : TargetGameObjectAction
    {
        [Tooltip("The target emission rate.")]
        [SerializeField] protected SharedVariable<float> m_TargetEmissionRate = 10.0f;
        [Tooltip("The transition duration (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.0f;

        private ParticleSystem m_ResolvedParticleSystem;
        private ParticleSystem.EmissionModule m_EmissionModule;
        private float m_StartEmissionRate;
        private float m_ElapsedTime;

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
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;
            if (m_ResolvedParticleSystem != null) {
                m_EmissionModule = m_ResolvedParticleSystem.emission;
                m_StartEmissionRate = m_EmissionModule.rateOverTime.constant;
            }
        }

        /// <summary>
        /// Updates the emission rate.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedParticleSystem == null) {
                return TaskStatus.Success;
            }

            if (m_TransitionDuration.Value > 0.0f) {
                m_ElapsedTime += Time.deltaTime;
                var progress = Mathf.Clamp01(m_ElapsedTime / m_TransitionDuration.Value);
                var currentRate = Mathf.Lerp(m_StartEmissionRate, m_TargetEmissionRate.Value, progress);
                m_EmissionModule.rateOverTime = currentRate;
            } else {
                m_EmissionModule.rateOverTime = m_TargetEmissionRate.Value;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_TargetEmissionRate = 10.0f;
            m_TransitionDuration = 0.0f;
        }
    }
}
#endif