#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Debug
{
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Debug")]
    [Opsive.Shared.Utility.Description("Pauses the editor when executed. Equivalent to setting a breakpoint. Only works in the Unity Editor.")]
    public class Break : Action
    {
        /// <summary>
        /// Triggers a breakpoint in the editor and finishes.
        /// </summary>
        /// <returns>The Finished TaskStatus.</returns>
        public override TaskStatus OnUpdate()
        {
            UnityEngine.Debug.Break();
            return TaskStatus.Success;
        }
    }
}
#endif