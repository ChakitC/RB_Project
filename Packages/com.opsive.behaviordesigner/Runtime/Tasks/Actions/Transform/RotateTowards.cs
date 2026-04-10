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
    [Opsive.Shared.Utility.Description("Rotates the Transform to face a direction or a GameObject. If a GameObject is supplied, the direction is from this transform towards the target's position. Returns Finished when aligned.")]
    public class RotateTowards : TargetGameObjectAction
    {
        [Tooltip("The GameObject to rotate towards. If set, direction is from this transform towards the target's position. If null, uses Direction.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The direction vector to face. Only used if Target is null.")]
        [SerializeField] protected SharedVariable<Vector3> m_Direction;
        [Tooltip("The rotation speed (degrees per second).")]
        [SerializeField] protected SharedVariable<float> m_RotationSpeed = 90f;
        [Tooltip("The up vector for rotation.")]
        [SerializeField] protected SharedVariable<Vector3> m_UpVector = Vector3.up;
        [Tooltip("The agent has arrived when the angle difference is less than this value (in degrees).")]
        [SerializeField] protected SharedVariable<float> m_ArrivedAngle = 1f;

        /// <summary>
        /// Rotates towards the direction or target.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var direction = GetDirection();
            if (direction == Vector3.zero) {
                return TaskStatus.Running;
            }

            var targetRotation = Quaternion.LookRotation(direction, m_UpVector.Value);

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
        /// Returns the direction to face. From Target position if set, otherwise from Direction.
        /// </summary>
        /// <returns>The normalized direction vector.</returns>
        private Vector3 GetDirection()
        {
            if (m_Target.Value != null) {
                return (m_Target.Value.transform.position - transform.position).normalized;
            }
            return m_Direction.Value.normalized;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Target = null;
            m_Direction = null;
            m_RotationSpeed = 90f;
            m_UpVector = Vector3.up;
            m_ArrivedAngle = 1f;
        }
    }
}
#endif