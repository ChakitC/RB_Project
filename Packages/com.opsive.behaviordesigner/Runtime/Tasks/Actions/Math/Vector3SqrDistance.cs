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
    [Opsive.Shared.Utility.Description("Calculates the squared distance between two Vector3 points. Faster than distance (no square root).")]
    public class Vector3SqrDistance : Action
    {
        [Tooltip("The first Vector3 point.")]
        [SerializeField] protected SharedVariable<Vector3> m_Point1;
        [Tooltip("The second Vector3 point.")]
        [SerializeField] protected SharedVariable<Vector3> m_Point2;
        [Tooltip("The resulting squared distance value.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Calculates the squared distance.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = (m_Point1.Value - m_Point2.Value).sqrMagnitude;

            return TaskStatus.Success;
        }
    }
}
#endif