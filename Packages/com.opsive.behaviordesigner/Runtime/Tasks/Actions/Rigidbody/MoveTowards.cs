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
    [Opsive.Shared.Utility.Description("Moves Rigidbody towards target with force application and arrival detection.")]
    public class MoveTowards : TargetGameObjectAction
    {
        [Tooltip("The target GameObject. If null, uses Target Position.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The target position. Only used if Target is null.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetPosition;
        [Tooltip("The movement force.")]
        [SerializeField] protected SharedVariable<float> m_Force = 10.0f;
        [Tooltip("The arrival distance threshold.")]
        [SerializeField] protected SharedVariable<float> m_ArrivedDistance = 0.5f;
        [Tooltip("Whether the Rigidbody has arrived.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_HasArrived;

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
        /// Updates the Rigidbody movement.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody == null) {
                return TaskStatus.Success;
            }

            var targetPosition = GetTargetPosition();
            var direction = targetPosition - m_ResolvedRigidbody.position;
            var distance = direction.magnitude;

            if (distance < m_ArrivedDistance.Value) {
                m_HasArrived.Value = true;
                return TaskStatus.Success;
            }

            m_HasArrived.Value = false;
            var normalizedDirection = direction.normalized;
            m_ResolvedRigidbody.AddForce(normalizedDirection * m_Force.Value, ForceMode.Force);

            return TaskStatus.Success;
        }

        /// <summary>
        /// Gets the target position.
        /// </summary>
        private Vector3 GetTargetPosition()
        {
            if (m_Target.Value != null) {
                return m_Target.Value.transform.position;
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
            m_TargetPosition = null;
            m_Force = 10.0f;
            m_ArrivedDistance = 0.5f;
            m_HasArrived = null;
        }
    }
}
#endif