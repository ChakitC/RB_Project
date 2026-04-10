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
    [Opsive.Shared.Utility.Description("Calculates the distance between two Vector3 points.")]
    public class Vector3Distance : Action
    {
        [Tooltip("The first Vector3 point.")]
        [SerializeField] protected SharedVariable<Vector3> m_Point1;
        [Tooltip("The second Vector3 point.")]
        [SerializeField] protected SharedVariable<Vector3> m_Point2;
        [Tooltip("The resulting distance value.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Calculates the distance.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Vector3.Distance(m_Point1.Value, m_Point2.Value);

            return TaskStatus.Success;
        }
    }
}
#endif