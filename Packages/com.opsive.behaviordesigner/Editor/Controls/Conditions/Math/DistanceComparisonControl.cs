/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Conditions.Math
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using DistanceComparisonCondition = Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.Math.DistanceComparison;

    /// <summary>
    /// Implements TypeControlBase for the DistanceComparison conditional.
    /// </summary>
    [ControlType(typeof(DistanceComparisonCondition))]
    public class DistanceComparisonControl : ConditionalTypeControlBase<DistanceComparisonCondition>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, DistanceComparisonCondition target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var source1GameObjectContainer = ConditionalFieldControlUtility.CreateContainer();
            var source1PositionContainer = ConditionalFieldControlUtility.CreateContainer();
            var source2GameObjectContainer = ConditionalFieldControlUtility.CreateContainer();
            var source2PositionContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var source1Type = ConditionalFieldControlUtility.GetEnumValue(target, "m_Source1Type", DistanceComparisonCondition.PositionSourceType.GameObject);
                var source2Type = ConditionalFieldControlUtility.GetEnumValue(target, "m_Source2Type", DistanceComparisonCondition.PositionSourceType.GameObject);
                ConditionalFieldControlUtility.SetDisplay(source1GameObjectContainer, source1Type == DistanceComparisonCondition.PositionSourceType.GameObject);
                ConditionalFieldControlUtility.SetDisplay(source1PositionContainer, source1Type == DistanceComparisonCondition.PositionSourceType.Vector3);
                ConditionalFieldControlUtility.SetDisplay(source2GameObjectContainer, source2Type == DistanceComparisonCondition.PositionSourceType.GameObject);
                ConditionalFieldControlUtility.SetDisplay(source2PositionContainer, source2Type == DistanceComparisonCondition.PositionSourceType.Vector3);
            }

            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Operation", verticalLayout);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Source1Type", verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_GameObject1", source1GameObjectContainer);
            verticalLayout.Add(source1GameObjectContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Position1", source1PositionContainer);
            verticalLayout.Add(source1PositionContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Source2Type", verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_GameObject2", source2GameObjectContainer);
            verticalLayout.Add(source2GameObjectContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Position2", source2PositionContainer);
            verticalLayout.Add(source2PositionContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Distance", verticalLayout);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}