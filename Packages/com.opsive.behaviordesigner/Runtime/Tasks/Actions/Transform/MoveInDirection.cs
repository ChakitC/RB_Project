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
    [Opsive.Shared.Utility.Description("Moves the Transform in a direction with acceleration.")]
    public class MoveInDirection : TargetGameObjectAction
    {
        [Tooltip("The direction to move in. If this value is 0 then the forward Transform direction is used.")]
        [SerializeField] protected SharedVariable<Vector3> m_Direction;
        [Tooltip("The base movement speed.")]
        [SerializeField] protected SharedVariable<float> m_Speed = 5f;
        [Tooltip("The acceleration rate (units per second squared).")]
        [SerializeField] protected SharedVariable<float> m_Acceleration = 10f;
        [Tooltip("The maximum speed.")]
        [SerializeField] protected SharedVariable<float> m_MaxSpeed = 10f;
        [Tooltip("Should the movement be relative to the Transform's local space?")]
        [SerializeField] protected SharedVariable<bool> m_UseLocalSpace = false;

        private float m_CurrentSpeed;
        private Vector3 m_CurrentVelocity;
        private Rigidbody m_Rigidbody;

        /// <summary>
        /// Initializes the target GameObject and caches the Rigidbody reference.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();
            m_Rigidbody = m_ResolvedGameObject != null ? m_ResolvedGameObject.GetComponent<Rigidbody>() : null;
        }

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_CurrentSpeed = 0f;
            m_CurrentVelocity = Vector3.zero;
        }

        /// <summary>
        /// Moves in the specified direction.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            Vector3 direction;
            if (m_Direction.Value != Vector3.zero) {
                direction = m_Direction.Value.normalized;
            } else {
                if (m_UseLocalSpace.Value) {
                    direction = transform.localRotation * Vector3.forward;
                } else {
                    direction = transform.forward;
                }
            }

            // Calculate velocity.
            m_CurrentSpeed = Mathf.Min(m_CurrentSpeed + m_Acceleration.Value * Time.deltaTime, m_MaxSpeed.Value);
            var targetSpeed = m_Speed.Value * m_CurrentSpeed / m_MaxSpeed.Value;
            m_CurrentVelocity = direction * targetSpeed;

            // Move.
            if (m_Rigidbody != null) {
                var worldVelocity = m_UseLocalSpace.Value ? m_Rigidbody.rotation * m_CurrentVelocity : m_CurrentVelocity;
#if UNITY_6000_3_OR_NEWER
                m_Rigidbody.linearVelocity = worldVelocity;
#else
                m_Rigidbody.linearVelocity = worldVelocity;
#endif
            } else {
                var moveDelta = m_CurrentVelocity * Time.deltaTime;
                if (m_UseLocalSpace.Value) {
                    transform.localPosition += moveDelta;
                } else {
                    transform.position += moveDelta;
                }
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Direction = Vector3.zero;
            m_Speed = 5f;
            m_Acceleration = 10f;
            m_MaxSpeed = 10f;
            m_UseLocalSpace = false;
        }
    }
}
#endif