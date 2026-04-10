#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Physics2DTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections.Generic;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Physics2D")]
    [Opsive.Shared.Utility.Description("Follows a path of waypoints using 2D physics-based movement with collision avoidance and smooth rotation. Returns Finished when the path is complete.")]
    public class FollowPath2D : TargetGameObjectAction
    {
        [Tooltip("The waypoints to follow. Can be GameObjects or positions.")]
        [SerializeField] protected SharedVariable<GameObject[]> m_Waypoints;
        [Tooltip("The waypoint positions. Only used if Waypoints array is empty.")]
        [SerializeField] protected SharedVariable<Vector2[]> m_WaypointPositions;
        [Tooltip("The movement force to apply.")]
        [SerializeField] protected SharedVariable<float> m_MovementForce = 10.0f;
        [Tooltip("The arrival distance for each waypoint.")]
        [SerializeField] protected SharedVariable<float> m_ArrivedDistance = 0.5f;
        [Tooltip("Should the path loop?")]
        [SerializeField] protected SharedVariable<bool> m_LoopPath = false;
        [Tooltip("Should the path reverse when complete?")]
        [SerializeField] protected SharedVariable<bool> m_ReverseOnComplete = false;
        [Tooltip("Should the Transform rotate to face the current waypoint?")]
        [SerializeField] protected SharedVariable<bool> m_LookAtWaypoint = false;
        [Tooltip("The rotation speed when looking at waypoint. Only used if Look At Waypoint is enabled.")]
        [SerializeField] protected SharedVariable<float> m_RotationSpeed = 5f;
        [Tooltip("The maximum rotation torque to apply. Only used if Look At Waypoint is enabled.")]
        [SerializeField] protected SharedVariable<float> m_MaxRotationTorque = 10.0f;
        [Tooltip("Should velocity be dampened when approaching waypoint?")]
        [SerializeField] protected SharedVariable<bool> m_DampenOnApproach = true;
        [Tooltip("The distance at which to start dampening velocity.")]
        [SerializeField] protected SharedVariable<float> m_DampenDistance = 2.0f;

        private Rigidbody2D m_ResolvedRigidbody2D;
        private int m_CurrentWaypointIndex;
        private bool m_Reversing;
        private List<Vector2> m_PathPositions;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedRigidbody2D = m_ResolvedGameObject.GetComponent<Rigidbody2D>();
            if (m_ResolvedRigidbody2D == null) {
                Debug.LogWarning("FollowPath2D: Rigidbody2D component not found on GameObject.");
            }
        }

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
            m_PathPositions = new List<Vector2>();

            if (m_Waypoints.Value != null && m_Waypoints.Value.Length > 0) {
                foreach (var waypoint in m_Waypoints.Value) {
                    if (waypoint != null) {
                        var pos = waypoint.transform.position;
                        m_PathPositions.Add(new Vector2(pos.x, pos.y));
                    }
                }
            } else if (m_WaypointPositions.Value != null && m_WaypointPositions.Value.Length > 0) {
                m_PathPositions.AddRange(m_WaypointPositions.Value);
            }

            if (m_PathPositions.Count == 0) {
                Debug.LogWarning("FollowPath2D: No waypoints assigned.");
            }
        }

        /// <summary>
        /// Follows the path using physics-based movement.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody2D == null) {
                return TaskStatus.Success;
            }

            if (m_PathPositions == null || m_PathPositions.Count == 0) {
                return TaskStatus.Running;
            }

            var currentPosition = new Vector2(m_ResolvedRigidbody2D.position.x, m_ResolvedRigidbody2D.position.y);
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
                distance = direction.magnitude;
            }

            // Apply movement force towards waypoint.
            if (distance > 0.01f) {
                var normalizedDirection = direction.normalized;
                var force = m_DampenOnApproach.Value && distance < m_DampenDistance.Value 
                    ? normalizedDirection * m_MovementForce.Value * Mathf.Clamp01(distance / m_DampenDistance.Value)
                    : normalizedDirection * m_MovementForce.Value;

                m_ResolvedRigidbody2D.AddForce(force, ForceMode2D.Force);
            }

            // Optionally rotate to face waypoint.
            if (m_LookAtWaypoint.Value && distance > 0.01f) {
                var targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                var currentAngle = m_ResolvedRigidbody2D.rotation;
                var angleDifference = Mathf.DeltaAngle(currentAngle, targetAngle);
                var torque = Mathf.Clamp(angleDifference * m_RotationSpeed.Value, -m_MaxRotationTorque.Value, m_MaxRotationTorque.Value);
                m_ResolvedRigidbody2D.AddTorque(torque, ForceMode2D.Force);
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
            m_MovementForce = 10.0f;
            m_ArrivedDistance = 0.5f;
            m_LoopPath = false;
            m_ReverseOnComplete = false;
            m_LookAtWaypoint = false;
            m_RotationSpeed = 5f;
            m_MaxRotationTorque = 10.0f;
            m_DampenOnApproach = true;
            m_DampenDistance = 2.0f;
        }
    }
}
#endif