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
    [Opsive.Shared.Utility.Description("Enables/disables CharacterController with optional smooth property transition.")]
    public class EnableDisable : TargetGameObjectAction
    {
        [Tooltip("Whether to enable the CharacterController.")]
        [SerializeField] protected SharedVariable<bool> m_Enable = true;
        [Tooltip("Whether to smoothly transition properties when enabling/disabling.")]
        [SerializeField] protected SharedVariable<bool> m_SmoothTransition = false;
        [Tooltip("The disabled height (when smooth transition is enabled).")]
        [SerializeField] protected SharedVariable<float> m_DisabledHeight = 0.1f;
        [Tooltip("The transition duration (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.5f;

        private CharacterController m_ResolvedCharacterController;
        private float m_OriginalHeight;
        private float m_ElapsedTime;
        private bool m_IsTransitioning;

        /// <summary>
        /// Called when the state machine is initialized.
        /// </summary>
        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedCharacterController = m_ResolvedGameObject.GetComponent<CharacterController>();
        }

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;
            m_IsTransitioning = false;
            if (m_ResolvedCharacterController != null) {
                m_OriginalHeight = m_ResolvedCharacterController.height;
            }
        }

        /// <summary>
        /// Updates the enable/disable state.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCharacterController == null) {
                return TaskStatus.Success;
            }

            if (m_SmoothTransition.Value && m_TransitionDuration.Value > 0.0f) {
                if (!m_IsTransitioning) {
                    m_IsTransitioning = true;
                    m_ElapsedTime = 0.0f;
                }

                m_ElapsedTime += Time.deltaTime;
                var progress = Mathf.Clamp01(m_ElapsedTime / m_TransitionDuration.Value);

                if (m_Enable.Value) {
                    m_ResolvedCharacterController.height = Mathf.Lerp(m_DisabledHeight.Value, m_OriginalHeight, progress);
                    if (progress >= 1.0f) {
                        m_ResolvedCharacterController.enabled = true;
                        m_IsTransitioning = false;
                    }
                } else {
                    m_ResolvedCharacterController.height = Mathf.Lerp(m_OriginalHeight, m_DisabledHeight.Value, progress);
                    if (progress >= 1.0f) {
                        m_ResolvedCharacterController.enabled = false;
                        m_IsTransitioning = false;
                    }
                }
            } else {
                m_ResolvedCharacterController.enabled = m_Enable.Value;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Enable = true;
            m_SmoothTransition = false;
            m_DisabledHeight = 0.1f;
            m_TransitionDuration = 0.5f;
        }
    }
}
#endif