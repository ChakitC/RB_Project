/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Rigidbody
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using RigidbodyMoveTowardsAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.RigidbodyTasks.MoveTowards;

    /// <summary>
    /// Implements TypeControlBase for the Rigidbody MoveTowards task.
    /// </summary>
    [ControlType(typeof(RigidbodyMoveTowardsAction))]
    public class RigidbodyMoveTowardsControl : ConditionalTypeControlBase<RigidbodyMoveTowardsAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, RigidbodyMoveTowardsAction target)
        {
            return ConditionalActionControlBuilder.BuildTargetFallbackControl(input, target,
                new[] { "m_TargetGameObject" }, "m_Target", new[] { "m_TargetPosition" }, "m_Force", "m_ArrivedDistance", "m_HasArrived");
        }
    }
}