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
    using CharacterControllerEnableDisableAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.CharacterControllerTasks.EnableDisable;

    /// <summary>
    /// Implements TypeControlBase for the CharacterController EnableDisable task.
    /// </summary>
    [ControlType(typeof(CharacterControllerEnableDisableAction))]
    public class CharacterControllerEnableDisableControl : ConditionalTypeControlBase<CharacterControllerEnableDisableAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, CharacterControllerEnableDisableAction target)
        {
            return ConditionalActionControlBuilder.BuildConditionalControl(input, target,
                new[] { "m_TargetGameObject", "m_Enable" }, "m_SmoothTransition",
                (CharacterControllerEnableDisableAction currentTask) => ConditionalFieldControlUtility.GetValue(currentTask, "m_SmoothTransition", false),
                new[] { "m_DisabledHeight", "m_TransitionDuration" });
        }
    }
}