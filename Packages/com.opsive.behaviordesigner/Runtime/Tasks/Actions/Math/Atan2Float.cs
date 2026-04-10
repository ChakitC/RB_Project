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
    [Opsive.Shared.Utility.Description("Calculates the arc tangent of y/x. Returns angle in radians. Useful for converting direction to angle.")]
    public class Atan2Float : Action
    {
        [Tooltip("The Y component.")]
        [SerializeField] protected SharedVariable<float> m_Y;
        [Tooltip("The X component.")]
        [SerializeField] protected SharedVariable<float> m_X;
        [Tooltip("The resulting angle in radians.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Calculates the arc tangent of y/x.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Mathf.Atan2(m_Y.Value, m_X.Value);

            return TaskStatus.Success;
        }
    }
}
#endif