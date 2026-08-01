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
    using SmoothLookAtAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks.SmoothLookAt;

    /// <summary>
    /// Implements TypeControlBase for the SmoothLookAt task.
    /// </summary>
    [ControlType(typeof(SmoothLookAtAction))]
    public class SmoothLookAtControl : ConditionalTypeControlBase<SmoothLookAtAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, SmoothLookAtAction target)
        {
            return ConditionalActionControlBuilder.BuildTargetFallbackControl(input, target,
                new[] { "m_TargetGameObject" }, "m_Target", new[] { "m_TargetPosition" }, "m_RotationSpeed", "m_UpVector", "m_LockAxis", "m_ArrivedAngle");
        }
    }
}