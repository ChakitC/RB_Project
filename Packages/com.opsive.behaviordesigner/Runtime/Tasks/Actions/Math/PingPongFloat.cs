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
    [Opsive.Shared.Utility.Description("Ping-pongs a float value between 0 and a specified length.")]
    public class PingPongFloat : Action
    {
        [Tooltip("The time value to ping-pong.")]
        [SerializeField] protected SharedVariable<float> m_Time;
        [Tooltip("The length of the ping-pong range.")]
        [SerializeField] protected SharedVariable<float> m_Length = 1f;
        [Tooltip("The resulting ping-ponged float value.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Ping-pongs the float value.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Mathf.PingPong(m_Time.Value, m_Length.Value);

            return TaskStatus.Success;
        }
    }
}
#endif