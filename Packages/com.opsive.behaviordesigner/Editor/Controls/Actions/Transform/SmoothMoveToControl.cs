/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Transform
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using SmoothMoveToAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks.SmoothMoveTo;

    /// <summary>
    /// Implements TypeControlBase for the SmoothMoveTo task.
    /// </summary>
    [ControlType(typeof(SmoothMoveToAction))]
    public class SmoothMoveToControl : ConditionalTypeControlBase<SmoothMoveToAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, SmoothMoveToAction target)
        {
            return ConditionalActionControlBuilder.BuildTargetFallbackControl(input, target,
                new[] { "m_TargetGameObject" }, "m_Target", new[] { "m_TargetPosition" }, "m_MaxSpeed", "m_Acceleration", "m_Deceleration",
                "m_ArrivedDistance", "m_EasingType", "m_UseLocalSpace");
        }
    }
}