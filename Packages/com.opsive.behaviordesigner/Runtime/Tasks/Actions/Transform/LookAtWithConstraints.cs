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
    [Opsive.Shared.Utility.Description("Looks at a target GameObject or position with pitch and yaw angle constraints.")]
    public class LookAtWithConstraints : TargetGameObjectAction
    {
        [Tooltip("The GameObject to look at. If null, uses Target Position.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The target position to look at. Only used if Target is null.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetPosition;
        [Tooltip("The minimum pitch angle (in degrees).")]
        [SerializeField] protected SharedVariable<float> m_MinPitch = -90f;
        [Tooltip("The maximum pitch angle (in degrees).")]
        [SerializeField] protected SharedVariable<float> m_MaxPitch = 90f;
        [Tooltip("The minimum yaw angle (in degrees).")]
        [SerializeField] protected SharedVariable<float> m_MinYaw = float.MinValue;
        [Tooltip("The maximum yaw angle (in degrees).")]
        [SerializeField] protected SharedVariable<float> m_MaxYaw = float.MaxValue;
        [Tooltip("The rotation speed (degrees per second).")]
        [SerializeField] protected SharedVariable<float> m_RotationSpeed = 90f;
        [Tooltip("The smooth damping time. Lower values = faster response.")]
        [SerializeField] protected SharedVariable<float> m_SmoothTime = 0.1f;
        [Tooltip("The up vector for look at rotation.")]
        [SerializeField] protected SharedVariable<Vector3> m_UpVector = Vector3.up;
        [Tooltip("The task will complete when the rotation is within this angle of the constrained target rotation.")]
        [SerializeField] protected SharedVariable<float> m_ArrivedAngle = 1f;

        private float m_CurrentPitch;
        private float m_CurrentYaw;
        private float m_PitchVelocity;
        private float m_YawVelocity;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            var currentEuler = transform.eulerAngles;
            m_CurrentPitch = currentEuler.x;
            m_CurrentYaw = currentEuler.y;

            // Normalize to -180 to 180 range.
            if (m_CurrentPitch > 180f) m_CurrentPitch -= 360f;
            if (m_CurrentYaw > 180f) m_CurrentYaw -= 360f;

            m_PitchVelocity = 0f;
            m_YawVelocity = 0f;
        }

        /// <summary>
        /// Looks at the target with constraints.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var targetPosition = GetTargetPosition();
            var direction = (targetPosition - transform.position).normalized;

            if (direction == Vector3.zero) {
                return TaskStatus.Failure;
            }

            // Calculate desired pitch and yaw.
            var forward = transform.forward;
            var right = Vector3.Cross(m_UpVector.Value, forward).normalized;
            var up = Vector3.Cross(forward, right).normalized;

            var pitch = Mathf.Asin(Vector3.Dot(direction, up)) * Mathf.Rad2Deg;
            var yaw = Mathf.Atan2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward)) * Mathf.Rad2Deg;

            var desiredPitch = Mathf.Clamp(pitch, m_MinPitch.Value, m_MaxPitch.Value);
            var desiredYaw = yaw;
            if (m_MinYaw.Value != float.MinValue || m_MaxYaw.Value != float.MaxValue) {
                desiredYaw = Mathf.Clamp(yaw, m_MinYaw.Value, m_MaxYaw.Value);
            }

            // Smoothly interpolate pitch and yaw.
            m_CurrentPitch = Mathf.SmoothDamp(m_CurrentPitch, desiredPitch, ref m_PitchVelocity, m_SmoothTime.Value, float.MaxValue, Time.deltaTime);
            m_CurrentYaw = Mathf.SmoothDamp(m_CurrentYaw, desiredYaw, ref m_YawVelocity, m_SmoothTime.Value, float.MaxValue, Time.deltaTime);

            // Apply rotation.
            transform.rotation = Quaternion.Euler(m_CurrentPitch, m_CurrentYaw, 0f);
            var targetRotation = Quaternion.Euler(desiredPitch, desiredYaw, 0f);

            if (Quaternion.Angle(transform.rotation, targetRotation) <= m_ArrivedAngle.Value) {
                transform.rotation = targetRotation;
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Returns the target position to look at.
        /// </summary>
        /// <returns>The target position.</returns>
        private Vector3 GetTargetPosition()
        {
            if (m_Target.Value != null) {
                return m_Target.Value.transform.position;
            }
            return m_TargetPosition.Value;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Target = null;
            m_TargetPosition = null;
            m_MinPitch = -90f;
            m_MaxPitch = 90f;
            m_MinYaw = float.MinValue;
            m_MaxYaw = float.MaxValue;
            m_RotationSpeed = 90f;
            m_SmoothTime = 0.1f;
            m_UpVector = Vector3.up;
            m_ArrivedAngle = 1f;
        }
    }
}
#endif