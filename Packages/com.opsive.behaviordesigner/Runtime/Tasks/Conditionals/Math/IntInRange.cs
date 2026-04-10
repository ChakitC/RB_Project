#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.Math
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Math")]
    [Opsive.Shared.Utility.Description("Checks if an integer value is within a min/max range.")]
    public class IntInRange : Conditional
    {
        /// <summary>
        /// Specifies the type of range check that should be performed.
        /// </summary>
        public enum RangeType
        {
            Inclusive,     // Both min and max are inclusive.
            Exclusive,     // Both min and max are exclusive.
            MinInclusive,  // Min is inclusive, max is exclusive.
            MaxInclusive   // Min is exclusive, max is inclusive.
        }

        [Tooltip("The integer value to check.")]
        [SerializeField] protected SharedVariable<int> m_Value;
        [Tooltip("The minimum value of the range.")]
        [SerializeField] protected SharedVariable<int> m_Min;
        [Tooltip("The maximum value of the range.")]
        [SerializeField] protected SharedVariable<int> m_Max;
        [Tooltip("The type of range check to perform.")]
        [SerializeField] protected RangeType m_RangeType = RangeType.Inclusive;

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            var value = m_Value.Value;
            var min = m_Min.Value;
            var max = m_Max.Value;

            bool valid;
            switch (m_RangeType) {
                case RangeType.Inclusive:
                    valid = value >= min && value <= max;
                    break;
                case RangeType.Exclusive:
                    valid = value > min && value < max;
                    break;
                case RangeType.MinInclusive:
                    valid = value >= min && value < max;
                    break;
                case RangeType.MaxInclusive:
                    valid = value > min && value <= max;
                    break;
                default:
                    valid = false;
                    break;
            }

            return valid ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}
#endif