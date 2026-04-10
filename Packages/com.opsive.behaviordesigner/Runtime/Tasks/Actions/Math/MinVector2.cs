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
    [Opsive.Shared.Utility.Description("Returns the component-wise minimum of two Vector2 values.")]
    public class MinVector2 : Action
    {
        [Tooltip("The first Vector2 value.")]
        [SerializeField] protected SharedVariable<Vector2> m_Value1;
        [Tooltip("The second Vector2 value.")]
        [SerializeField] protected SharedVariable<Vector2> m_Value2;
        [Tooltip("The resulting minimum Vector2.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2> m_Result;

        /// <summary>
        /// Returns the component-wise minimum.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Vector2.Min(m_Value1.Value, m_Value2.Value);
            return TaskStatus.Success;
        }
    }
}
#endif