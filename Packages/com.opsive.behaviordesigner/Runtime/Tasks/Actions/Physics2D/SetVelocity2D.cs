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
    [Opsive.Shared.Utility.Description("Sets Rigidbody2D velocity with smooth interpolation and optional constraints.")]
    public class SetVelocity2D : TargetGameObjectAction
    {
        [Tooltip("The target velocity.")]
        [SerializeField] protected SharedVariable<Vector2> m_TargetVelocity;
        [Tooltip("The interpolation speed (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_InterpolationSpeed = 0.0f;
        [Tooltip("Whether to apply velocity in local space.")]
        [SerializeField] protected SharedVariable<bool> m_UseLocalSpace = false;
        [Tooltip("Whether to preserve X velocity.")]
        [SerializeField] protected SharedVariable<bool> m_PreserveX = false;
        [Tooltip("Whether to preserve Y velocity.")]
        [SerializeField] protected SharedVariable<bool> m_PreserveY = false;

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
        /// Updates the Rigidbody2D velocity.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody2D == null) {
                return TaskStatus.Success;
            }

            Vector2 targetVelocity;
            if (m_UseLocalSpace.Value) {
                var angle = m_ResolvedRigidbody2D.rotation * Mathf.Deg2Rad;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);
                var targetVelocityValue = m_TargetVelocity.Value;
                targetVelocity = new Vector2(targetVelocityValue.x * cos - targetVelocityValue.y * sin, targetVelocityValue.x * sin + targetVelocityValue.y * cos);
            } else {
                targetVelocity = m_TargetVelocity.Value;
            }

#if UNITY_6000_0_OR_NEWER
            var currentVelocity = m_ResolvedRigidbody2D.linearVelocity;
#else
            var currentVelocity = m_ResolvedRigidbody2D.velocity;
#endif
            var newVelocity = m_InterpolationSpeed.Value > 0.0f ? Vector2.Lerp(currentVelocity, targetVelocity, m_InterpolationSpeed.Value * Time.deltaTime) : targetVelocity;

            if (m_PreserveX.Value) {
                newVelocity.x = currentVelocity.x;
            }
            if (m_PreserveY.Value) {
                newVelocity.y = currentVelocity.y;
            }

#if UNITY_6000_0_OR_NEWER
            m_ResolvedRigidbody2D.linearVelocity = newVelocity;
#else
            m_ResolvedRigidbody2D.velocity = newVelocity;
#endif

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_TargetVelocity = null;
            m_InterpolationSpeed = 0.0f;
            m_UseLocalSpace = false;
            m_PreserveX = false;
            m_PreserveY = false;
        }
    }
}
#endif