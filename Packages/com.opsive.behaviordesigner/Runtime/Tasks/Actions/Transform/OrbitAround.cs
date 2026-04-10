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
    [Opsive.Shared.Utility.Description("Orbits the Transform around a target GameObject or position at a specified radius and speed.")]
    public class OrbitAround : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the orbit axis.
        /// </summary>
        public enum OrbitAxis
        {
            X,          // Orbit around X axis.
            Y,          // Orbit around Y axis.
            Z,          // Orbit around Z axis.
            Custom      // Use custom axis.
        }

        [Tooltip("The GameObject to orbit around. If null, uses Target Position.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The target position to orbit around. Only used if Target is null.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetPosition;
        [Tooltip("The orbit radius.")]
        [SerializeField] protected SharedVariable<float> m_Radius = 5f;
        [Tooltip("The orbit speed (degrees per second).")]
        [SerializeField] protected SharedVariable<float> m_OrbitSpeed = 30f;
        [Tooltip("The orbit axis.")]
        [SerializeField] protected OrbitAxis m_OrbitAxis = OrbitAxis.Y;
        [Tooltip("The custom orbit axis. Only used if Orbit Axis is Custom.")]
        [SerializeField] protected SharedVariable<Vector3> m_CustomAxis = Vector3.up;
        [Tooltip("The initial angle offset (in degrees).")]
        [SerializeField] protected SharedVariable<float> m_InitialAngle = 0f;
        [Tooltip("Should the orbit be clockwise?")]
        [SerializeField] protected SharedVariable<bool> m_Clockwise = true;
        [Tooltip("Should the Transform look at the center?")]
        [SerializeField] protected SharedVariable<bool> m_LookAtCenter = false;
        [Tooltip("The up vector for look at rotation. Only used if Look At Center is enabled.")]
        [SerializeField] protected SharedVariable<Vector3> m_UpVector = Vector3.up;

        private float m_CurrentAngle;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_CurrentAngle = m_InitialAngle.Value;
        }

        /// <summary>
        /// Orbits around the target.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var centerPosition = GetCenterPosition();
            var direction = m_Clockwise.Value ? -1f : 1f;

            m_CurrentAngle += m_OrbitSpeed.Value * direction * Time.deltaTime;

            // Calculate orbit axis.
            Vector3 axis;
            switch (m_OrbitAxis) {
                case OrbitAxis.X:
                    axis = Vector3.right;
                    break;
                case OrbitAxis.Y:
                    axis = Vector3.up;
                    break;
                case OrbitAxis.Z:
                    axis = Vector3.forward;
                    break;
                case OrbitAxis.Custom:
                    axis = m_CustomAxis.Value.normalized;
                    break;
                default:
                    axis = Vector3.up;
                    break;
            }

            // Calculate orbit position.
            var radians = m_CurrentAngle * Mathf.Deg2Rad;
            var perpendicular = Vector3.Cross(axis, Vector3.forward);
            if (perpendicular == Vector3.zero) {
                perpendicular = Vector3.Cross(axis, Vector3.right);
            }
            perpendicular = perpendicular.normalized;
            var orbitPosition = centerPosition + (perpendicular * Mathf.Cos(radians) + Vector3.Cross(axis, perpendicular) * Mathf.Sin(radians)) * m_Radius.Value;

            transform.position = orbitPosition;

            // Optionally look at center.
            if (m_LookAtCenter.Value) {
                var directionToCenter = (centerPosition - transform.position).normalized;
                if (directionToCenter != Vector3.zero) {
                    var targetRotation = Quaternion.LookRotation(directionToCenter, m_UpVector.Value);
                    transform.rotation = targetRotation;
                }
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Returns the center position to orbit around.
        /// </summary>
        /// <returns>The center position.</returns>
        private Vector3 GetCenterPosition()
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
            m_Radius = 5f;
            m_OrbitSpeed = 30f;
            m_OrbitAxis = OrbitAxis.Y;
            m_CustomAxis = Vector3.up;
            m_InitialAngle = 0f;
            m_Clockwise = true;
            m_LookAtCenter = false;
            m_UpVector = Vector3.up;
        }
    }
}
#endif