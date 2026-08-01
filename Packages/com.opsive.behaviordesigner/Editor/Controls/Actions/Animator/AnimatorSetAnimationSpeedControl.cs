/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Animator
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using AnimatorSetAnimationSpeedAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AnimatorTasks.SetAnimationSpeed;

    /// <summary>
    /// Implements TypeControlBase for the Animator SetAnimationSpeed task.
    /// </summary>
    [ControlType(typeof(AnimatorSetAnimationSpeedAction))]
    public class AnimatorSetAnimationSpeedControl : ConditionalTypeControlBase<AnimatorSetAnimationSpeedAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, AnimatorSetAnimationSpeedAction target)
        {
            return ConditionalActionControlBuilder.BuildConditionalControl(input, target,
                new[] { "m_TargetGameObject", "m_TargetSpeed" }, "m_UseSmoothTransition",
                (AnimatorSetAnimationSpeedAction currentTask) => ConditionalFieldControlUtility.GetValue(currentTask, "m_UseSmoothTransition", true),
                new[] { "m_TransitionDuration" });
        }
    }
}