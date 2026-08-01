/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.GameObject
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using MoveToParentAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks.MoveToParent;

    /// <summary>
    /// Implements TypeControlBase for the MoveToParent task.
    /// </summary>
    [ControlType(typeof(MoveToParentAction))]
    public class MoveToParentControl : ConditionalTypeControlBase<MoveToParentAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, MoveToParentAction target)
        {
            return ConditionalActionControlBuilder.BuildConditionalControl(input, target,
                new[] { "m_Target", "m_NewParent" }, "m_SmoothMovement",
                (MoveToParentAction currentTask) => ConditionalFieldControlUtility.GetValue(currentTask, "m_SmoothMovement", false),
                new[] { "m_MovementSpeed" }, "m_WorldPositionStays");
        }
    }
}