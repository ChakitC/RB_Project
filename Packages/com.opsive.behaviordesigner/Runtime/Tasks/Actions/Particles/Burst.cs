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
    [Opsive.Shared.Utility.Description("Bursts particles with count, cycles, and interval control.")]
    public class Burst : TargetGameObjectAction
    {
        [Tooltip("The number of particles to burst.")]
        [SerializeField] protected SharedVariable<int> m_BurstCount = 10;
        [Tooltip("The number of burst cycles.")]
        [SerializeField] protected SharedVariable<int> m_Cycles = 1;
        [Tooltip("The interval between bursts (seconds).")]
        [SerializeField] protected SharedVariable<float> m_Interval = 0.0f;

        private ParticleSystem m_ResolvedParticleSystem;
        private int m_CurrentCycle;
        private float m_NextBurstTime;

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
            m_CurrentCycle = 0;
            m_NextBurstTime = 0.0f;
        }

        /// <summary>
        /// Updates the particle burst.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedParticleSystem == null) {
                return TaskStatus.Success;
            }

            if (m_CurrentCycle < m_Cycles.Value && Time.time >= m_NextBurstTime) {
                m_ResolvedParticleSystem.Emit(m_BurstCount.Value);
                m_CurrentCycle++;
                m_NextBurstTime = Time.time + m_Interval.Value;
            }

            if (m_CurrentCycle >= m_Cycles.Value) {
                return TaskStatus.Success;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_BurstCount = 10;
            m_Cycles = 1;
            m_Interval = 0.0f;
        }
    }
}
#endif