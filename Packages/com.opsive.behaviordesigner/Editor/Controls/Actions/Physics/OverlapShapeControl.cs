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
    using OverlapShapeAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.PhysicsTasks.OverlapShape;

    /// <summary>
    /// Implements TypeControlBase for the OverlapShape task.
    /// </summary>
    [ControlType(typeof(OverlapShapeAction))]
    public class OverlapShapeControl : ConditionalTypeControlBase<OverlapShapeAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, OverlapShapeAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var rotationContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var overlapType = ConditionalFieldControlUtility.GetEnumValue(target, "m_OverlapType", OverlapShapeAction.OverlapType.Sphere);
                ConditionalFieldControlUtility.SetDisplay(rotationContainer, overlapType == OverlapShapeAction.OverlapType.Box);
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_TargetGameObject");
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_OverlapType", verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Center", verticalLayout);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Size", verticalLayout);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Rotation", rotationContainer);
            verticalLayout.Add(rotationContainer);
            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_LayerMask", "m_RequiredTag", "m_MinOverlaps", "m_OverlapsDetected", "m_OverlapCount", "m_OverlappedGameObjects");
            UpdateVisibility();
            return verticalLayout;
        }
    }
}