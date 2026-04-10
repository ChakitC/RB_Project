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
    [Opsive.Shared.Utility.Description("Returns the component-wise minimum of two Vector3 values.")]
    public class MinVector3 : Action
    {
        [Tooltip("The first Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_Value1;
        [Tooltip("The second Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_Value2;
        [Tooltip("The resulting minimum Vector3.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_Result;

        /// <summary>
        /// Returns the component-wise minimum.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Vector3.Min(m_Value1.Value, m_Value2.Value);

            return TaskStatus.Success;
        }
    }
}
#endif