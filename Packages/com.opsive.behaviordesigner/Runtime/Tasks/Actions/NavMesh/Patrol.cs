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
    [Opsive.Shared.Utility.Description("Uses the NavMeshAgent to patrol through a set of waypoints.")]
    public class Patrol : TargetGameObjectAction
    {
        [Tooltip("The patrol waypoints.")]
        [SerializeField] protected SharedVariable<GameObject[]> m_Waypoints;
        [Tooltip("Should the starting waypoint be randomized?")]
        [SerializeField] protected SharedVariable<bool> m_RandomStart;

        private NavMeshAgent m_ResolvedAgent;
        private int m_Index;

        /// <summary>
        /// Called when the state machine is initialized.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();
            m_Waypoints.OnValueChange += WaypointsChanged;
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
        /// The Waypoints SharedVariable has changed.
        /// </summary>
        private void WaypointsChanged()
        {
            if (m_ResolvedAgent == null || m_Waypoints.Value == null || m_Waypoints.Value.Length == 0) {
                return;
            }

            m_Index = (m_Index + 1) % m_Waypoints.Value.Length;
            if (m_Waypoints.Value[m_Index] != null) {
                m_ResolvedAgent.SetDestination(m_Waypoints.Value[m_Index].transform.position);
            }
        }

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            if (m_ResolvedAgent == null || m_Waypoints.Value == null || m_Waypoints.Value.Length == 0) {
                return;
            }

            m_Index = m_RandomStart.Value ? Random.Range(0, m_Waypoints.Value.Length) : 0;
            m_ResolvedAgent.isStopped = false;
            if (m_Waypoints.Value[m_Index] != null) {
                m_ResolvedAgent.SetDestination(m_Waypoints.Value[m_Index].transform.position);
            }
        }

        /// <summary>
        /// Patrols the waypoints. Always returns running.
        /// </summary>
        /// <returns>A status of running.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAgent == null || m_Waypoints.Value == null || m_Waypoints.Value.Length == 0) {
                return TaskStatus.Failure;
            }

            if (!m_ResolvedAgent.pathPending && m_ResolvedAgent.remainingDistance <= m_ResolvedAgent.stoppingDistance) {
                m_Index = (m_Index + 1) % m_Waypoints.Value.Length;
                if (m_Waypoints.Value[m_Index] != null) {
                    m_ResolvedAgent.SetDestination(m_Waypoints.Value[m_Index].transform.position);
                }
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// The action has ended.
        /// </summary>
        public override void OnEnd()
        {
            base.OnEnd();
            if (m_ResolvedAgent != null && m_ResolvedAgent.isOnNavMesh) {
                m_ResolvedAgent.isStopped = true;
            }
        }

        /// <summary>
        /// The state machine has been destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();
            m_Waypoints.OnValueChange -= WaypointsChanged;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Waypoints = null;
            m_RandomStart = false;
        }
    }
}
#endif