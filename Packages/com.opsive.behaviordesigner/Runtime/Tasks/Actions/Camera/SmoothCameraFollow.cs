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
    [Opsive.Shared.Utility.Description("Smoothly follows target with offset, damping, and look-ahead.")]
    public class SmoothCameraFollow : TargetGameObjectAction
    {
        [Tooltip("The target GameObject to follow.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The offset from the target position.")]
        [SerializeField] protected SharedVariable<Vector3> m_Offset = Vector3.zero;
        [Tooltip("The damping factor for smooth following (lower = smoother).")]
        [SerializeField] protected SharedVariable<float> m_Damping = 5.0f;
        [Tooltip("Whether to look at the target.")]
        [SerializeField] protected SharedVariable<bool> m_LookAtTarget = true;
        [Tooltip("The look-ahead distance based on target velocity.")]
        [SerializeField] protected SharedVariable<float> m_LookAheadDistance = 0.0f;
        [Tooltip("The task will complete when the camera is within this distance of the desired follow position.")]
        [SerializeField] protected SharedVariable<float> m_PositionArrivedDistance = 0.1f;
        [Tooltip("The task will complete when the camera rotation is within this angle of the desired look rotation.")]
        [SerializeField] protected SharedVariable<float> m_RotationArrivedAngle = 1f;

        private Camera m_ResolvedCamera;
        private Vector3 m_LastTargetPosition;
        private Vector3 m_TargetVelocity;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedCamera = m_ResolvedGameObject.GetComponent<Camera>();
        }

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            if (m_Target.Value != null) {
                m_LastTargetPosition = m_Target.Value.transform.position;
                m_TargetVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Updates the camera follow.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCamera == null) {
                return TaskStatus.Failure;
            }

            if (m_Target.Value == null) {
                return TaskStatus.Failure;
            }

            var targetPosition = m_Target.Value.transform.position;
            m_TargetVelocity = (targetPosition - m_LastTargetPosition) / Time.deltaTime;
            m_LastTargetPosition = targetPosition;

            var lookAhead = m_TargetVelocity.normalized * m_LookAheadDistance.Value;
            var desiredPosition = targetPosition + m_Offset.Value + lookAhead;
            var currentPosition = m_ResolvedCamera.transform.position;
            var newPosition = Vector3.Lerp(currentPosition, desiredPosition, m_Damping.Value * Time.deltaTime);
            m_ResolvedCamera.transform.position = newPosition;
            var positionArrived = Vector3.Distance(m_ResolvedCamera.transform.position, desiredPosition) <= m_PositionArrivedDistance.Value;
            if (positionArrived) {
                m_ResolvedCamera.transform.position = desiredPosition;
            }

            var rotationArrived = true;
            if (m_LookAtTarget.Value) {
                var lookDirection = targetPosition - newPosition;
                if (lookDirection.sqrMagnitude > 0.01f) {
                    var targetRotation = Quaternion.LookRotation(lookDirection);
                    m_ResolvedCamera.transform.rotation = Quaternion.Slerp(m_ResolvedCamera.transform.rotation, targetRotation, m_Damping.Value * Time.deltaTime);
                    rotationArrived = Quaternion.Angle(m_ResolvedCamera.transform.rotation, targetRotation) <= m_RotationArrivedAngle.Value;
                    if (rotationArrived) {
                        m_ResolvedCamera.transform.rotation = targetRotation;
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
            m_Offset = Vector3.zero;
            m_Damping = 5.0f;
            m_LookAtTarget = true;
            m_LookAheadDistance = 0.0f;
            m_PositionArrivedDistance = 0.1f;
            m_RotationArrivedAngle = 1f;
        }
    }
}
#endif