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

    [Opsive.Shared.Utility.Description("Converts a boolean value to an integer value (true = 1, false = 0).")]
    [Shared.Utility.Category("Conversions")]
    public class ConvertBoolToInt : Action
    {
        [Tooltip("The boolean value to convert.")]
        [SerializeField] protected SharedVariable<bool> m_Value;
        [Tooltip("The variable that should be set to the converted integer value.")]
        [RequireShared] [SerializeField] protected SharedVariable<int> m_StoreResult;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            m_StoreResult.Value = m_Value.Value ? 1 : 0;
            return TaskStatus.Success;
        }
    }
}
#endif

