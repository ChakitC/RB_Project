#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.RigidbodyTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Rigidbody")]
    [Opsive.Shared.Utility.Description("Sets angular velocity with constraint checking.")]
    public class SetAngularVelocity : TargetGameObjectAction
    {
        [Tooltip("The target angular velocity.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetAngularVelocity;
        [Tooltip("The interpolation speed (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_InterpolationSpeed = 0.0f;
        [Tooltip("Whether to respect rotation constraints.")]
        [SerializeField] protected SharedVariable<bool> m_RespectRotationConstraints = true;

        private UnityEngine.Rigidbody m_ResolvedRigidbody;

        /// <summary>
        /// Called when the state machine is initialized.
        /// </summary>
        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedRigidbody = m_ResolvedGameObject.GetComponent<UnityEngine.Rigidbody>();
        }

        /// <summary>
        /// Updates the angular velocity.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody == null) {
                return TaskStatus.Success;
            }

            var targetAngularVelocity = m_TargetAngularVelocity.Value;

            if (m_RespectRotationConstraints.Value) {
                var constraints = m_ResolvedRigidbody.constraints;
                if ((constraints & RigidbodyConstraints.FreezeRotationX) != 0) {
                    targetAngularVelocity.x = 0.0f;
                }
                if ((constraints & RigidbodyConstraints.FreezeRotationY) != 0) {
                    targetAngularVelocity.y = 0.0f;
                }
                if ((constraints & RigidbodyConstraints.FreezeRotationZ) != 0) {
                    targetAngularVelocity.z = 0.0f;
                }
            }

            if (m_InterpolationSpeed.Value > 0.0f) {
                m_ResolvedRigidbody.angularVelocity = Vector3.Lerp(m_ResolvedRigidbody.angularVelocity, targetAngularVelocity, m_InterpolationSpeed.Value * Time.deltaTime);
            } else {
                m_ResolvedRigidbody.angularVelocity = targetAngularVelocity;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_TargetAngularVelocity = null;
            m_InterpolationSpeed = 0.0f;
            m_RespectRotationConstraints = true;
        }
    }
}
#endif