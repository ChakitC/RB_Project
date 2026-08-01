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
    using Physics2DMoveTowardsAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Physics2DTasks.MoveTowards2D;

    /// <summary>
    /// Implements TypeControlBase for the Physics2D MoveTowards task.
    /// </summary>
    [ControlType(typeof(Physics2DMoveTowardsAction))]
    public class Physics2DMoveTowardsControl : ConditionalTypeControlBase<Physics2DMoveTowardsAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, Physics2DMoveTowardsAction target)
        {
            return ConditionalActionControlBuilder.BuildTargetFallbackControl(input, target,
                new[] { "m_TargetGameObject" }, "m_Target", new[] { "m_TargetPosition" }, "m_Force", "m_ArrivedDistance", "m_HasArrived");
        }
    }
}