#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("GameObject")]
    [Opsive.Shared.Utility.Description("Moves a GameObject to a new parent with optional smooth movement. Returns Finished when moved.")]
    public class MoveToParent : Action
    {
        [Tooltip("The GameObject to move.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The new parent GameObject. If null, the GameObject is unparented.")]
        [SerializeField] protected SharedVariable<GameObject> m_NewParent;
        [Tooltip("Should the movement be smooth?")]
        [SerializeField] protected SharedVariable<bool> m_SmoothMovement = false;
        [Tooltip("The movement speed. Only used if Smooth Movement is enabled.")]
        [SerializeField] protected SharedVariable<float> m_MovementSpeed = 5f;
        [Tooltip("Should world position be maintained?")]
        [SerializeField] protected SharedVariable<bool> m_WorldPositionStays = true;

        private Vector3 m_TargetWorldPosition;
        private bool m_HasMoved;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_HasMoved = false;

            if (m_Target.Value != null) {
                m_TargetWorldPosition = m_Target.Value.transform.position;
            }
        }

        /// <summary>
        /// Moves the GameObject to the new parent.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Target.Value == null) {
                return TaskStatus.Success;
            }

            if (m_HasMoved && !m_SmoothMovement.Value) {
                return TaskStatus.Success;
            }

            var newParent = m_NewParent.Value?.transform;

            if (m_SmoothMovement.Value) {
                // Smooth movement to parent position.
                var targetPos = newParent != null ? newParent.position : m_TargetWorldPosition;
                var currentPos = m_Target.Value.transform.position;
                var distance = Vector3.Distance(currentPos, targetPos);

                if (distance > 0.01f) {
                    m_Target.Value.transform.position = Vector3.MoveTowards(currentPos, targetPos, m_MovementSpeed.Value * Time.deltaTime);
                    return TaskStatus.Running;
                }
            }

            // Set parent.
            m_Target.Value.transform.SetParent(newParent, m_WorldPositionStays.Value);
            m_HasMoved = true;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Target = null;
            m_NewParent = null;
            m_SmoothMovement = false;
            m_MovementSpeed = 5f;
            m_WorldPositionStays = true;
        }
    }
}
#endif