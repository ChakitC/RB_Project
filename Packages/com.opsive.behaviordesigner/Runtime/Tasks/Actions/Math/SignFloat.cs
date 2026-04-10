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
    [Opsive.Shared.Utility.Description("Returns the sign of a float value (-1 for negative, 0 for zero, 1 for positive).")]
    public class SignFloat : Action
    {
        [Tooltip("The float value to get the sign of.")]
        [SerializeField] protected SharedVariable<float> m_InputValue;
        [Tooltip("The resulting sign value (-1, 0, or 1).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Returns the sign of the float value.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Mathf.Sign(m_InputValue.Value);

            return TaskStatus.Success;
        }
    }
}
#endif