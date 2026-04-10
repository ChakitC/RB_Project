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
    [Opsive.Shared.Utility.Description("Converts a Vector2 direction to an angle in degrees.")]
    public class DirectionToAngle : Action
    {
        [Tooltip("The Vector2 direction.")]
        [SerializeField] protected SharedVariable<Vector2> m_Direction;
        [Tooltip("The resulting angle in degrees.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Converts the direction to an angle.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Mathf.Atan2(m_Direction.Value.y, m_Direction.Value.x) * Mathf.Rad2Deg;

            return TaskStatus.Success;
        }
    }
}
#endif