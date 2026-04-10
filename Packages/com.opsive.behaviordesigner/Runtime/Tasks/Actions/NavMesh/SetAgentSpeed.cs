#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.NavMeshTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;
    using UnityEngine.AI;

    [Opsive.Shared.Utility.Category("NavMesh")]
    [Opsive.Shared.Utility.Description("Sets agent speed with smooth transition and acceleration.")]
    public class SetAgentSpeed : TargetGameObjectAction
    {
        [Tooltip("The target speed.")]
        [SerializeField] protected SharedVariable<float> m_TargetSpeed = 3.5f;
        [Tooltip("The transition duration (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.0f;
        [Tooltip("The acceleration rate.")]
        [SerializeField] protected SharedVariable<float> m_Acceleration = 8.0f;

        private NavMeshAgent m_ResolvedAgent;
        private float m_StartSpeed;
        private float m_ElapsedTime;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedAgent = m_ResolvedGameObject.GetComponent<NavMeshAgent>();
            if (m_ResolvedAgent == null) {
                Debug.LogError($"Error: The GameObject {m_ResolvedGameObject} must have a NavMeshAgent component.");
            }
        }

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;
            if (m_ResolvedAgent != null) {
                m_StartSpeed = m_ResolvedAgent.speed;
            }
        }

        /// <summary>
        /// Updates the agent speed.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAgent == null) {
                return TaskStatus.Failure;
            }

            if (m_TransitionDuration.Value > 0.0f) {
                m_ElapsedTime += Time.deltaTime;
                var progress = Mathf.Clamp01(m_ElapsedTime / m_TransitionDuration.Value);
                m_ResolvedAgent.speed = Mathf.Lerp(m_StartSpeed, m_TargetSpeed.Value, progress);
                m_ResolvedAgent.acceleration = m_Acceleration.Value;
                return progress >= 1.0f ? TaskStatus.Success : TaskStatus.Running;
            }

            m_ResolvedAgent.speed = m_TargetSpeed.Value;
            m_ResolvedAgent.acceleration = m_Acceleration.Value;
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_TargetSpeed = 3.5f;
            m_TransitionDuration = 0.0f;
            m_Acceleration = 8.0f;
        }
    }
}
#endif