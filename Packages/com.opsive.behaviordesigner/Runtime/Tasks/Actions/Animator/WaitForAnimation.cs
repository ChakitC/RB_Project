#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AnimatorTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Animator")]
    [Opsive.Shared.Utility.Description("Waits until an animation completes. The action will return running until the animation finishes playing.")]
    public class WaitForAnimation : TargetGameObjectAction
    {
        [Tooltip("The name of the animation state that should be checked.")]
        [SerializeField] protected SharedVariable<string> m_StateName;
        [Tooltip("The layer index of the animation.")]
        [SerializeField] protected SharedVariable<int> m_Layer = 0;
        [Tooltip("The maximum amount of time to wait for the animation to complete. Set to 0 to wait indefinitely.")]
        [SerializeField] protected SharedVariable<float> m_Timeout = 0f;
        [Tooltip("Should the timeout use unscaled time?")]
        [SerializeField] protected SharedVariable<bool> m_UseUnscaledTime = false;

        private Animator m_ResolvedAnimator;
        private float m_StartTime;
        private bool m_WasPlaying;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedAnimator = m_ResolvedGameObject.GetComponent<Animator>();
        }

        /// <summary>
        /// Called when the action is started.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_StartTime = m_UseUnscaledTime.Value ? Time.unscaledTime : Time.time;
            m_WasPlaying = false;
        }

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAnimator == null) {
                return TaskStatus.Success;
            }

            // Check if the animation is playing.
            var animatorStateInfo = m_ResolvedAnimator.GetCurrentAnimatorStateInfo(m_Layer.Value);
            var isPlaying = animatorStateInfo.IsName(m_StateName.Value) && animatorStateInfo.normalizedTime < 1f;

            // If animation was playing but now it's not (or normalizedTime >= 1), it has completed.
            if (m_WasPlaying && (!isPlaying || animatorStateInfo.normalizedTime >= 1f)) {
                return TaskStatus.Success;
            }

            // Track if animation started playing.
            if (isPlaying) {
                m_WasPlaying = true;
            }

            // Check for timeout.
            if (m_Timeout.Value > 0) {
                var currentTime = m_UseUnscaledTime.Value ? Time.unscaledTime : Time.time;
                if (currentTime - m_StartTime >= m_Timeout.Value) {
                    return TaskStatus.Success;
                }
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_StateName = "";
            m_Layer = 0;
            m_Timeout = 0f;
            m_UseUnscaledTime = false;
        }
    }
}
#endif