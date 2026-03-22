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

    [Opsive.Shared.Utility.Description("Converts a float value to a boolean value (0.0 = false, non-zero = true).")]
    [Shared.Utility.Category("Conversions")]
    public class ConvertFloatToBool : Action
    {
        [Tooltip("The float value to convert.")]
        [SerializeField] protected SharedVariable<float> m_Value;
        [Tooltip("The variable that should be set to the converted boolean value.")]
        [RequireShared] [SerializeField] protected SharedVariable<bool> m_StoreResult;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            m_StoreResult.Value = m_Value.Value != 0.0f;
            return TaskStatus.Success;
        }
    }
}
#endif

