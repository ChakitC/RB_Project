#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.CharacterControllerTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Character Controller")]
    [Opsive.Shared.Utility.Description("Gets CharacterController state (grounded, velocity, bounds, properties) with validation.")]
    public class GetState : TargetGameObjectAction
    {
        [Tooltip("Whether the character is grounded (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_IsGrounded;
        [Tooltip("The current velocity (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_Velocity;
        [Tooltip("The current height (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Height;
        [Tooltip("The current radius (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Radius;
        [Tooltip("The current center (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_Center;
        [Tooltip("The bounds center (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_BoundsCenter;
        [Tooltip("The bounds extents (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_BoundsExtents;

        private CharacterController m_ResolvedCharacterController;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedCharacterController = m_ResolvedGameObject.GetComponent<CharacterController>();
        }

        /// <summary>
        /// Updates the CharacterController state retrieval.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCharacterController == null) {
                return TaskStatus.Success;
            }

            m_IsGrounded.Value = m_ResolvedCharacterController.isGrounded;
            m_Velocity.Value = m_ResolvedCharacterController.velocity;
            m_Height.Value = m_ResolvedCharacterController.height;
            m_Radius.Value = m_ResolvedCharacterController.radius;
            m_Center.Value = m_ResolvedCharacterController.center;

            var bounds = m_ResolvedCharacterController.bounds;
            m_BoundsCenter.Value = bounds.center;
            m_BoundsExtents.Value = bounds.extents;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_IsGrounded = null;
            m_Velocity = null;
            m_Height = null;
            m_Radius = null;
            m_Center = null;
            m_BoundsCenter = null;
            m_BoundsExtents = null;
        }
    }
}
#endif