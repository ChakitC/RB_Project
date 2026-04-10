#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Utility
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Utility")]
    [Opsive.Shared.Utility.Description("Limits how often an action can execute by enforcing a minimum time between executions.")]
    public class RateLimiter : Action
    {
        [Tooltip("The minimum amount of time (in seconds) that must pass between executions.")]
        [SerializeField] protected SharedVariable<float> m_MinInterval = 1f;
        [Tooltip("Should the interval use unscaled time?")]
        [SerializeField] protected SharedVariable<bool> m_UseUnscaledTime = false;

        private float m_LastExecutionTime = -1f;

        /// <summary>
        /// Called when the action is started.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_LastExecutionTime = -1f;
        }

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var currentTime = m_UseUnscaledTime.Value ? Time.unscaledTime : Time.time;

            // Check if enough time has passed since last execution.
            if (m_LastExecutionTime < 0 || currentTime - m_LastExecutionTime >= m_MinInterval.Value) {
                m_LastExecutionTime = currentTime;
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
            m_MinInterval = 1f;
            m_UseUnscaledTime = false;
        }
    }
}
#endif