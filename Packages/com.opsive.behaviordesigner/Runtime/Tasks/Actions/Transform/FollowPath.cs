#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections.Generic;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Transform")]
    [Opsive.Shared.Utility.Description("Follows a path of waypoints. Returns Finished when the path is complete.")]
    public class FollowPath : TargetGameObjectAction
    {
        [Tooltip("The waypoints to follow. Can be GameObjects or positions.")]
        [SerializeField] protected SharedVariable<GameObject[]> m_Waypoints;
        [Tooltip("The waypoint positions. Only used if Waypoints array is empty.")]
        [SerializeField] protected SharedVariable<Vector3[]> m_WaypointPositions;
        [Tooltip("The movement speed.")]
        [SerializeField] protected SharedVariable<float> m_MovementSpeed = 5f;
        [Tooltip("The arrival distance for each waypoint.")]
        [SerializeField] protected SharedVariable<float> m_ArrivedDistance = 0.5f;
        [Tooltip("Should the path loop?")]
        [SerializeField] protected SharedVariable<bool> m_LoopPath = false;
        [Tooltip("Should the path reverse when complete?")]
        [SerializeField] protected SharedVariable<bool> m_ReverseOnComplete = false;
        [Tooltip("Should the Transform look at the current waypoint?")]
        [SerializeField] protected SharedVariable<bool> m_LookAtWaypoint = false;
        [Tooltip("The rotation speed when looking at waypoint. Only used if Look At Waypoint is enabled.")]
        [SerializeField] protected SharedVariable<float> m_RotationSpeed = 5f;

        private int m_CurrentWaypointIndex;
        private bool m_Reversing;
        private List<Vector3> m_PathPositions;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_CurrentWaypointIndex = 0;
            m_Reversing = false;
            BuildPathPositions();
        }

        /// <summary>
        /// Builds the list of path positions from waypoints.
        /// </summary>
        private void BuildPathPositions()
        {
            m_PathPositions = new List<Vector3>();

            if (m_Waypoints.Value != null && m_Waypoints.Value.Length > 0) {
                foreach (var waypoint in m_Waypoints.Value) {
                    if (waypoint != null) {
                        m_PathPositions.Add(waypoint.transform.position);
                    }
                }
            } else if (m_WaypointPositions.Value != null && m_WaypointPositions.Value.Length > 0) {
                m_PathPositions.AddRange(m_WaypointPositions.Value);
            }

            if (m_PathPositions.Count == 0) {
                Debug.LogWarning("FollowPath: No waypoints assigned.");
            }
        }

        /// <summary>
        /// Follows the path.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_PathPositions == null || m_PathPositions.Count == 0) {
                return TaskStatus.Running;
            }

            var currentPosition = transform.position;
            var targetPosition = m_PathPositions[m_CurrentWaypointIndex];
            var direction = targetPosition - currentPosition;
            var distance = direction.magnitude;

            // Check if arrived at current waypoint.
            if (distance < m_ArrivedDistance.Value) {
                // Move to next waypoint.
                if (m_Reversing) {
                    m_CurrentWaypointIndex--;
                    if (m_CurrentWaypointIndex < 0) {
                        if (m_LoopPath.Value) {
                            m_CurrentWaypointIndex = m_PathPositions.Count - 1;
                        } else {
                            return TaskStatus.Success;
                        }
                    }
                } else {
                    m_CurrentWaypointIndex++;
                    if (m_CurrentWaypointIndex >= m_PathPositions.Count) {
                        if (m_LoopPath.Value) {
                            m_CurrentWaypointIndex = 0;
                        } else if (m_ReverseOnComplete.Value) {
                            m_Reversing = true;
                            m_CurrentWaypointIndex = m_PathPositions.Count - 2;
                            if (m_CurrentWaypointIndex < 0) {
                                return TaskStatus.Success;
                            }
                        } else {
                            return TaskStatus.Success;
                        }
                    }
                }
                targetPosition = m_PathPositions[m_CurrentWaypointIndex];
                direction = targetPosition - currentPosition;
            }

            // Move towards current waypoint.
            var moveDistance = m_MovementSpeed.Value * Time.deltaTime;
            var newPosition = Vector3.MoveTowards(currentPosition, targetPosition, moveDistance);
            transform.position = newPosition;

            // Optionally look at waypoint.
            if (m_LookAtWaypoint.Value) {
                direction = (targetPosition - transform.position).normalized;
                if (direction != Vector3.zero) {
                    var targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, m_RotationSpeed.Value * Time.deltaTime);
                }
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Waypoints = null;
            m_WaypointPositions = null;
            m_MovementSpeed = 5f;
            m_ArrivedDistance = 0.5f;
            m_LoopPath = false;
            m_ReverseOnComplete = false;
            m_LookAtWaypoint = false;
            m_RotationSpeed = 5f;
        }
    }
}
#endif