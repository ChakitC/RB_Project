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
    [Opsive.Shared.Utility.Description("Calculates the square root of a float value.")]
    public class SqrtFloat : Action
    {
        [Tooltip("The float value to calculate the square root of.")]
        [SerializeField] protected SharedVariable<float> m_InputValue;
        [Tooltip("The resulting square root value.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_OutputValue;

        /// <summary>
        /// Calculates the square root.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_OutputValue.Value = Mathf.Sqrt(m_InputValue.Value);

            return TaskStatus.Success;
        }
    }
}
#endif