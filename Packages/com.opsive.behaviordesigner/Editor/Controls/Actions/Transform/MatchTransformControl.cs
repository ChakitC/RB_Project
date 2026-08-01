/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Transform
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using MatchTransformAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks.MatchTransform;

    /// <summary>
    /// Implements TypeControlBase for the MatchTransform task.
    /// </summary>
    [ControlType(typeof(MatchTransformAction))]
    public class MatchTransformControl : ConditionalTypeControlBase<MatchTransformAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, MatchTransformAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var positionContainer = ConditionalFieldControlUtility.CreateContainer();
            var rotationContainer = ConditionalFieldControlUtility.CreateContainer();
            var scaleContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                ConditionalFieldControlUtility.SetDisplay(positionContainer, ConditionalFieldControlUtility.GetValue(target, "m_MatchPosition", true));
                ConditionalFieldControlUtility.SetDisplay(rotationContainer, ConditionalFieldControlUtility.GetValue(target, "m_MatchRotation", true));
                ConditionalFieldControlUtility.SetDisplay(scaleContainer, ConditionalFieldControlUtility.GetValue(target, "m_MatchScale", false));
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_TargetGameObject", "m_TargetTransform");
            var refreshMatchPosition = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_MatchPosition", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_MatchPosition", verticalLayout, (object obj) => {
                refreshMatchPosition?.Invoke();
                UpdateVisibility();
            });
            ConditionalActionControlBuilder.AddFields(input, target, positionContainer, "m_PositionSpeed", "m_PositionArrivedDistance");
            verticalLayout.Add(positionContainer);
            var refreshMatchRotation = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_MatchRotation", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_MatchRotation", verticalLayout, (object obj) => {
                refreshMatchRotation?.Invoke();
                UpdateVisibility();
            });
            ConditionalActionControlBuilder.AddFields(input, target, rotationContainer, "m_RotationSpeed", "m_RotationArrivedAngle");
            verticalLayout.Add(rotationContainer);
            var refreshMatchScale = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_MatchScale", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_MatchScale", verticalLayout, (object obj) => {
                refreshMatchScale?.Invoke();
                UpdateVisibility();
            });
            ConditionalActionControlBuilder.AddFields(input, target, scaleContainer, "m_ScaleSpeed", "m_ScaleArrivedDistance");
            verticalLayout.Add(scaleContainer);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}