#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Variables
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;
    using UnityEngine.Scripting.APIUpdating;

    [Opsive.Shared.Utility.Description("Set the float value.")]
    [Shared.Utility.Category("Variables")]
    [MovedFrom(false, "Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Math", "Opsive.BehaviorDesigner.Runtime", "SetFloat")]
    public class SetFloat : Action
    {
        [Tooltip("The float value to set.")]
        [SerializeField] protected SharedVariable<float> m_Value;
        [Tooltip("The variable that should be set.")]
        [RequireShared] [SerializeField] protected SharedVariable<float> m_StoreResult;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            m_StoreResult.Value = m_Value.Value;
            return TaskStatus.Success;
        }
    }
}
#endif