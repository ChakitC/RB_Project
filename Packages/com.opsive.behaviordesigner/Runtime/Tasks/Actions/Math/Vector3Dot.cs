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
    [Opsive.Shared.Utility.Description("Calculates the dot product of two Vector3 values.")]
    public class Vector3Dot : Action
    {
        [Tooltip("The first Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_Vector1;
        [Tooltip("The second Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_Vector2;
        [Tooltip("The resulting dot product value.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Calculates the dot product.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Vector3.Dot(m_Vector1.Value, m_Vector2.Value);

            return TaskStatus.Success;
        }
    }
}
#endif