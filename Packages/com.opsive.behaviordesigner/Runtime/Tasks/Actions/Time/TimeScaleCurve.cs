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
    [Opsive.Shared.Utility.Description("Changes time scale over time using animation curve.")]
    public class TimeScaleCurve : Action
    {
        [Tooltip("The animation curve for time scale (x = time, y = time scale).")]
        [SerializeField] protected AnimationCurve m_TimeScaleCurve = AnimationCurve.Linear(0, 1, 1, 1);
        [Tooltip("The duration to evaluate the curve over.")]
        [SerializeField] protected SharedVariable<float> m_Duration = 1.0f;
        [Tooltip("The time scale multiplier.")]
        [SerializeField] protected SharedVariable<float> m_TimeScaleMultiplier = 1.0f;

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
        /// Updates the time scale curve.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_ElapsedTime += UnityEngine.Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(m_ElapsedTime / m_Duration.Value);
            var curveValue = m_TimeScaleCurve.Evaluate(progress);
            UnityEngine.Time.timeScale = curveValue * m_TimeScaleMultiplier.Value;

            if (progress >= 1.0f) {
                return TaskStatus.Success;
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
            m_TimeScaleCurve = AnimationCurve.Linear(0, 1, 1, 1);
            m_Duration = 1.0f;
            m_TimeScaleMultiplier = 1.0f;
        }
    }
}
#endif