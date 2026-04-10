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
    [Opsive.Shared.Utility.Description("Moves the character using the Unity CharacterController component based on the input vector. " +
                     "The x component controls horizontal movement and the y component controls forward movement.")]
    public class Move : TargetGameObjectAction
    {
        [Tooltip("The input vector used for movement (x = horizontal, y = vertical).")]
        [SerializeField] protected SharedVariable<Vector2> m_InputVector;
        [Tooltip("The movement speed of the character.")]
        [SerializeField] protected SharedVariable<float> m_Speed = 5f;
        [Tooltip("The gravity applied to the character.")]
        [SerializeField] protected SharedVariable<float> m_Gravity = -9.81f;
        [Tooltip("Should the movement be relative to the character's orientation?")]
        [SerializeField] protected SharedVariable<bool> m_RelativeMovement = true;
        [Tooltip("Should the character jump? Set to true to trigger a jump when grounded.")]
        [SerializeField] protected SharedVariable<bool> m_Jump;
        [Tooltip("The height of the jump.")]
        [SerializeField] protected SharedVariable<float> m_JumpHeight = 1.2f;

        private CharacterController m_CharacterController;
        private float m_VerticalVelocity;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_CharacterController = m_ResolvedGameObject.GetComponent<CharacterController>();
        }

        /// <summary>
        /// Moves the character based on the input vector.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_CharacterController == null) {
                Debug.LogWarning("CharacterControllerMove: No CharacterController component found on the GameObject.");
                return TaskStatus.Success;
            }

            // Calculate the movement direction based on the input vector.
            Vector3 moveDirection;
            if (m_RelativeMovement.Value) {
                moveDirection = transform.right * m_InputVector.Value.x + transform.forward * m_InputVector.Value.y;
            } else {
                moveDirection = new Vector3(m_InputVector.Value.x, 0, m_InputVector.Value.y);
            }
            moveDirection *= m_Speed.Value;

            if (m_CharacterController.isGrounded) {
                // Apply a small downward velocity to keep the character grounded.
                // This ensures isGrounded remains true and handles slopes properly.
                m_VerticalVelocity = -1f;

                // Handle jumping when grounded.
                if (m_Jump.Value) {
                    // Calculate jump velocity based on jump height: v = sqrt(2 * g * h).
                    m_VerticalVelocity = Mathf.Sqrt(m_JumpHeight.Value * -2f * m_Gravity.Value);
                }
            } else {
                m_VerticalVelocity += m_Gravity.Value * Time.deltaTime;
            }
            moveDirection.y = m_VerticalVelocity;

            // Move the character.
            m_CharacterController.Move(moveDirection * Time.deltaTime);

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_InputVector = Vector2.zero;
            m_Speed = 5f;
            m_Gravity = -9.81f;
            m_RelativeMovement = true;
            m_Jump = false;
            m_JumpHeight = 1.2f;
        }
    }
}
#endif