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
    [Opsive.Shared.Utility.Description("Delays action based on unscaled time (works during pause).")]
    public class TimeBasedDelay : Action
    {
        [Tooltip("The delay duration (seconds).")]
        [SerializeField] protected SharedVariable<float> m_Delay = 1.0f;
        [Tooltip("Whether to use unscaled time.")]
        [SerializeField] protected SharedVariable<bool> m_UseUnscaledTime = true;

        private float m_ElapsedTime;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;
        }

        /// <summary>
        /// Updates the delay.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_ElapsedTime += m_UseUnscaledTime.Value ? UnityEngine.Time.unscaledDeltaTime : UnityEngine.Time.deltaTime;

            if (m_ElapsedTime >= m_Delay.Value) {
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Delay = 1.0f;
            m_UseUnscaledTime = true;
        }
    }
}
#endif