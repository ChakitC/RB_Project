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
    [Opsive.Shared.Utility.Description("Moves Rigidbody2D towards target with force application and arrival detection.")]
    public class MoveTowards2D : TargetGameObjectAction
    {
        [Tooltip("The target GameObject. If null, uses Target Position.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The target position. Only used if Target is null.")]
        [SerializeField] protected SharedVariable<Vector2> m_TargetPosition;
        [Tooltip("The movement force.")]
        [SerializeField] protected SharedVariable<float> m_Force = 10.0f;
        [Tooltip("The arrival distance threshold.")]
        [SerializeField] protected SharedVariable<float> m_ArrivedDistance = 0.5f;
        [Tooltip("Whether the Rigidbody2D has arrived.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_HasArrived;

        private Rigidbody2D m_ResolvedRigidbody2D;

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
        }

        /// <summary>
        /// Updates the Rigidbody2D movement.
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

            if (distance < m_ArrivedDistance.Value) {
                m_HasArrived.Value = true;
                return TaskStatus.Success;
            }

            m_HasArrived.Value = false;

            if (distance > 0.01f) {
                var normalizedDirection = direction.normalized;
                m_ResolvedRigidbody2D.AddForce(normalizedDirection * m_Force.Value, ForceMode2D.Force);
            }

            return TaskStatus.Success;
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
            m_Force = 10.0f;
            m_ArrivedDistance = 0.5f;
            m_HasArrived = null;
        }
    }
}
#endif