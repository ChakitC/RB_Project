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
    using CopyTransformAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks.CopyTransform;

    /// <summary>
    /// Implements TypeControlBase for the CopyTransform task.
    /// </summary>
    [ControlType(typeof(CopyTransformAction))]
    public class CopyTransformControl : ConditionalTypeControlBase<CopyTransformAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, CopyTransformAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var positionSpeedContainer = ConditionalFieldControlUtility.CreateContainer();
            var rotationSpeedContainer = ConditionalFieldControlUtility.CreateContainer();
            var scaleSpeedContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var smooth = ConditionalFieldControlUtility.GetValue(target, "m_Smooth", false);
                ConditionalFieldControlUtility.SetDisplay(positionSpeedContainer, smooth && ConditionalFieldControlUtility.GetValue(target, "m_CopyPosition", true));
                ConditionalFieldControlUtility.SetDisplay(rotationSpeedContainer, smooth && ConditionalFieldControlUtility.GetValue(target, "m_CopyRotation", true));
                ConditionalFieldControlUtility.SetDisplay(scaleSpeedContainer, smooth && ConditionalFieldControlUtility.GetValue(target, "m_CopyScale", false));
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_TargetGameObject", "m_SourceTransform");
            var refreshCopyPosition = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_CopyPosition", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_CopyPosition", verticalLayout, (object obj) => {
                refreshCopyPosition?.Invoke();
                UpdateVisibility();
            });
            var refreshCopyRotation = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_CopyRotation", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_CopyRotation", verticalLayout, (object obj) => {
                refreshCopyRotation?.Invoke();
                UpdateVisibility();
            });
            var refreshCopyScale = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_CopyScale", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_CopyScale", verticalLayout, (object obj) => {
                refreshCopyScale?.Invoke();
                UpdateVisibility();
            });
            var refreshSmooth = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_Smooth", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Smooth", verticalLayout, (object obj) => {
                refreshSmooth?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_PositionSpeed", positionSpeedContainer);
            verticalLayout.Add(positionSpeedContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_RotationSpeed", rotationSpeedContainer);
            verticalLayout.Add(rotationSpeedContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_ScaleSpeed", scaleSpeedContainer);
            verticalLayout.Add(scaleSpeedContainer);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}