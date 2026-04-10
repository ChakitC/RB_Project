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
    /// Logs the specified string.
    /// </summary>
    [NodeIcon("c97bee71424b3e247a161d1279643506", "138439e3588de5d449b7949d68d32ad8")]
    [Opsive.Shared.Utility.Description("A simple task which will output the specified text and return success. It can be used for debugging.")]
    [MovedFrom(false, "Opsive.BehaviorDesigner.Runtime.Tasks.Actions", "Opsive.BehaviorDesigner.Runtime", "Log")]
    public class Log : Action
    {
        [Tooltip("The string that should be outputted to the console.")]
        [SerializeField] protected SharedVariable<string> m_Text;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            UnityEngine.Debug.Log(m_Text.Value);
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the task values.
        /// </summary>
        public override void Reset()
        {
            m_Text = string.Empty;
        }
    }
}
#endif