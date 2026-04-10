#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Time
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Time")]
    [Opsive.Shared.Utility.Description("Creates slow motion effect with time scale and duration.")]
    public class SlowMotionEffect : Action
    {
        [Tooltip("The target time scale for slow motion.")]
        [SerializeField] protected SharedVariable<float> m_TargetTimeScale = 0.5f;
        [Tooltip("The duration of the slow motion effect.")]
        [SerializeField] protected SharedVariable<float> m_Duration = 2.0f;
        [Tooltip("The transition duration to reach target time scale.")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.5f;
        [Tooltip("Whether to restore normal time scale after duration.")]
        [SerializeField] protected SharedVariable<bool> m_RestoreAfterDuration = true;

        private float m_StartTimeScale;
        private float m_ElapsedTime;
        private bool m_IsTransitioningIn;
        private bool m_IsActive;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_StartTimeScale = UnityEngine.Time.timeScale;
            m_ElapsedTime = 0.0f;
            m_IsTransitioningIn = true;
            m_IsActive = false;
        }

        /// <summary>
        /// Updates the slow motion effect.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_ElapsedTime += UnityEngine.Time.unscaledDeltaTime;

            if (m_IsTransitioningIn) {
                var progress = Mathf.Clamp01(m_ElapsedTime / m_TransitionDuration.Value);
                UnityEngine.Time.timeScale = Mathf.Lerp(m_StartTimeScale, m_TargetTimeScale.Value, progress);

                if (progress >= 1.0f) {
                    m_IsTransitioningIn = false;
                    m_IsActive = true;
                    m_ElapsedTime = 0.0f;
                }
            } else if (m_IsActive) {
                if (m_RestoreAfterDuration.Value && m_ElapsedTime >= m_Duration.Value) {
                    m_IsActive = false;
                    m_ElapsedTime = 0.0f;
                }
            } else {
                var progress = Mathf.Clamp01(m_ElapsedTime / m_TransitionDuration.Value);
                UnityEngine.Time.timeScale = Mathf.Lerp(m_TargetTimeScale.Value, m_StartTimeScale, progress);

                if (progress >= 1.0f) {
                    return TaskStatus.Success;
                }
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Called when the action ends.
        /// </summary>
        public override void OnEnd()
        {
            base.OnEnd();
            UnityEngine.Time.timeScale = m_StartTimeScale;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_TargetTimeScale = 0.5f;
            m_Duration = 2.0f;
            m_TransitionDuration = 0.5f;
            m_RestoreAfterDuration = true;
        }
    }
}
#endif