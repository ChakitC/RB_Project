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
    [Opsive.Shared.Utility.Description("Rounds a float to the nearest multiple of a specified value.")]
    public class RoundToNearestFloat : Action
    {
        [Tooltip("The float value to round.")]
        [SerializeField] protected SharedVariable<float> m_InputValue;
        [Tooltip("The value to round to the nearest multiple of (e.g., 0.5 to round to nearest 0.5).")]
        [SerializeField] protected SharedVariable<float> m_Nearest = 1f;
        [Tooltip("The resulting rounded float value.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Rounds the float to the nearest multiple.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var nearest = m_Nearest.Value != 0f ? m_Nearest.Value : 1f;
            m_Result.Value = Mathf.Round(m_InputValue.Value / nearest) * nearest;

            return TaskStatus.Success;
        }
    }
}
#endif