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
    [Opsive.Shared.Utility.Description("Moves a float value towards a target value by a maximum delta amount.")]
    public class MoveTowardsFloat : Action
    {
        [Tooltip("The current float value.")]
        [SerializeField] protected SharedVariable<float> m_Current;
        [Tooltip("The target float value.")]
        [SerializeField] protected SharedVariable<float> m_Target;
        [Tooltip("The maximum change allowed per frame.")]
        [SerializeField] protected SharedVariable<float> m_MaxDelta;
        [Tooltip("The resulting float value.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Moves the float value towards the target.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Mathf.MoveTowards(m_Current.Value, m_Target.Value, m_MaxDelta.Value);

            return TaskStatus.Success;
        }
    }
}
#endif