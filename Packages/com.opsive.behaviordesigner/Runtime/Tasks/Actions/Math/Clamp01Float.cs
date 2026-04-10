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
    [Opsive.Shared.Utility.Description("Clamps a float value between 0 and 1.")]
    public class Clamp01Float : Action
    {
        [Tooltip("The float value to clamp.")]
        [SerializeField] protected SharedVariable<float> m_InputValue;
        [Tooltip("The resulting clamped value (between 0 and 1).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Clamps the float value between 0 and 1.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Mathf.Clamp01(m_InputValue.Value);

            return TaskStatus.Success;
        }
    }
}
#endif