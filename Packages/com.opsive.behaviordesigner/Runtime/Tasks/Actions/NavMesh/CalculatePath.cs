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
    [Opsive.Shared.Utility.Description("Calculates NavMesh path with distance and corner count.")]
    public class CalculatePath : TargetGameObjectAction
    {
        [Tooltip("The target position.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetPosition;
        [Tooltip("The calculated path distance.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_PathDistance;
        [Tooltip("The number of corners in the path.")]
        [SerializeField] [RequireShared] protected SharedVariable<int> m_CornerCount;
        [Tooltip("Whether a valid path was found.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_PathFound;

        private NavMeshAgent m_ResolvedAgent;
        private NavMeshPath m_Path;

        /// <summary>
        /// Called when the state machine is initialized.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();
            m_Path = new NavMeshPath();
        }

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
        /// Updates the path calculation.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAgent == null) {
                m_PathFound.Value = false;
                m_PathDistance.Value = 0.0f;
                m_CornerCount.Value = 0;
                return TaskStatus.Success;
            }

            var startPosition = m_ResolvedAgent.transform.position;

            var pathFound = NavMesh.CalculatePath(startPosition, m_TargetPosition.Value, NavMesh.AllAreas, m_Path);
            m_PathFound.Value = pathFound;

            if (pathFound) {
                var distance = 0.0f;
                for (int i = 0; i < m_Path.corners.Length - 1; ++i) {
                    distance += Vector3.Distance(m_Path.corners[i], m_Path.corners[i + 1]);
                }
                m_PathDistance.Value = distance;
                m_CornerCount.Value = m_Path.corners.Length;
            } else {
                m_PathDistance.Value = 0.0f;
                m_CornerCount.Value = 0;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_TargetPosition = null;
            m_PathDistance = null;
            m_CornerCount = null;
            m_PathFound = null;
        }
    }
}
#endif