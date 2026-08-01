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
    using SetGravityAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.PhysicsTasks.SetGravity;

    /// <summary>
    /// Implements TypeControlBase for the SetGravity task.
    /// </summary>
    [ControlType(typeof(SetGravityAction))]
    public class SetGravityControl : ConditionalTypeControlBase<SetGravityAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, SetGravityAction target)
        {
            return ConditionalActionControlBuilder.BuildConditionalControl(input, target,
                new[] { "m_Gravity", "m_TransitionDuration" }, "m_UseGlobalGravity",
                (SetGravityAction currentTask) => !ConditionalFieldControlUtility.GetValue(currentTask, "m_UseGlobalGravity", true),
                new[] { "m_Rigidbody" });
        }
    }
}