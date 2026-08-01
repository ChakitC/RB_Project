/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.NavMesh
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using NavMeshSetDestinationAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.NavMeshTasks.SetDestination;

    /// <summary>
    /// Implements TypeControlBase for the NavMesh SetDestination task.
    /// </summary>
    [ControlType(typeof(NavMeshSetDestinationAction))]
    public class NavMeshSetDestinationControl : ConditionalTypeControlBase<NavMeshSetDestinationAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, NavMeshSetDestinationAction target)
        {
            return ConditionalActionControlBuilder.BuildTargetFallbackControl(input, target,
                new[] { "m_TargetGameObject" }, "m_Target", new[] { "m_TargetPosition" }, "m_ArrivedDistance", "m_HasArrived");
        }
    }
}