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
    using PhysicsBasedMovement2DAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Physics2DTasks.PhysicsBasedMovement2D;

    /// <summary>
    /// Implements TypeControlBase for the PhysicsBasedMovement2D task.
    /// </summary>
    [ControlType(typeof(PhysicsBasedMovement2DAction))]
    public class PhysicsBasedMovement2DControl : ConditionalTypeControlBase<PhysicsBasedMovement2DAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, PhysicsBasedMovement2DAction target)
        {
            return ConditionalActionControlBuilder.BuildTargetFallbackControl(input, target,
                new[] { "m_TargetGameObject" }, "m_Target", new[] { "m_TargetPosition" }, "m_MaxForce", "m_ArrivedDistance",
                "m_AccelerationCurve", "m_DecelerationCurve", "m_DecelerationStartDistance", "m_MaxDistance", "m_HasArrived", "m_CurrentDistance");
        }
    }
}