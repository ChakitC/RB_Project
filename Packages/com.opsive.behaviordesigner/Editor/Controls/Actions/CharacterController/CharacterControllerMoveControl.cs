/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.CharacterController
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using CharacterControllerMoveAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.CharacterControllerTasks.Move;

    /// <summary>
    /// Implements TypeControlBase for the CharacterController Move task.
    /// </summary>
    [ControlType(typeof(CharacterControllerMoveAction))]
    public class CharacterControllerMoveControl : ConditionalTypeControlBase<CharacterControllerMoveAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, CharacterControllerMoveAction target)
        {
            return ConditionalActionControlBuilder.BuildConditionalControl(input, target,
                new[] { "m_TargetGameObject", "m_InputVector", "m_Speed", "m_Gravity", "m_RelativeMovement" }, "m_Jump",
                (CharacterControllerMoveAction currentTask) => ConditionalFieldControlUtility.GetValue(currentTask, "m_Jump", false),
                new[] { "m_JumpHeight" });
        }
    }
}