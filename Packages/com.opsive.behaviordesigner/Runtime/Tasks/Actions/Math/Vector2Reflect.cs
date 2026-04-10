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
    [Opsive.Shared.Utility.Description("Reflects a Vector2 off a normal vector.")]
    public class Vector2Reflect : Action
    {
        [Tooltip("The Vector2 direction to reflect.")]
        [SerializeField] protected SharedVariable<Vector2> m_Direction;
        [Tooltip("The normal vector to reflect off of.")]
        [SerializeField] protected SharedVariable<Vector2> m_Normal;
        [Tooltip("The resulting reflected Vector2.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2> m_Result;

        /// <summary>
        /// Reflects the Vector2 off the normal.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Vector2.Reflect(m_Direction.Value, m_Normal.Value);

            return TaskStatus.Success;
        }
    }
}
#endif