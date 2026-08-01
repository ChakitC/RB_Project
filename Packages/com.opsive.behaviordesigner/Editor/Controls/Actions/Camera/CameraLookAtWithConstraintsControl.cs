/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Camera
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using CameraLookAtWithConstraintsAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.CameraTasks.LookAtWithConstraints;

    /// <summary>
    /// Implements TypeControlBase for the Camera LookAtWithConstraints task.
    /// </summary>
    [ControlType(typeof(CameraLookAtWithConstraintsAction))]
    public class CameraLookAtWithConstraintsControl : ConditionalTypeControlBase<CameraLookAtWithConstraintsAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, CameraLookAtWithConstraintsAction target)
        {
            return ConditionalActionControlBuilder.BuildTargetFallbackControl(input, target,
                new[] { "m_TargetGameObject" }, "m_Target", new[] { "m_TargetPosition" }, "m_SmoothingSpeed", "m_MinRotationX", "m_MaxRotationX",
                "m_MinRotationY", "m_MaxRotationY", "m_ArrivedAngle");
        }
    }
}