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
    [Opsive.Shared.Utility.Description("Rotates the Transform around a specified axis (X, Y, or Z) based on a float input value. " +
                     "Useful for character rotation (Y axis) or camera panning (X axis).")]
    public class RotateAxis : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the axis that should be rotated.
        /// </summary>
        public enum RotationAxis
        {
            X,  // Rotate around X axis (pitch).
            Y,  // Rotate around Y axis (yaw).
            Z   // Rotate around Z axis (roll).
        }

        [Tooltip("The float input value used for rotation (e.g., Mouse X or Mouse Y delta).")]
        [SerializeField] protected SharedVariable<float> m_RotationInput;
        [Tooltip("The axis to rotate around.")]
        [SerializeField] protected SharedVariable<RotationAxis> m_Axis = RotationAxis.Y;
        [Tooltip("The rotation speed multiplier.")]
        [SerializeField] protected SharedVariable<float> m_RotationSpeed = 1f;
        [Tooltip("Should the rotation be applied relative to the current rotation or set as absolute?")]
        [SerializeField] protected SharedVariable<bool> m_RelativeRotation = true;
        [Tooltip("The minimum rotation angle (in degrees). Only used when Relative Rotation is enabled.")]
        [SerializeField] protected SharedVariable<float> m_MinAngle = float.MinValue;
        [Tooltip("The maximum rotation angle (in degrees). Only used when Relative Rotation is enabled.")]
        [SerializeField] protected SharedVariable<float> m_MaxAngle = float.MaxValue;
        [Tooltip("The task will complete when the rotation is within this angle of the target rotation.")]
        [SerializeField] protected SharedVariable<float> m_ArrivedAngle = 1f;

        private float m_CurrentRotation;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            // Initialize current rotation value from the Transform's current rotation.
            var currentRotation = transform.localEulerAngles;
            switch (m_Axis.Value) {
                case RotationAxis.X:
                    m_CurrentRotation = currentRotation.x;
                    break;
                case RotationAxis.Y:
                    m_CurrentRotation = currentRotation.y;
                    break;
                case RotationAxis.Z:
                    m_CurrentRotation = currentRotation.z;
                    break;
            }

            // Normalize to -180 to 180 range.
            if (m_CurrentRotation > 180f) {
                m_CurrentRotation -= 360f;
            }
        }

        /// <summary>
        /// Rotates the Transform around the specified axis based on the input value.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            Quaternion targetRotation;
            var currentRotation = transform.localRotation;
            m_CurrentRotation += m_RotationInput.Value * m_RotationSpeed.Value * Time.deltaTime;

            // Clamp to the specified range if limits are set.
            if (m_MinAngle.Value != float.MinValue || m_MaxAngle.Value != float.MaxValue) {
                m_CurrentRotation = Mathf.Clamp(m_CurrentRotation, m_MinAngle.Value, m_MaxAngle.Value);
            }

            // Apply the rotation to the Transform.
            var currentEuler = transform.localEulerAngles;
            switch (m_Axis.Value) {
                case RotationAxis.X:
                    targetRotation = Quaternion.Euler(m_CurrentRotation, currentEuler.y, currentEuler.z);
                    break;
                case RotationAxis.Y:
                    targetRotation = Quaternion.Euler(currentEuler.x, m_CurrentRotation, currentEuler.z);
                    break;
                case RotationAxis.Z:
                    targetRotation = Quaternion.Euler(currentEuler.x, currentEuler.y, m_CurrentRotation);
                    break;
                default:
                    targetRotation = transform.localRotation;
                    break;
            }
            transform.localRotation = targetRotation;

            return Quaternion.Angle(currentRotation, targetRotation) <= m_ArrivedAngle.Value ? TaskStatus.Success : TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_RotationInput = null;
            m_Axis = RotationAxis.Y;
            m_RotationSpeed = 1f;
            m_RelativeRotation = true;
            m_MinAngle = float.MinValue;
            m_MaxAngle = float.MaxValue;
            m_ArrivedAngle = 1f;
        }
    }
}
#endif