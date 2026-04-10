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
    [Opsive.Shared.Utility.Description("Calculates the arc sine (inverse sine) of a value. Returns angle in radians.")]
    public class AsinFloat : Action
    {
        [Tooltip("The value to calculate the arc sine of (must be between -1 and 1).")]
        [SerializeField] protected SharedVariable<float> m_Value;
        [Tooltip("The resulting angle in radians.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Calculates the arc sine.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Mathf.Asin(Mathf.Clamp(m_Value.Value, -1f, 1f));

            return TaskStatus.Success;
        }
    }
}
#endif