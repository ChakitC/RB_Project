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
    [Opsive.Shared.Utility.Description("Increments or decrements a counter and finishes when the threshold is reached.")]
    public class Counter : Action
    {
        [Tooltip("The counter value that should be modified.")]
        [SerializeField] protected SharedVariable<int> m_Counter;
        [Tooltip("The amount to increment or decrement the counter by each update.")]
        [SerializeField] protected SharedVariable<int> m_IncrementAmount = 1;
        [Tooltip("The threshold value that the counter should reach before finishing.")]
        [SerializeField] protected SharedVariable<int> m_Threshold;
        [Tooltip("Should the counter be incremented (true) or decremented (false)?")]
        [SerializeField] protected SharedVariable<bool> m_Increment = true;
        [Tooltip("Should the counter be reset to 0 when the action starts?")]
        [SerializeField] protected SharedVariable<bool> m_ResetOnStart = false;

        /// <summary>
        /// Called when the action is started.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            if (m_ResetOnStart.Value) {
                m_Counter.Value = 0;
            }
        }

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            // Update the counter.
            if (m_Increment.Value) {
                m_Counter.Value += m_IncrementAmount.Value;
            } else {
                m_Counter.Value -= m_IncrementAmount.Value;
            }

            // Check if threshold is reached.
            if (m_Increment.Value) {
                if (m_Counter.Value >= m_Threshold.Value) {
                    return TaskStatus.Success;
                }
            } else {
                if (m_Counter.Value <= m_Threshold.Value) {
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
            m_Counter = 0;
            m_IncrementAmount = 1;
            m_Threshold = 10;
            m_Increment = true;
            m_ResetOnStart = false;
        }
    }
}
#endif