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

    [Opsive.Shared.Utility.Description("Converts a string value to a boolean value.")]
    [Shared.Utility.Category("Conversions")]
    public class ConvertStringToBool : Action
    {
        [Tooltip("The string value to convert.")]
        [SerializeField] protected SharedVariable<string> m_Value;
        [Tooltip("The variable that should be set to the converted boolean value.")]
        [RequireShared] [SerializeField] protected SharedVariable<bool> m_StoreResult;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            if (bool.TryParse(m_Value.Value, out bool result)) {
                m_StoreResult.Value = result;
                return TaskStatus.Success;
            }
            return TaskStatus.Failure;
        }
    }
}
#endif

