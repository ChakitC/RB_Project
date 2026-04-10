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
    [Opsive.Shared.Utility.Description("Rounds a float up to the nearest integer.")]
    public class CeilFloat : Action
    {
        [Tooltip("The float value to ceil.")]
        [SerializeField] protected SharedVariable<float> m_InputValue;
        [Tooltip("The resulting ceiled float value.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Ceils the float value.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Mathf.Ceil(m_InputValue.Value);

            return TaskStatus.Success;
        }
    }
}
#endif