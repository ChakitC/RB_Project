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
    using FollowTargetAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks.FollowTarget;

    /// <summary>
    /// Implements TypeControlBase for the FollowTarget task.
    /// </summary>
    [ControlType(typeof(FollowTargetAction))]
    public class FollowTargetControl : ConditionalTypeControlBase<FollowTargetAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, FollowTargetAction target)
        {
            return ConditionalActionControlBuilder.BuildConditionalControl(input, target,
                new[] { "m_TargetGameObject", "m_Target", "m_FollowDistance", "m_Offset", "m_FollowSpeed", "m_SmoothTime", "m_HeightOffset" }, "m_LookAtTarget",
                (FollowTargetAction currentTask) => ConditionalFieldControlUtility.GetValue(currentTask, "m_LookAtTarget", false),
                new[] { "m_RotationSpeed", "m_UpVector", "m_RotationArrivedAngle" }, "m_PositionArrivedDistance");
        }
    }
}