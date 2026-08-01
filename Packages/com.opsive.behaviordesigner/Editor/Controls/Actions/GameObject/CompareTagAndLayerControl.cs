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
    using CompareTagAndLayerAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks.CompareTagAndLayer;

    /// <summary>
    /// Implements TypeControlBase for the CompareTagAndLayer task.
    /// </summary>
    [ControlType(typeof(CompareTagAndLayerAction))]
    public class CompareTagAndLayerControl : ConditionalTypeControlBase<CompareTagAndLayerAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, CompareTagAndLayerAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var tagContainer = ConditionalFieldControlUtility.CreateContainer();
            var layerContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var comparisonMode = ConditionalFieldControlUtility.GetEnumValue(target, "m_ComparisonMode", CompareTagAndLayerAction.ComparisonMode.BothMatch);
                var usesTag = comparisonMode != CompareTagAndLayerAction.ComparisonMode.LayerOnly;
                var usesLayer = comparisonMode != CompareTagAndLayerAction.ComparisonMode.TagOnly;
                ConditionalFieldControlUtility.SetDisplay(tagContainer, usesTag);
                ConditionalFieldControlUtility.SetDisplay(layerContainer, usesLayer);
            }

            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_TargetGameObject", verticalLayout);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_ComparisonMode", verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Tag", tagContainer);
            verticalLayout.Add(tagContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_LayerMask", layerContainer);
            verticalLayout.Add(layerContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_ConditionMet", verticalLayout);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}