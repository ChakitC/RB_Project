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
    [Opsive.Shared.Utility.Description("Rotates the Transform based on a Vector2 input. The x component controls yaw (horizontal rotation) and the y component controls pitch (vertical rotation).")]
    public class Rotate : TargetGameObjectAction
    {
        [Tooltip("The input vector used for rotation (x = yaw, y = pitch).")]
        [SerializeField] protected SharedVariable<Vector2> m_RotationInput;
        [Tooltip("The rotation speed multiplier.")]
        [SerializeField] protected SharedVariable<float> m_RotationSpeed = 1f;
        [Tooltip("Should the rotation be applied relative to the current rotation or set as absolute?")]
        [SerializeField] protected SharedVariable<bool> m_RelativeRotation = true;
        [Tooltip("The minimum pitch angle (in degrees).")]
        [SerializeField] protected SharedVariable<float> m_MinPitch = -90f;
        [Tooltip("The maximum pitch angle (in degrees).")]
        [SerializeField] protected SharedVariable<float> m_MaxPitch = 90f;
        [Tooltip("The task will complete when the rotation is within this angle of the target rotation.")]
        [SerializeField] protected SharedVariable<float> m_ArrivedAngle = 1f;

        private float m_CurrentPitch;
        private float m_CurrentYaw;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();

            // Initialize current rotation values from the Transform's current rotation.
            var currentRotation = transform.localEulerAngles;
            m_CurrentYaw = currentRotation.y;
            m_CurrentPitch = currentRotation.x;

            // Normalize pitch to -180 to 180 range.
            if (m_CurrentPitch > 180f) {
                m_CurrentPitch -= 360f;
            }
        }

        /// <summary>
        /// Rotates the Transform based on the input vector.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_RelativeRotation.Value) {
                m_CurrentYaw += m_RotationInput.Value.x * m_RotationSpeed.Value * Time.deltaTime;
                m_CurrentPitch -= m_RotationInput.Value.y * m_RotationSpeed.Value * Time.deltaTime;
            } else { // Set absolute rotation.
                m_CurrentYaw = m_RotationInput.Value.x * m_RotationSpeed.Value;
                m_CurrentPitch = m_RotationInput.Value.y * m_RotationSpeed.Value;
            }

            m_CurrentPitch = Mathf.Clamp(m_CurrentPitch, m_MinPitch.Value, m_MaxPitch.Value);
            var currentRotation = transform.localRotation;
            var targetRotation = Quaternion.Euler(m_CurrentPitch, m_CurrentYaw, 0f);
            transform.localRotation = targetRotation;

            return Quaternion.Angle(currentRotation, targetRotation) <= m_ArrivedAngle.Value ? TaskStatus.Success : TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_RotationInput = Vector2.zero;
            m_RotationSpeed = 1f;
            m_RelativeRotation = true;
            m_MinPitch = -90f;
            m_MaxPitch = 90f;
            m_ArrivedAngle = 1f;
            m_CurrentPitch = 0f;
            m_CurrentYaw = 0f;
        }
    }
}
#endif