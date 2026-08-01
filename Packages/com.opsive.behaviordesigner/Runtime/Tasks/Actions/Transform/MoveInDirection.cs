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
        private const int c_DataVersion = 1;

        /// <summary>
        /// Specifies how the move direction should be interpreted.
        /// </summary>
        public enum DirectionMode
        {
            Forward,
            WorldDirection,
            LocalDirection
        }

        [Tooltip("How the move direction should be selected.")]
        [SerializeField] protected DirectionMode m_DirectionMode = DirectionMode.Forward;
        [Tooltip("The direction used when Direction Mode is World Direction or Local Direction.")]
        [SerializeField] protected SharedVariable<Vector3> m_Direction;
        [Tooltip("The base movement speed.")]
        [SerializeField] protected SharedVariable<float> m_Speed = 5f;
        [Tooltip("The acceleration rate (units per second squared).")]
        [SerializeField] protected SharedVariable<float> m_Acceleration = 10f;
        [Tooltip("The maximum speed.")]
        [SerializeField] protected SharedVariable<float> m_MaxSpeed = 10f;
        [System.Obsolete("m_UseLocalSpace is obsolete in version 3.1.0. Use m_DirectionMode instead.")]
        [Tooltip("Obsolete in version 3.1.0. Use Direction Mode instead.")]
        [SerializeField] protected SharedVariable<bool> m_UseLocalSpace = false;
        [HideInInspector] [Tooltip("The serialized data version.")]
        [SerializeField] private int m_DataVersion;

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
            MigrateLegacyFields();
            m_CurrentSpeed = 0f;
            m_CurrentVelocity = Vector3.zero;
        }

        /// <summary>
        /// Moves in the specified direction.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            MigrateLegacyFields();
            if (!TryGetDirection(out var direction)) {
                return TaskStatus.Running;
            }

            // Calculate velocity.
            m_CurrentSpeed = Mathf.Min(m_CurrentSpeed + m_Acceleration.Value * Time.deltaTime, m_MaxSpeed.Value);
            var targetSpeed = m_Speed.Value * m_CurrentSpeed / m_MaxSpeed.Value;
            m_CurrentVelocity = direction * targetSpeed;

            // Move.
            if (m_Rigidbody != null) {
#if UNITY_6000_0_OR_NEWER
                m_Rigidbody.linearVelocity = m_CurrentVelocity;
#else
                m_Rigidbody.velocity = m_CurrentVelocity;
#endif
            } else {
                var moveDelta = m_CurrentVelocity * Time.deltaTime;
                transform.position += moveDelta;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Migrates the legacy direction fields to the explicit direction mode.
        /// </summary>
        public void MigrateLegacyFields()
        {
            if (m_DataVersion >= c_DataVersion) {
                return;
            }

#pragma warning disable CS0618
            if (m_Direction != null && (m_Direction.IsShared || m_Direction.Value != Vector3.zero)) {
                m_DirectionMode = m_UseLocalSpace != null && m_UseLocalSpace.Value ? DirectionMode.LocalDirection : DirectionMode.WorldDirection;
            } else {
                m_DirectionMode = DirectionMode.Forward;
            }
#pragma warning restore CS0618

            m_DataVersion = c_DataVersion;
        }

        /// <summary>
        /// Returns the world-space direction to move in.
        /// </summary>
        /// <param name="direction">The world-space direction.</param>
        /// <returns>True if a non-zero direction is available.</returns>
        private bool TryGetDirection(out Vector3 direction)
        {
            switch (m_DirectionMode) {
                case DirectionMode.WorldDirection:
                    direction = m_Direction != null ? m_Direction.Value : Vector3.zero;
                    if (direction == Vector3.zero) {
                        return false;
                    }
                    direction.Normalize();
                    return true;
                case DirectionMode.LocalDirection:
                    var localDirection = m_Direction != null ? m_Direction.Value : Vector3.zero;
                    if (localDirection == Vector3.zero) {
                        direction = Vector3.zero;
                        return false;
                    }
                    direction = transform.TransformDirection(localDirection.normalized);
                    return true;
                default:
                    direction = transform.forward;
                    return true;
            }
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_DirectionMode = DirectionMode.Forward;
            m_Direction = Vector3.zero;
            m_Speed = 5f;
            m_Acceleration = 10f;
            m_MaxSpeed = 10f;
#pragma warning disable CS0618
            m_UseLocalSpace = false;
#pragma warning restore CS0618
            m_DataVersion = c_DataVersion;
        }
    }
}
#endif