/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.GameObject
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using FadeInOutAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks.FadeInOut;

    /// <summary>
    /// Implements TypeControlBase for the FadeInOut task.
    /// </summary>
    [ControlType(typeof(FadeInOutAction))]
    public class FadeInOutControl : ConditionalTypeControlBase<FadeInOutAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, FadeInOutAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var startAlphaContainer = ConditionalFieldControlUtility.CreateContainer();
            var endAlphaContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var fadeDirection = ConditionalFieldControlUtility.GetEnumValue(target, "m_FadeDirection", FadeInOutAction.FadeDirection.FadeOut);
                ConditionalFieldControlUtility.SetDisplay(startAlphaContainer, fadeDirection == FadeInOutAction.FadeDirection.FadeIn);
                ConditionalFieldControlUtility.SetDisplay(endAlphaContainer, fadeDirection == FadeInOutAction.FadeDirection.FadeOut);
            }

            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_TargetGameObject", verticalLayout);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_FadeDirection", verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_FadeDuration", verticalLayout);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_StartAlpha", startAlphaContainer);
            verticalLayout.Add(startAlphaContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_EndAlpha", endAlphaContainer);
            verticalLayout.Add(endAlphaContainer);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}