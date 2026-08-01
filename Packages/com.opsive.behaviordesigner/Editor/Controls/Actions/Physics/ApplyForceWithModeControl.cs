/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Physics
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using ApplyForceWithModeAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.PhysicsTasks.ApplyForceWithMode;

    /// <summary>
    /// Implements TypeControlBase for the ApplyForceWithMode task.
    /// </summary>
    [ControlType(typeof(ApplyForceWithModeAction))]
    public class ApplyForceWithModeControl : ConditionalTypeControlBase<ApplyForceWithModeAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, ApplyForceWithModeAction target)
        {
            return ConditionalActionControlBuilder.BuildConditionalControl(input, target,
                new[] { "m_TargetGameObject", "m_Force", "m_ForceMode", "m_UseLocalSpace" }, "m_ApplyAtPosition",
                (ApplyForceWithModeAction currentTask) => ConditionalFieldControlUtility.GetValue(currentTask, "m_ApplyAtPosition", false),
                new[] { "m_Position" });
        }
    }
}