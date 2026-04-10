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
    [Opsive.Shared.Utility.Description("Scales a Vector2 by another Vector2 (component-wise multiplication).")]
    public class Vector2Scale : Action
    {
        [Tooltip("The Vector2 to scale.")]
        [SerializeField] protected SharedVariable<Vector2> m_Vector;
        [Tooltip("The Vector2 to scale by (component-wise).")]
        [SerializeField] protected SharedVariable<Vector2> m_Scale;
        [Tooltip("The resulting scaled Vector2.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2> m_Result;

        /// <summary>
        /// Scales the Vector2.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Vector2.Scale(m_Vector.Value, m_Scale.Value);

            return TaskStatus.Success;
        }
    }
}
#endif