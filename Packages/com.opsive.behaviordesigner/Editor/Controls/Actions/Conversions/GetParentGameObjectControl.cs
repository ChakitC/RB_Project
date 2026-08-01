/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Conversions
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using GetParentGameObjectAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Conversions.GetParentGameObject;

    /// <summary>
    /// Implements TypeControlBase for the GetParentGameObject task.
    /// </summary>
    [ControlType(typeof(GetParentGameObjectAction))]
    public class GetParentGameObjectControl : ConditionalTypeControlBase<GetParentGameObjectAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, GetParentGameObjectAction target)
        {
            return ConditionalActionControlBuilder.BuildTargetFallbackControl(input, target,
                null, "m_SourceGameObject", new[] { "m_SourceTransform" }, "m_LevelsUp", "m_ParentGameObject");
        }
    }
}