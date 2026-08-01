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
    using RotateTowardsAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks.RotateTowards;

    /// <summary>
    /// Implements TypeControlBase for the RotateTowards task.
    /// </summary>
    [ControlType(typeof(RotateTowardsAction))]
    public class RotateTowardsControl : ConditionalTypeControlBase<RotateTowardsAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, RotateTowardsAction target)
        {
            return ConditionalActionControlBuilder.BuildTargetFallbackControl(input, target,
                new[] { "m_TargetGameObject" }, "m_Target", new[] { "m_Direction" }, "m_RotationSpeed", "m_UpVector", "m_ArrivedAngle");
        }
    }
}