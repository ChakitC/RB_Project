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
    [Opsive.Shared.Utility.Description("Generates a random Vector2 value within a specified range.")]
    public class RandomVector2 : Action
    {
        [Tooltip("The minimum Vector2 value.")]
        [SerializeField] protected SharedVariable<Vector2> m_MinValue = Vector2.zero;
        [Tooltip("The maximum Vector2 value.")]
        [SerializeField] protected SharedVariable<Vector2> m_MaxValue = Vector2.one;
        [Tooltip("The resulting random Vector2 value.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2> m_Result;

        /// <summary>
        /// Generates a random Vector2 value.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = new Vector2(
                Random.Range(m_MinValue.Value.x, m_MaxValue.Value.x),
                Random.Range(m_MinValue.Value.y, m_MaxValue.Value.y)
            );

            return TaskStatus.Success;
        }
    }
}
#endif