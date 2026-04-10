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
    [Opsive.Shared.Utility.Description("Returns the component-wise maximum of two Vector3 values.")]
    public class MaxVector3 : Action
    {
        [Tooltip("The first Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_Value1;
        [Tooltip("The second Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_Value2;
        [Tooltip("The resulting maximum Vector3.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_Result;

        /// <summary>
        /// Returns the component-wise maximum.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Vector3.Max(m_Value1.Value, m_Value2.Value);
            return TaskStatus.Success;
        }
    }
}
#endif