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
    using TransformLookAtWithConstraintsAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks.LookAtWithConstraints;

    /// <summary>
    /// Implements TypeControlBase for the Transform LookAtWithConstraints task.
    /// </summary>
    [ControlType(typeof(TransformLookAtWithConstraintsAction))]
    public class TransformLookAtWithConstraintsControl : ConditionalTypeControlBase<TransformLookAtWithConstraintsAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, TransformLookAtWithConstraintsAction target)
        {
            return ConditionalActionControlBuilder.BuildTargetFallbackControl(input, target,
                new[] { "m_TargetGameObject" }, "m_Target", new[] { "m_TargetPosition" }, "m_MinPitch", "m_MaxPitch", "m_MinYaw", "m_MaxYaw",
                "m_RotationSpeed", "m_SmoothTime", "m_UpVector", "m_ArrivedAngle");
        }
    }
}