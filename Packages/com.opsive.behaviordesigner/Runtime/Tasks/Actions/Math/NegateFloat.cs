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
    [Opsive.Shared.Utility.Description("Negates a float value (multiplies by -1).")]
    public class NegateFloat : Action
    {
        [Tooltip("The float value to negate.")]
        [SerializeField] protected SharedVariable<float> m_InputValue;
        [Tooltip("The resulting negated value.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_OutputValue;

        /// <summary>
        /// Negates the float value.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_OutputValue.Value = -m_InputValue.Value;

            return TaskStatus.Success;
        }
    }
}
#endif