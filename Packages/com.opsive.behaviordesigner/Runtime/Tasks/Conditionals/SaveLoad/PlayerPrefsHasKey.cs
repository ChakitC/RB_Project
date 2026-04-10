#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.SaveLoadTasks
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    /// <summary>
    /// Checks if a PlayerPrefs key exists.
    /// </summary>
    [Opsive.Shared.Utility.Category("Save//Load")]
    [Opsive.Shared.Utility.Description("Checks if a PlayerPrefs key exists.")]
    public class PlayerPrefsHasKey : Conditional
    {
        [Tooltip("The PlayerPrefs key to check.")]
        [SerializeField] protected SharedVariable<string> m_Key;

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            if (string.IsNullOrEmpty(m_Key.Value)) {
                return TaskStatus.Failure;
            }

            return PlayerPrefs.HasKey(m_Key.Value) ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}
#endif