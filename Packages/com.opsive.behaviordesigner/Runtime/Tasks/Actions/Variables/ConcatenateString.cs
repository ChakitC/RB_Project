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

    [Opsive.Shared.Utility.Description("Concatenate two strings and store the result.")]
    [Shared.Utility.Category("Variables")]
    public class ConcatenateString : Action
    {
        [Tooltip("The first string value to concatenate.")]
        [SerializeField] protected SharedVariable<string> m_String1;
        [Tooltip("The second string value to concatenate.")]
        [SerializeField] protected SharedVariable<string> m_String2;
        [Tooltip("The variable that should be set.")]
        [RequireShared] [SerializeField] protected SharedVariable<string> m_StoreResult;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            m_StoreResult.Value = m_String1.Value + m_String2.Value;
            return TaskStatus.Success;
        }
    }
}
#endif