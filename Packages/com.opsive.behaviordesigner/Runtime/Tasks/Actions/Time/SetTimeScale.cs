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
    [Opsive.Shared.Utility.Description("Sets time scale with smooth transition and duration.")]
    public class SetTimeScale : Action
    {
        public enum EasingType
        {
            Linear,
            EaseIn,
            EaseOut,
            EaseInOut
        }

        [Tooltip("The target time scale.")]
        [SerializeField] protected SharedVariable<float> m_TargetTimeScale = 1.0f;
        [Tooltip("The transition duration (seconds).")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 1.0f;
        [Tooltip("The easing curve type.")]
        [SerializeField] protected EasingType m_EasingType = EasingType.Linear;

        private float m_StartTimeScale;
        private float m_ElapsedTime;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_StartTimeScale = UnityEngine.Time.timeScale;
            m_ElapsedTime = 0.0f;
        }

        /// <summary>
        /// Updates the time scale.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_ElapsedTime += UnityEngine.Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(m_ElapsedTime / m_TransitionDuration.Value);
            var easedProgress = ApplyEasing(progress);

            UnityEngine.Time.timeScale = Mathf.Lerp(m_StartTimeScale, m_TargetTimeScale.Value, easedProgress);

            if (progress >= 1.0f) {
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Applies easing to the value.
        /// </summary>
        private float ApplyEasing(float t)
        {
            switch (m_EasingType) {
                case EasingType.EaseIn:
                    return t * t;
                case EasingType.EaseOut:
                    return 1.0f - (1.0f - t) * (1.0f - t);
                case EasingType.EaseInOut:
                    return t < 0.5f ? 2.0f * t * t : 1.0f - Mathf.Pow(-2.0f * t + 2.0f, 2.0f) / 2.0f;
                default:
                    return t;
            }
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_TargetTimeScale = 1.0f;
            m_TransitionDuration = 1.0f;
            m_EasingType = EasingType.Linear;
        }
    }
}
#endif