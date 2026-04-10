#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Utility
{
    using UnityEngine;

    /// <summary>
    /// Quits the application.
    /// </summary>
    [Opsive.Shared.Utility.Category("Utility")]
    [Opsive.Shared.Utility.Description("Quits the application.")]
    public class ApplicationQuit : Action
    {
        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            Application.Quit();
            return TaskStatus.Success;
        }
    }
}
#endif