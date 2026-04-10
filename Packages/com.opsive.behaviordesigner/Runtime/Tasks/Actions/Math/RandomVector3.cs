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
    [Opsive.Shared.Utility.Description("Generates a random Vector3 value within a specified range.")]
    public class RandomVector3 : Action
    {
        [Tooltip("The minimum Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_MinValue = Vector3.zero;
        [Tooltip("The maximum Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_MaxValue = Vector3.one;
        [Tooltip("The resulting random Vector3 value.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_Result;

        /// <summary>
        /// Generates a random Vector3 value.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = new Vector3(
                Random.Range(m_MinValue.Value.x, m_MaxValue.Value.x),
                Random.Range(m_MinValue.Value.y, m_MaxValue.Value.y),
                Random.Range(m_MinValue.Value.z, m_MaxValue.Value.z)
            );

            return TaskStatus.Success;
        }
    }
}
#endif