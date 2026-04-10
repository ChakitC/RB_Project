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
    [Opsive.Shared.Utility.Description("Calculates the arc tangent (inverse tangent) of a value. Returns angle in radians.")]
    public class AtanFloat : Action
    {
        [Tooltip("The value to calculate the arc tangent of.")]
        [SerializeField] protected SharedVariable<float> m_Value;
        [Tooltip("The resulting angle in radians.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Calculates the arc tangent.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Mathf.Atan(m_Value.Value);

            return TaskStatus.Success;
        }
    }
}
#endif