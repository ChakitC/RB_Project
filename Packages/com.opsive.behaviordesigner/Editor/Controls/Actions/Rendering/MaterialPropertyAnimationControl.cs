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
    using MaterialPropertyAnimationAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.RenderingTasks.MaterialPropertyAnimation;

    /// <summary>
    /// Implements TypeControlBase for the MaterialPropertyAnimation task.
    /// </summary>
    [ControlType(typeof(MaterialPropertyAnimationAction))]
    public class MaterialPropertyAnimationControl : ConditionalTypeControlBase<MaterialPropertyAnimationAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, MaterialPropertyAnimationAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var valueMultiplierContainer = ConditionalFieldControlUtility.CreateContainer();
            var colorMultiplierContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var propertyType = ConditionalFieldControlUtility.GetEnumValue(target, "m_PropertyType", MaterialPropertyAnimationAction.PropertyType.Float);
                ConditionalFieldControlUtility.SetDisplay(valueMultiplierContainer, propertyType == MaterialPropertyAnimationAction.PropertyType.Float || propertyType == MaterialPropertyAnimationAction.PropertyType.Vector);
                ConditionalFieldControlUtility.SetDisplay(colorMultiplierContainer, propertyType == MaterialPropertyAnimationAction.PropertyType.Color);
            }

            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_TargetGameObject", verticalLayout);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_PropertyType", verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_PropertyName", "m_AnimationCurve", "m_Duration");
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_ValueMultiplier", valueMultiplierContainer);
            verticalLayout.Add(valueMultiplierContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_ColorMultiplier", colorMultiplierContainer);
            verticalLayout.Add(colorMultiplierContainer);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}