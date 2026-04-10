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
    [Opsive.Shared.Utility.Description("Linearly interpolates between two Vector3 values based on a t value (0 to 1).")]
    public class Vector3Lerp : Action
    {
        [Tooltip("The starting Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_From;
        [Tooltip("The ending Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_To;
        [Tooltip("The interpolation value (0 = from, 1 = to).")]
        [SerializeField] protected SharedVariable<float> m_T;
        [Tooltip("The resulting interpolated Vector3.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_Result;

        /// <summary>
        /// Linearly interpolates between the two Vector3 values.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var t = Mathf.Clamp01(m_T.Value);
            m_Result.Value = Vector3.Lerp(m_From.Value, m_To.Value, t);

            return TaskStatus.Success;
        }
    }
}
#endif