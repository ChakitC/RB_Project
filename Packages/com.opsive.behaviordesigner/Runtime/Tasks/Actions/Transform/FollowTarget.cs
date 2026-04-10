#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Transform")]
    [Opsive.Shared.Utility.Description("Follows a target GameObject with configurable distance, offset, and smoothing. Can optionally look at the target.")]
    public class FollowTarget : TargetGameObjectAction
    {
        [Tooltip("The GameObject to follow.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The desired distance to maintain from the target. Set to 0 to follow directly.")]
        [SerializeField] protected SharedVariable<float> m_FollowDistance = 0f;
        [Tooltip("The offset from the target position.")]
        [SerializeField] protected SharedVariable<Vector3> m_Offset = Vector3.zero;
        [Tooltip("The follow speed.")]
        [SerializeField] protected SharedVariable<float> m_FollowSpeed = 5f;
        [Tooltip("The smooth damping time. Lower values = faster response.")]
        [SerializeField] protected SharedVariable<float> m_SmoothTime = 0.3f;
        [Tooltip("The height offset from the target.")]
        [SerializeField] protected SharedVariable<float> m_HeightOffset = 0f;
        [Tooltip("Should the Transform look at the target?")]
        [SerializeField] protected SharedVariable<bool> m_LookAtTarget = false;
        [Tooltip("The rotation speed when looking at target. Only used if Look At Target is enabled.")]
        [SerializeField] protected SharedVariable<float> m_RotationSpeed = 5f;
        [Tooltip("The up vector for look at rotation. Only used if Look At Target is enabled.")]
        [SerializeField] protected SharedVariable<Vector3> m_UpVector = Vector3.up;
        [Tooltip("The task will complete when the position is within this distance of the desired follow position.")]
        [SerializeField] protected SharedVariable<float> m_PositionArrivedDistance = 0.1f;
        [Tooltip("The task will complete when the rotation is within this angle of the desired look rotation.")]
        [SerializeField] protected SharedVariable<float> m_RotationArrivedAngle = 1f;

        private Vector3 m_CurrentVelocity;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_CurrentVelocity = Vector3.zero;
        }

        /// <summary>
        /// Follows the target.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Target.Value == null) {
                return TaskStatus.Failure;
            }

            var targetPosition = m_Target.Value.transform.position;
            var offset = m_Offset.Value;
            offset.y += m_HeightOffset.Value;

            // Calculate desired position.
            Vector3 desiredPosition;
            if (m_FollowDistance.Value > 0f) {
                var direction = (transform.position - targetPosition).normalized;
                desiredPosition = targetPosition + direction * m_FollowDistance.Value + offset;
            } else {
                desiredPosition = targetPosition + offset;
            }

            // Smoothly move towards desired position.
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref m_CurrentVelocity, m_SmoothTime.Value, m_FollowSpeed.Value, Time.deltaTime);
            var positionArrived = Vector3.Distance(transform.position, desiredPosition) <= m_PositionArrivedDistance.Value;
            if (positionArrived) {
                transform.position = desiredPosition;
            }

            // Optionally look at target.
            var rotationArrived = true;
            if (m_LookAtTarget.Value) {
                var targetPos = m_Target.Value.transform.position;
                var direction = (targetPos - transform.position).normalized;
                if (direction != Vector3.zero) {
                    var targetRotation = Quaternion.LookRotation(direction, m_UpVector.Value);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, m_RotationSpeed.Value * Time.deltaTime);
                    rotationArrived = Quaternion.Angle(transform.rotation, targetRotation) <= m_RotationArrivedAngle.Value;
                    if (rotationArrived) {
                        transform.rotation = targetRotation;
                    }
                }
            }

            return positionArrived && rotationArrived ? TaskStatus.Success : TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Target = null;
            m_FollowDistance = 0f;
            m_Offset = Vector3.zero;
            m_FollowSpeed = 5f;
            m_SmoothTime = 0.3f;
            m_HeightOffset = 0f;
            m_LookAtTarget = false;
            m_RotationSpeed = 5f;
            m_UpVector = Vector3.up;
            m_PositionArrivedDistance = 0.1f;
            m_RotationArrivedAngle = 1f;
        }
    }
}
#endif