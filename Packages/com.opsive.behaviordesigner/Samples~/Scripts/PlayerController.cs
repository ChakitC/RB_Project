#if ENABLE_INPUT_SYSTEM
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Samples
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    /// A small player controller using the Character Collider component.
    /// Uses the Unity Input System for movement and sprint input.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Tooltip("The speed that the character should move.")]
        [SerializeField] protected float m_MoveSpeed = 2f;
        [Tooltip("The index of the state parameter.")]
        [SerializeField] protected int m_StateParameterValue;
        [Tooltip("The multiplier to apply to the speed when the shift key is held down.")]
        [SerializeField] protected float m_ShiftSpeedMultiplier = 1.5f;

        private Transform m_Transform;
        private Animator m_Animator;
        private CharacterController m_CharacterController;

        /// <summary>
        /// Initializes the default value.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_Animator = GetComponent<Animator>();
            m_CharacterController = GetComponent<CharacterController>();

            m_Animator.SetInteger("State", m_StateParameterValue);
        }

        /// <summary>
        /// Returns the horizontal input value (-1 to 1) from keyboard or gamepad.
        /// </summary>
        /// <returns>The horizontal axis value.</returns>
        private float GetHorizontal()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var v = 0f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) v -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) v += 1f;
                if (v != 0f) return v;
            }
            var gamepad = Gamepad.current;
            if (gamepad != null)
                return gamepad.leftStick.x.ReadValue();
            return 0f;
        }

        /// <summary>
        /// Returns the vertical input value (-1 to 1) from keyboard or gamepad.
        /// </summary>
        /// <returns>The vertical axis value.</returns>
        private float GetVertical()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var v = 0f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) v -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) v += 1f;
                if (v != 0f) return v;
            }
            var gamepad = Gamepad.current;
            if (gamepad != null)
                return gamepad.leftStick.y.ReadValue();
            return 0f;
        }

        /// <summary>
        /// Returns whether the sprint (shift) button is held.
        /// </summary>
        /// <returns>True if sprint is held.</returns>
        private bool GetSprint()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.leftShiftKey.isPressed)
                return true;
            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.leftStickButton.isPressed)
                return true;
            return false;
        }

        /// <summary>
        /// Moves the character.
        /// </summary>
        private void Update()
        {
            var direction = new Vector3(GetHorizontal(), 0, GetVertical());
            var multiplier = GetSprint() ? m_ShiftSpeedMultiplier : 1f;
            m_CharacterController.Move(direction * m_MoveSpeed * Time.deltaTime * multiplier);
            m_Transform.LookAt(m_Transform.position + direction);
            m_Animator.speed = multiplier;
            m_Animator.SetFloat("Forward", direction.magnitude);
        }
    }
}
#endif