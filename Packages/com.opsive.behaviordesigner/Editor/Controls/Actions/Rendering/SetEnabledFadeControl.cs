/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Rendering
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using SetEnabledFadeAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.RenderingTasks.SetEnabledFade;

    /// <summary>
    /// Implements TypeControlBase for the SetEnabledFade task.
    /// </summary>
    [ControlType(typeof(SetEnabledFadeAction))]
    public class SetEnabledFadeControl : ConditionalTypeControlBase<SetEnabledFadeAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, SetEnabledFadeAction target)
        {
            return ConditionalActionControlBuilder.BuildConditionalControl(input, target,
                new[] { "m_TargetGameObject", "m_Enable" }, "m_FadeTransition",
                (SetEnabledFadeAction currentTask) => ConditionalFieldControlUtility.GetValue(currentTask, "m_FadeTransition", false),
                new[] { "m_FadeDuration" });
        }
    }
}