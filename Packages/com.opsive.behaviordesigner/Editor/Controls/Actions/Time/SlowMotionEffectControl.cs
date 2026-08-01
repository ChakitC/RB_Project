/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Time
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using SlowMotionEffectAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Time.SlowMotionEffect;

    /// <summary>
    /// Implements TypeControlBase for the SlowMotionEffect task.
    /// </summary>
    [ControlType(typeof(SlowMotionEffectAction))]
    public class SlowMotionEffectControl : ConditionalTypeControlBase<SlowMotionEffectAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, SlowMotionEffectAction target)
        {
            return ConditionalActionControlBuilder.BuildConditionalControl(input, target,
                new[] { "m_TargetTimeScale", "m_TransitionDuration" }, "m_RestoreAfterDuration",
                (SlowMotionEffectAction currentTask) => ConditionalFieldControlUtility.GetValue(currentTask, "m_RestoreAfterDuration", true),
                new[] { "m_Duration" });
        }
    }
}