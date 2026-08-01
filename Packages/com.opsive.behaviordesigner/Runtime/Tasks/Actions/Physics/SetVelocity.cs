#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.PhysicsTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Physics")]
    [Opsive.Shared.Utility.Description("Sets Rigidbody velocity with smooth interpolation and optional constraints.")]
    public class SetVelocity : TargetGameObjectAction
    {
        [Tooltip("The target velocity.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetVelocity;
        [Tooltip("The interpolation speed (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_InterpolationSpeed = 0.0f;
        [Tooltip("Whether to apply velocity in local space.")]
        [SerializeField] protected SharedVariable<bool> m_UseLocalSpace = false;

        private UnityEngine.Rigidbody m_ResolvedRigidbody;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedRigidbody = m_ResolvedGameObject.GetComponent<UnityEngine.Rigidbody>();
        }

        /// <summary>
        /// Updates the Rigidbody velocity.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody == null) {
                return TaskStatus.Success;
            }

            var targetVelocity = m_TargetVelocity.Value;

            if (m_UseLocalSpace.Value) {
                targetVelocity = m_ResolvedRigidbody.transform.TransformDirection(targetVelocity);
            }

#if UNITY_6000_0_OR_NEWER
            if (m_InterpolationSpeed.Value > 0.0f) {
                m_ResolvedRigidbody.linearVelocity = Vector3.Lerp(m_ResolvedRigidbody.linearVelocity, targetVelocity, m_InterpolationSpeed.Value * Time.deltaTime);
            } else {
                m_ResolvedRigidbody.linearVelocity = targetVelocity;
            }
#else
            if (m_InterpolationSpeed.Value > 0.0f) {
                m_ResolvedRigidbody.velocity = Vector3.Lerp(m_ResolvedRigidbody.velocity, targetVelocity, m_InterpolationSpeed.Value * Time.deltaTime);
            } else {
                m_ResolvedRigidbody.velocity = targetVelocity;
            }
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
        }
    }
}
#endif