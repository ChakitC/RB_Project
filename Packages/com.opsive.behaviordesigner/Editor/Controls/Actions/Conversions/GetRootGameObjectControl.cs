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
    using GetRootGameObjectAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Conversions.GetRootGameObject;

    /// <summary>
    /// Implements TypeControlBase for the GetRootGameObject task.
    /// </summary>
    [ControlType(typeof(GetRootGameObjectAction))]
    public class GetRootGameObjectControl : ConditionalTypeControlBase<GetRootGameObjectAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, GetRootGameObjectAction target)
        {
            return ConditionalActionControlBuilder.BuildTargetFallbackControl(input, target,
                null, "m_SourceGameObject", new[] { "m_SourceTransform" }, "m_RootGameObject");
        }
    }
}