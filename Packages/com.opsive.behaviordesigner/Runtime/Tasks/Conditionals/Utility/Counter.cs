#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.UtilityTasks
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Utility")]
    [Opsive.Shared.Utility.Description("Checks if a counter has reached a threshold. Supports greater than, less than, and equal to comparisons.")]
    public class Counter : Conditional
    {
        [Tooltip("The counter value that should be checked.")]
        [SerializeField] protected SharedVariable<int> m_Counter;
        [Tooltip("The threshold value to compare against.")]
        [SerializeField] protected SharedVariable<int> m_Threshold;
        [Tooltip("The comparison type to use.")]
        [SerializeField] protected ComparisonType m_ComparisonType = ComparisonType.GreaterThanOrEqual;

        /// <summary>
        /// The type of comparison to perform.
        /// </summary>
        public enum ComparisonType
        {
            GreaterThan,
            GreaterThanOrEqual,
            Equal,
            LessThan,
            LessThanOrEqual
        }

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            var counterValue = m_Counter.Value;
            var thresholdValue = m_Threshold.Value;

            switch (m_ComparisonType) {
                case ComparisonType.GreaterThan:
                    return counterValue > thresholdValue ? TaskStatus.Success : TaskStatus.Failure;
                case ComparisonType.GreaterThanOrEqual:
                    return counterValue >= thresholdValue ? TaskStatus.Success : TaskStatus.Failure;
                case ComparisonType.Equal:
                    return counterValue == thresholdValue ? TaskStatus.Success : TaskStatus.Failure;
                case ComparisonType.LessThan:
                    return counterValue < thresholdValue ? TaskStatus.Success : TaskStatus.Failure;
                case ComparisonType.LessThanOrEqual:
                    return counterValue <= thresholdValue ? TaskStatus.Success : TaskStatus.Failure;
                default:
                    return TaskStatus.Failure;
            }
        }
    }
}
#endif