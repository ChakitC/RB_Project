#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Debug
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;
    using UnityEngine.Scripting.APIUpdating;

    /// <summary>
    /// Logs the specified value.
    /// </summary>
    [NodeIcon("c97bee71424b3e247a161d1279643506", "138439e3588de5d449b7949d68d32ad8")]
    [MovedFrom(false, "Opsive.BehaviorDesigner.Runtime.Tasks.Actions", "Opsive.BehaviorDesigner.Runtime", "LogValue")]
    public class LogValue : Action
    {
        [Tooltip("The value that should be outputted to the console.")]
        [RequireShared] [SerializeField] protected SharedVariable m_Value;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Value.Scope == SharedVariable.SharingScope.Empty) {
                UnityEngine.Debug.LogWarning("Warning: The LogValue.Value variable must be set.");
                return TaskStatus.Failure;
            }

            UnityEngine.Debug.Log(m_Value.GetValue());
            return TaskStatus.Success;
        }
    }
}
#endif