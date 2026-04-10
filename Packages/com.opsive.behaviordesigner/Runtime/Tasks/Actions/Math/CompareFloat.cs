#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Math
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Math")]
    [Opsive.Shared.Utility.Description("Compares two float values. Returns -1 if first is less, 0 if equal, 1 if first is greater.")]
    public class CompareFloat : Action
    {
        [Tooltip("The first float value.")]
        [SerializeField] protected SharedVariable<float> m_Value1;
        [Tooltip("The second float value.")]
        [SerializeField] protected SharedVariable<float> m_Value2;
        [Tooltip("The resulting comparison value (-1, 0, or 1).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Compares the two float values.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Value1.Value < m_Value2.Value) {
                m_Result.Value = -1f;
            } else if (m_Value1.Value > m_Value2.Value) {
                m_Result.Value = 1f;
            } else {
                m_Result.Value = 0f;
            }

            return TaskStatus.Success;
        }
    }
}
#endif