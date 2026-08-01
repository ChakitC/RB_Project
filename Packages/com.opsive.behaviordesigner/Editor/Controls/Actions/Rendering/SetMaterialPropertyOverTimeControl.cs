/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Rendering
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using SetMaterialPropertyOverTimeAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.RenderingTasks.SetMaterialPropertyOverTime;

    /// <summary>
    /// Implements TypeControlBase for the SetMaterialPropertyOverTime task.
    /// </summary>
    [ControlType(typeof(SetMaterialPropertyOverTimeAction))]
    public class SetMaterialPropertyOverTimeControl : ConditionalTypeControlBase<SetMaterialPropertyOverTimeAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, SetMaterialPropertyOverTimeAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var colorContainer = ConditionalFieldControlUtility.CreateContainer();
            var floatContainer = ConditionalFieldControlUtility.CreateContainer();
            var vectorContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var propertyType = ConditionalFieldControlUtility.GetEnumValue(target, "m_PropertyType", SetMaterialPropertyOverTimeAction.PropertyType.Color);
                ConditionalFieldControlUtility.SetDisplay(colorContainer, propertyType == SetMaterialPropertyOverTimeAction.PropertyType.Color);
                ConditionalFieldControlUtility.SetDisplay(floatContainer, propertyType == SetMaterialPropertyOverTimeAction.PropertyType.Float);
                ConditionalFieldControlUtility.SetDisplay(vectorContainer, propertyType == SetMaterialPropertyOverTimeAction.PropertyType.Vector);
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_TargetGameObject", "m_PropertyName");
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_PropertyType", verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            ConditionalActionControlBuilder.AddFields(input, target, colorContainer, "m_StartColor", "m_TargetColor");
            verticalLayout.Add(colorContainer);
            ConditionalActionControlBuilder.AddFields(input, target, floatContainer, "m_StartFloat", "m_TargetFloat");
            verticalLayout.Add(floatContainer);
            ConditionalActionControlBuilder.AddFields(input, target, vectorContainer, "m_StartVector", "m_TargetVector");
            verticalLayout.Add(vectorContainer);
            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_Duration", "m_EasingType");
            UpdateVisibility();
            return verticalLayout;
        }
    }
}