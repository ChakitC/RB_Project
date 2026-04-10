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
    [Opsive.Shared.Utility.Description("Sets NavMesh destination with path validation and arrival detection.")]
    public class SetDestination : TargetGameObjectAction
    {
        [Tooltip("The target GameObject to move towards. If null, uses Target Position.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The target position. Only used if Target is null.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetPosition;
        [Tooltip("The arrival distance threshold.")]
        [SerializeField] protected SharedVariable<float> m_ArrivedDistance = 0.5f;
        [Tooltip("Whether the agent has arrived at the destination.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_HasArrived;

        private NavMeshAgent m_ResolvedAgent;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedAgent = m_ResolvedGameObject.GetComponent<NavMeshAgent>();
        }

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            if (m_ResolvedAgent == null) {
                Debug.LogError($"Error: The GameObject {m_ResolvedGameObject} must have a NavMeshAgent component.");
            } else {
                m_ResolvedAgent.isStopped = false;
            }
        }

        /// <summary>
        /// Updates the NavMesh destination.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAgent == null) {
                return TaskStatus.Failure;
            }

            var destination = GetTargetDestination();
            if (Vector3.Distance(m_ResolvedAgent.destination, destination) > 0.1f) {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(destination, out hit, 5.0f, NavMesh.AllAreas)) {
                    m_ResolvedAgent.SetDestination(hit.position);
                    m_HasArrived.Value = false;
                }
            }

            if (!m_ResolvedAgent.pathPending && m_ResolvedAgent.remainingDistance < m_ArrivedDistance.Value) {
                m_HasArrived.Value = true;
                m_ResolvedAgent.isStopped = true;
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Gets the target destination.
        /// </summary>
        private Vector3 GetTargetDestination()
        {
            if (m_Target.Value != null) {
                return m_Target.Value.transform.position;
            } else {
                return m_TargetPosition.Value;
            }
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Target = null;
            m_TargetPosition = null;
            m_ArrivedDistance = 0.5f;
            m_HasArrived = null;
        }
    }
}
#endif