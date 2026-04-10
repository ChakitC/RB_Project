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
    [Opsive.Shared.Utility.Description("Calculates the angle (in degrees) between two Vector3 values.")]
    public class Vector3Angle : Action
    {
        [Tooltip("The first Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_Vector1;
        [Tooltip("The second Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_Vector2;
        [Tooltip("The resulting angle in degrees.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Calculates the angle between the two Vector3 values.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Vector3.Angle(m_Vector1.Value, m_Vector2.Value);

            return TaskStatus.Success;
        }
    }
}
#endif