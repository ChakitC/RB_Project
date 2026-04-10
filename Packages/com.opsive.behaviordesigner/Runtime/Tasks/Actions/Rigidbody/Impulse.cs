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
    [Opsive.Shared.Utility.Description("Applies impulse with direction, force, and optional relative space.")]
    public class Impulse : TargetGameObjectAction
    {
        [Tooltip("The impulse direction.")]
        [SerializeField] protected SharedVariable<Vector3> m_Direction = Vector3.forward;
        [Tooltip("The impulse force magnitude.")]
        [SerializeField] protected SharedVariable<float> m_Force = 10.0f;
        [Tooltip("Whether to apply impulse in local space.")]
        [SerializeField] protected SharedVariable<bool> m_UseLocalSpace = false;
        [Tooltip("Whether the impulse has been applied.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_ImpulseApplied;

        private UnityEngine.Rigidbody m_ResolvedRigidbody;
        private bool m_HasApplied;

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
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_HasApplied = false;
            m_ImpulseApplied.Value = false;
        }

        /// <summary>
        /// Updates the impulse application.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody == null) {
                return TaskStatus.Success;
            }

            if (!m_HasApplied) {
                var direction = m_Direction.Value;
                if (m_UseLocalSpace.Value) {
                    direction = m_ResolvedRigidbody.transform.TransformDirection(direction);
                }

                m_ResolvedRigidbody.AddForce(direction.normalized * m_Force.Value, ForceMode.Impulse);
                m_HasApplied = true;
                m_ImpulseApplied.Value = true;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Direction = Vector3.forward;
            m_Force = 10.0f;
            m_UseLocalSpace = false;
            m_ImpulseApplied = null;
        }
    }
}
#endif