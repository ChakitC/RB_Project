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
    [Opsive.Shared.Utility.Description("Generates a random Vector2 on a unit circle (normalized).")]
    public class RandomOnUnitCircle : Action
    {
        [Tooltip("The resulting random Vector2 on a unit circle.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2> m_Result;

        /// <summary>
        /// Generates a random Vector2 on a unit circle.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Random.insideUnitCircle.normalized;

            return TaskStatus.Success;
        }
    }
}
#endif