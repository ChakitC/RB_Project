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
    [Opsive.Shared.Utility.Description("Controls obstacle avoidance with quality and radius settings.")]
    public class ObstacleAvoidance : TargetGameObjectAction
    {
        [Tooltip("The obstacle avoidance quality.")]
        [SerializeField] protected ObstacleAvoidanceType m_ObstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        [Tooltip("The obstacle avoidance radius.")]
        [SerializeField] protected SharedVariable<float> m_Radius = 0.5f;

        private NavMeshAgent m_ResolvedAgent;

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
        /// Updates the obstacle avoidance settings.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAgent == null) {
                return TaskStatus.Success;
            }

            m_ResolvedAgent.obstacleAvoidanceType = m_ObstacleAvoidanceType;
            m_ResolvedAgent.radius = m_Radius.Value;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_ObstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            m_Radius = 0.5f;
        }
    }
}
#endif