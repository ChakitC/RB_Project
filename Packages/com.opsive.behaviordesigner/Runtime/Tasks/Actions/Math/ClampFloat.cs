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
    [Opsive.Shared.Utility.Description("Clamps a float value between a minimum and maximum value.")]
    public class ClampFloat : Action
    {
        [Tooltip("The float value to clamp.")]
        [SerializeField] protected SharedVariable<float> m_InputValue;
        [Tooltip("The minimum value.")]
        [SerializeField] protected SharedVariable<float> m_MinValue;
        [Tooltip("The maximum value.")]
        [SerializeField] protected SharedVariable<float> m_MaxValue;
        [Tooltip("The resulting clamped value.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Clamps the float value.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Mathf.Clamp(m_InputValue.Value, m_MinValue.Value, m_MaxValue.Value);

            return TaskStatus.Success;
        }
    }
}
#endif