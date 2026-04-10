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
    [Opsive.Shared.Utility.Description("Smoothly rotates the Transform to look at a target GameObject or position. Returns Finished when aligned.")]
    public class SmoothLookAt : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies which axis to lock during rotation.
        /// </summary>
        public enum LockAxis
        {
            None,   // No axis locked.
            X,      // Lock X axis.
            Y,      // Lock Y axis.
            Z       // Lock Z axis.
        }

        [Tooltip("The GameObject to look at. If null, uses Target Position.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The target position to look at. Only used if Target is null.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetPosition;
        [Tooltip("The rotation speed (degrees per second).")]
        [SerializeField] protected SharedVariable<float> m_RotationSpeed = 90f;
        [Tooltip("The up vector for look at rotation.")]
        [SerializeField] protected SharedVariable<Vector3> m_UpVector = Vector3.up;
        [Tooltip("The axis to lock during rotation.")]
        [SerializeField] protected LockAxis m_LockAxis = LockAxis.None;
        [Tooltip("The agent has arrived when the angle difference is less than this value (in degrees).")]
        [SerializeField] protected SharedVariable<float> m_ArrivedAngle = 1f;

        /// <summary>
        /// Smoothly rotates to look at the target.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var targetPosition = GetTargetPosition();
            var direction = (targetPosition - transform.position).normalized;

            if (direction == Vector3.zero) {
                return TaskStatus.Running;
            }

            var targetRotation = Quaternion.LookRotation(direction, m_UpVector.Value);

            // Lock axis if specified.
            if (m_LockAxis != LockAxis.None) {
                var currentEuler = transform.eulerAngles;
                var targetEuler = targetRotation.eulerAngles;
                switch (m_LockAxis) {
                    case LockAxis.X:
                        targetEuler.x = currentEuler.x;
                        break;
                    case LockAxis.Y:
                        targetEuler.y = currentEuler.y;
                        break;
                    case LockAxis.Z:
                        targetEuler.z = currentEuler.z;
                        break;
                }
                targetRotation = Quaternion.Euler(targetEuler);
            }

            // Rotate towards target.
            var maxDegreesDelta = m_RotationSpeed.Value * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, maxDegreesDelta);

            // Check if arrived.
            var angleDifference = Quaternion.Angle(transform.rotation, targetRotation);
            if (angleDifference < m_ArrivedAngle.Value) {
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
    }
}
#endif