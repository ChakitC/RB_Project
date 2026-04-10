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
    [Opsive.Shared.Utility.Description("Calculates the angle (in degrees) between two Vector2 values.")]
    public class Vector2Angle : Action
    {
        [Tooltip("The first Vector2 value.")]
        [SerializeField] protected SharedVariable<Vector2> m_Vector1;
        [Tooltip("The second Vector2 value.")]
        [SerializeField] protected SharedVariable<Vector2> m_Vector2;
        [Tooltip("The resulting angle in degrees.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Calculates the angle between the two Vector2 values.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Vector2.Angle(m_Vector1.Value, m_Vector2.Value);

            return TaskStatus.Success;
        }
    }
}
#endif