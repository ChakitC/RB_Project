#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Physics2DTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Physics2D")]
    [Opsive.Shared.Utility.Description("Moves towards a target using physics forces with acceleration and deceleration curves. Returns Finished when the target is reached.")]
    public class PhysicsBasedMovement2D : TargetGameObjectAction
    {
        [Tooltip("The target GameObject. If null, uses Target Position.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The target position. Only used if Target is null.")]
        [SerializeField] protected SharedVariable<Vector2> m_TargetPosition;
        [Tooltip("The maximum force to apply.")]
        [SerializeField] protected SharedVariable<float> m_MaxForce = 10.0f;
        [Tooltip("The arrival distance threshold.")]
        [SerializeField] protected SharedVariable<float> m_ArrivedDistance = 0.5f;
        [Tooltip("The acceleration curve. X-axis is distance (0-1), Y-axis is force multiplier (0-1).")]
        [SerializeField] protected AnimationCurve m_AccelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("The deceleration curve. X-axis is distance (0-1), Y-axis is force multiplier (0-1).")]
        [SerializeField] protected AnimationCurve m_DecelerationCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [Tooltip("The distance at which to switch from acceleration to deceleration.")]
        [SerializeField] protected SharedVariable<float> m_DecelerationStartDistance = 5.0f;
        [Tooltip("The maximum distance for curve evaluation.")]
        [SerializeField] protected SharedVariable<float> m_MaxDistance = 10.0f;
        [Tooltip("Whether the Rigidbody has arrived.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_HasArrived;
        [Tooltip("The current distance to target.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_CurrentDistance;

        private Rigidbody2D m_ResolvedRigidbody2D;
        private float m_InitialDistance;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedRigidbody2D = m_ResolvedGameObject.GetComponent<Rigidbody2D>();
            if (m_ResolvedRigidbody2D == null) {
                Debug.LogWarning("PhysicsBasedMovement2D: Rigidbody2D component not found on GameObject.");
            }
        }

        public override void OnStart()
        {
            base.OnStart();
            m_HasArrived.Value = false;
            UpdateInitialDistance();
        }

        /// <summary>
        /// Updates the initial distance to target.
        /// </summary>
        private void UpdateInitialDistance()
        {
            var targetPos = GetTargetPosition();
            var currentPos = new Vector2(m_ResolvedRigidbody2D.position.x, m_ResolvedRigidbody2D.position.y);
            m_InitialDistance = Vector2.Distance(currentPos, targetPos);
        }

        /// <summary>
        /// Updates the physics-based movement.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody2D == null) {
                return TaskStatus.Success;
            }

            var targetPosition = GetTargetPosition();
            var currentPosition = new Vector2(m_ResolvedRigidbody2D.position.x, m_ResolvedRigidbody2D.position.y);
            var direction = targetPosition - currentPosition;
            var distance = direction.magnitude;

            m_CurrentDistance.Value = distance;

            if (distance < m_ArrivedDistance.Value) {
                m_HasArrived.Value = true;
                return TaskStatus.Success;
            }

            m_HasArrived.Value = false;

            if (distance > 0.01f) {
                var normalizedDirection = direction.normalized;
                var forceMultiplier = distance > m_DecelerationStartDistance.Value
                    ? m_AccelerationCurve.Evaluate(Mathf.Clamp01((m_InitialDistance - distance) / Mathf.Max(m_InitialDistance - m_DecelerationStartDistance.Value, 0.01f)))
                    : m_DecelerationCurve.Evaluate(Mathf.Clamp01(distance / m_DecelerationStartDistance.Value));

                m_ResolvedRigidbody2D.AddForce(normalizedDirection * m_MaxForce.Value * forceMultiplier, ForceMode2D.Force);
            }

            // Update initial distance if target moved significantly.
            var newInitialDistance = Vector2.Distance(currentPosition, targetPosition);
            if (Mathf.Abs(newInitialDistance - m_InitialDistance) > m_MaxDistance.Value * 0.5f) {
                m_InitialDistance = newInitialDistance;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Gets the target position.
        /// </summary>
        private Vector2 GetTargetPosition()
        {
            if (m_Target.Value != null) {
                var pos = m_Target.Value.transform.position;
                return new Vector2(pos.x, pos.y);
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
            m_TargetPosition = Vector2.zero;
            m_MaxForce = 10.0f;
            m_ArrivedDistance = 0.5f;
            m_AccelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            m_DecelerationCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            m_DecelerationStartDistance = 5.0f;
            m_MaxDistance = 10.0f;
            m_HasArrived = null;
            m_CurrentDistance = null;
        }
    }
}
#endif