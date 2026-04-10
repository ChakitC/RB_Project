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
    [Opsive.Shared.Utility.Description("Aligns the Transform to a surface normal using raycasting. Returns Finished when aligned.")]
    public class AlignToSurface : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies which axis to lock during alignment.
        /// </summary>
        public enum LockAxis
        {
            None,   // No axis locked.
            X,      // Lock X axis.
            Y,      // Lock Y axis.
            Z       // Lock Z axis.
        }

        [Tooltip("The raycast direction (typically down for ground alignment).")]
        [SerializeField] protected SharedVariable<Vector3> m_RaycastDirection = Vector3.down;
        [Tooltip("The raycast distance.")]
        [SerializeField] protected SharedVariable<float> m_RaycastDistance = 10f;
        [Tooltip("The layer mask for raycasting.")]
        [SerializeField] protected LayerMask m_LayerMask = -1;
        [Tooltip("The alignment speed (degrees per second).")]
        [SerializeField] protected SharedVariable<float> m_AlignmentSpeed = 90f;
        [Tooltip("The offset from the surface.")]
        [SerializeField] protected SharedVariable<float> m_SurfaceOffset = 0f;
        [Tooltip("The axis to lock during alignment.")]
        [SerializeField] protected LockAxis m_LockAxis = LockAxis.None;
        [Tooltip("The agent has arrived when the angle difference is less than this value (in degrees).")]
        [SerializeField] protected SharedVariable<float> m_ArrivedAngle = 1f;

        private Vector3 m_SurfaceNormal;
        private Vector3 m_SurfacePoint;

        /// <summary>
        /// Aligns to the surface.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var raycastDir = m_RaycastDirection.Value.normalized;
            var ray = new Ray(transform.position, raycastDir);

            if (Physics.Raycast(ray, out RaycastHit hit, m_RaycastDistance.Value, m_LayerMask)) {
                m_SurfaceNormal = hit.normal;
                m_SurfacePoint = hit.point;

                // Calculate target rotation to align with surface normal.
                var targetRotation = Quaternion.FromToRotation(Vector3.up, m_SurfaceNormal);

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

                // Rotate towards target rotation.
                var maxDegreesDelta = m_AlignmentSpeed.Value * Time.deltaTime;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, maxDegreesDelta);

                // Position offset from surface.
                if (m_SurfaceOffset.Value != 0f) {
                    transform.position = m_SurfacePoint + m_SurfaceNormal * m_SurfaceOffset.Value;
                }

                // Check if arrived.
                var angleDifference = Quaternion.Angle(transform.rotation, targetRotation);
                if (angleDifference < m_ArrivedAngle.Value) {
                    transform.rotation = targetRotation;
                    return TaskStatus.Success;
                }
            }

            return TaskStatus.Running;
        }
    }
}
#endif