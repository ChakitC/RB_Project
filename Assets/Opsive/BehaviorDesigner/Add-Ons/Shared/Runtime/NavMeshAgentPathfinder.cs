/// ---------------------------------------------
/// Shared Add-On for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.Shared.Runtime.Pathfinding
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;
    using UnityEngine.AI;

    /// <summary>
    /// Implements the Pathfindiner abstract class for the NavMeshAgent pathfinding implementation.
    /// </summary>
    public class NavMeshAgentPathfinder : Pathfinder
    {
        [Tooltip("Should the NavMeshAgent rotation be updated?")]
        [SerializeField] protected SharedVariable<bool> m_UpdateRotation = true;
        [Tooltip("Sets the minimum amount of time in between destination updates. This allows for throttling the number of path searches.")]
        [SerializeField] protected SharedVariable<float> m_DestinationUpdateInterval = 0;
        [Tooltip("Specifies the stopping distance of the NavMeshAgent.")]
        [SerializeField] protected SharedVariable<float> m_StoppingDistance = 0.2f;

        private NavMeshAgent m_NavMeshAgent;
        private bool m_StartUpdateRotation;
        private float m_SetDestinationTime;

        public override Vector3 Velocity { get => m_NavMeshAgent.velocity; }
        public override float RemainingDistance { get => m_NavMeshAgent.pathPending ? float.PositiveInfinity : m_NavMeshAgent.remainingDistance; }
        public override Vector3 Destination { get => m_NavMeshAgent.destination; }
        public override float Speed { get => m_NavMeshAgent.speed; set => m_NavMeshAgent.speed = value; }

        /// <summary>
        /// Initializes the Pathfinder.
        /// </summary>
        /// <param name="gameObject">The parent GameObject.</param>
        public override void Initialize(GameObject gameObject)
        {
            m_NavMeshAgent = gameObject.GetComponent<NavMeshAgent>();
            if (m_NavMeshAgent == null) {
                Debug.LogError($"Error: Unable to find the NavMeshAgent component on the {gameObject} GameObject.");
                return;
            }

            if (m_StoppingDistance.Value <= 0) {
                Debug.LogWarning("Warning: The NavMeshAgent stopping distance is set to 0. This should be a positive value to ensure the agent is able to arrive at their destination.");
                m_StoppingDistance.Value = 0.2f;
            }
        }

        /// <summary>
        /// The task has started.
        /// </summary>
        public override void OnStart()
        {
            m_NavMeshAgent.isStopped = false;
            m_NavMeshAgent.stoppingDistance = m_StoppingDistance.Value;
            m_StartUpdateRotation = m_NavMeshAgent.updateRotation;
            m_SetDestinationTime = -m_DestinationUpdateInterval.Value;
            UpdateRotation(m_UpdateRotation.Value);
        }

        /// <summary>
        /// Specifies if the rotation should be updated.
        /// </summary>
        /// <param name="update">Should the rotation be updated?</param>
        private void UpdateRotation(bool update)
        {
            m_NavMeshAgent.updateRotation = update;
            m_NavMeshAgent.updateUpAxis = update;
        }

        /// <summary>
        /// Set a new pathfinding destination.
        /// </summary>
        /// <param name="destination">The destination to set.</param>
        /// <returns>True if the destination is valid.</returns>
        public override bool SetDesination(Vector3 destination)
        {
            if (!m_NavMeshAgent.isStopped && m_NavMeshAgent.destination == destination) {
                return true;
            }

            // Prevent the destination from being set too often.
            if (m_SetDestinationTime + m_DestinationUpdateInterval.Value > Time.time) {
                return true;
            }

            m_NavMeshAgent.isStopped = false;
            m_SetDestinationTime = Time.time;
            return m_NavMeshAgent.SetDestination(destination);
        }

        /// <summary>
        /// Does the agent have a pathfinding path?
        /// </summary>
        /// <returns>True if the agent has a pathfinding path.</returns>
        public override bool HasPath()
        {
            return m_NavMeshAgent.hasPath && RemainingDistance > m_NavMeshAgent.stoppingDistance && !m_NavMeshAgent.isStopped;
        }

        /// <summary>
        /// Returns true if the position is a valid pathfinding position.
        /// </summary>
        /// <param name="position">The position to sample. The position will be updated to the valid sampled position.</param>
        /// <returns>True if the position is a valid pathfinding position.</returns>
        public override bool SamplePosition(ref Vector3 position)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(position, out hit, m_NavMeshAgent.height * 2, NavMesh.AllAreas)) {
                position = hit.position;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Has the agent arrived at the destination?
        /// </summary>
        /// <returns>True if the agent has arrived at the destination.</returns>
        public override bool HasArrived()
        {
            // The path hasn't been computed yet if the path is pending.
            float remainingDistance;
            if (m_NavMeshAgent.pathPending) {
                remainingDistance = float.PositiveInfinity;
            } else {
                remainingDistance = m_NavMeshAgent.remainingDistance;
            }

            return remainingDistance <= m_NavMeshAgent.stoppingDistance;
        }

        /// <summary>
        /// The agent should stop moving.
        /// </summary>
        public override void Stop()
        {
            if (m_NavMeshAgent != null && m_NavMeshAgent.isActiveAndEnabled) {
                m_NavMeshAgent.isStopped = true;
            }
        }

        /// <summary>
        /// The task has stopped.
        /// </summary>
        public override void OnEnd()
        {
            UpdateRotation(m_StartUpdateRotation);
        }

        /// <summary>
        /// Resets the values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_UpdateRotation = true;
            m_StoppingDistance = 0.2f;
        }
    }
}