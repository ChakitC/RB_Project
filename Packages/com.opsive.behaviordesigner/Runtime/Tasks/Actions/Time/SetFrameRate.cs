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
    [Opsive.Shared.Utility.Description("Sets target frame rate with smooth transition.")]
    public class SetFrameRate : Action
    {
        [Tooltip("The target frame rate (-1 = unlimited).")]
        [SerializeField] protected SharedVariable<int> m_TargetFrameRate = 60;
        [Tooltip("The transition duration (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.0f;

        private int m_StartFrameRate;
        private float m_ElapsedTime;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_StartFrameRate = Application.targetFrameRate;
            m_ElapsedTime = 0.0f;
        }

        /// <summary>
        /// Updates the frame rate.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_TransitionDuration.Value > 0.0f) {
                m_ElapsedTime += UnityEngine.Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(m_ElapsedTime / m_TransitionDuration.Value);
                var currentFrameRate = Mathf.RoundToInt(Mathf.Lerp(m_StartFrameRate, m_TargetFrameRate.Value, progress));
                Application.targetFrameRate = currentFrameRate;
            } else {
                Application.targetFrameRate = m_TargetFrameRate.Value;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_TargetFrameRate = 60;
            m_TransitionDuration = 0.0f;
        }
    }
}
#endif