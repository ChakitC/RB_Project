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
    using AnimatorSetStateAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AnimatorTasks.SetState;

    /// <summary>
    /// Implements TypeControlBase for the Animator SetState task.
    /// </summary>
    [ControlType(typeof(AnimatorSetStateAction))]
    public class AnimatorSetStateControl : ConditionalTypeControlBase<AnimatorSetStateAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, AnimatorSetStateAction target)
        {
            return ConditionalActionControlBuilder.BuildStringFallbackControl(input, target,
                new[] { "m_TargetGameObject" }, "m_StateName", new[] { "m_StateHash" }, "m_Layer", "m_TransitionDuration");
        }
    }
}