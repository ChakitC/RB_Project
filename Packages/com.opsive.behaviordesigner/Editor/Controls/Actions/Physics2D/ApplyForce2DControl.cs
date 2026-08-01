/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Physics2D
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using ApplyForce2DAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Physics2DTasks.ApplyForce2D;

    /// <summary>
    /// Implements TypeControlBase for the ApplyForce2D task.
    /// </summary>
    [ControlType(typeof(ApplyForce2DAction))]
    public class ApplyForce2DControl : ConditionalTypeControlBase<ApplyForce2DAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, ApplyForce2DAction target)
        {
            return ConditionalActionControlBuilder.BuildConditionalControl(input, target,
                new[] { "m_TargetGameObject", "m_Force", "m_ForceMode", "m_UseLocalSpace" }, "m_ApplyAtPosition",
                (ApplyForce2DAction currentTask) => ConditionalFieldControlUtility.GetValue(currentTask, "m_ApplyAtPosition", false),
                new[] { "m_Position" });
        }
    }
}