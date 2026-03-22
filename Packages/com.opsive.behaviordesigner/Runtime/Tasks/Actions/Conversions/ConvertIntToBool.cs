#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Conversions
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Converts an integer value to a boolean value (0 = false, non-zero = true).")]
    [Shared.Utility.Category("Conversions")]
    public class ConvertIntToBool : Action
    {
        [Tooltip("The integer value to convert.")]
        [SerializeField] protected SharedVariable<int> m_Value;
        [Tooltip("The variable that should be set to the converted boolean value.")]
        [RequireShared] [SerializeField] protected SharedVariable<bool> m_StoreResult;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            m_StoreResult.Value = m_Value.Value != 0;
            return TaskStatus.Success;
        }
    }
}
#endif

