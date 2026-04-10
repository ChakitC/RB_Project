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
    [Opsive.Shared.Utility.Description("Smoothly dampens a float value towards a target value over time.")]
    public class SmoothDampFloat : Action
    {
        [Tooltip("The current float value.")]
        [SerializeField] protected SharedVariable<float> m_Current;
        [Tooltip("The target float value.")]
        [SerializeField] protected SharedVariable<float> m_Target;
        [Tooltip("The current velocity (updated each frame).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_CurrentVelocity;
        [Tooltip("The time it takes to reach the target (approximately).")]
        [SerializeField] protected SharedVariable<float> m_SmoothTime = 0.3f;
        [Tooltip("The maximum speed. Set to Mathf.Infinity for no limit.")]
        [SerializeField] protected SharedVariable<float> m_MaxSpeed = Mathf.Infinity;
        [Tooltip("The resulting smoothed float value.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Smoothly dampens the float value.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var currentVelocity = m_CurrentVelocity.Value;

            m_Result.Value = Mathf.SmoothDamp(m_Current.Value, m_Target.Value, ref currentVelocity, m_SmoothTime.Value, m_MaxSpeed.Value, Time.deltaTime);
            m_CurrentVelocity.Value = currentVelocity;

            return TaskStatus.Success;
        }
    }
}
#endif