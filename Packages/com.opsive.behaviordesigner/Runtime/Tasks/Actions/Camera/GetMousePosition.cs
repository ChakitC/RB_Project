#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.CameraTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Camera")]
    [Opsive.Shared.Utility.Description("Gets the mouse position in screen coordinates.")]
    public class GetMousePosition : Action
    {
        [Tooltip("The mouse position in screen coordinates (x, y).")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2> m_MousePosition;

        /// <summary>
        /// Gets the mouse position.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_MousePosition.Value = Input.mousePosition;
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_MousePosition = null;
        }
    }
}
#endif