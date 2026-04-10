#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.CameraTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Camera")]
    [Opsive.Shared.Utility.Description("Looks at target with rotation constraints and smoothing.")]
    public class LookAtWithConstraints : TargetGameObjectAction
    {
        [Tooltip("The target GameObject to look at.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The target position. Only used if Target is null.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetPosition;
        [Tooltip("The smoothing speed.")]
        [SerializeField] protected SharedVariable<float> m_SmoothingSpeed = 5.0f;
        [Tooltip("The minimum X rotation (Euler angles).")]
        [SerializeField] protected SharedVariable<float> m_MinRotationX = -90.0f;
        [Tooltip("The maximum X rotation (Euler angles).")]
        [SerializeField] protected SharedVariable<float> m_MaxRotationX = 90.0f;
        [Tooltip("The minimum Y rotation (Euler angles).")]
        [SerializeField] protected SharedVariable<float> m_MinRotationY = -180.0f;
        [Tooltip("The maximum Y rotation (Euler angles).")]
        [SerializeField] protected SharedVariable<float> m_MaxRotationY = 180.0f;
        [Tooltip("The task will complete when the rotation is within this angle of the target rotation.")]
        [SerializeField] protected SharedVariable<float> m_ArrivedAngle = 1f;

        private Camera m_ResolvedCamera;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedCamera = m_ResolvedGameObject.GetComponent<Camera>();
        }

        /// <summary>
        /// Updates the camera look at.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCamera == null) {
                return TaskStatus.Failure;
            }

            var targetPosition = GetTargetPosition();
            var direction = targetPosition - m_ResolvedCamera.transform.position;

            if (direction.sqrMagnitude < 0.01f) {
                return TaskStatus.Success;
            }

            var targetRotation = Quaternion.LookRotation(direction);
            var eulerAngles = targetRotation.eulerAngles;

            eulerAngles.x = Mathf.Clamp(eulerAngles.x, m_MinRotationX.Value, m_MaxRotationX.Value);
            eulerAngles.y = Mathf.Clamp(eulerAngles.y, m_MinRotationY.Value, m_MaxRotationY.Value);

            targetRotation = Quaternion.Euler(eulerAngles);
            m_ResolvedCamera.transform.rotation = Quaternion.Slerp(m_ResolvedCamera.transform.rotation, targetRotation, m_SmoothingSpeed.Value * Time.deltaTime);

            var arrived = Quaternion.Angle(m_ResolvedCamera.transform.rotation, targetRotation) <= m_ArrivedAngle.Value;
            if (arrived) {
                m_ResolvedCamera.transform.rotation = targetRotation;
            }
            return arrived ? TaskStatus.Success : TaskStatus.Running;
        }

        /// <summary>
        /// Gets the target position.
        /// </summary>
        private Vector3 GetTargetPosition()
        {
            if (m_Target.Value != null) {
                return m_Target.Value.transform.position;
            } else {
                return m_TargetPosition.Value;
            }
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Target = null;
            m_TargetPosition = null;
            m_SmoothingSpeed = 5.0f;
            m_MinRotationX = -90.0f;
            m_MaxRotationX = 90.0f;
            m_MinRotationY = -180.0f;
            m_MaxRotationY = 180.0f;
            m_ArrivedAngle = 1f;
        }
    }
}
#endif