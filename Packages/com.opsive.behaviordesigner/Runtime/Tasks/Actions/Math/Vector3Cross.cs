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
    [Opsive.Shared.Utility.Description("Calculates the cross product of two Vector3 values.")]
    public class Vector3Cross : Action
    {
        [Tooltip("The first Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_Vector1;
        [Tooltip("The second Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_Vector2;
        [Tooltip("The resulting cross product Vector3.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_Result;

        /// <summary>
        /// Calculates the cross product.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Vector3.Cross(m_Vector1.Value, m_Vector2.Value);

            return TaskStatus.Success;
        }
    }
}
#endif