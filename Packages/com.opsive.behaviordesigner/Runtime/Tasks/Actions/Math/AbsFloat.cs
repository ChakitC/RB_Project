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
    [Opsive.Shared.Utility.Description("Calculates the absolute value of a float.")]
    public class AbsFloat : Action
    {
        [Tooltip("The float value to get the absolute value of.")]
        [SerializeField] protected SharedVariable<float> m_InputValue;
        [Tooltip("The resulting absolute value.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_OutputValue;

        /// <summary>
        /// Calculates the absolute value.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_OutputValue.Value = Mathf.Abs(m_InputValue.Value);

            return TaskStatus.Success;
        }
    }
}
#endif