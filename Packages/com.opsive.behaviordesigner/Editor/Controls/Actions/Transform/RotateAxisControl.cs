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
    using RotateAxisAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks.RotateAxis;

    /// <summary>
    /// Implements TypeControlBase for the RotateAxis task.
    /// </summary>
    [ControlType(typeof(RotateAxisAction))]
    public class RotateAxisControl : ConditionalTypeControlBase<RotateAxisAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, RotateAxisAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var relativeRotationContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                ConditionalFieldControlUtility.SetDisplay(relativeRotationContainer, ConditionalFieldControlUtility.GetValue(target, "m_RelativeRotation", true));
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_TargetGameObject", "m_RotationInput", "m_Axis", "m_RotationSpeed");
            var refreshRelativeRotation = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_RelativeRotation", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_RelativeRotation", verticalLayout, (object obj) => {
                refreshRelativeRotation?.Invoke();
                UpdateVisibility();
            });
            ConditionalActionControlBuilder.AddFields(input, target, relativeRotationContainer, "m_MinAngle", "m_MaxAngle");
            verticalLayout.Add(relativeRotationContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_ArrivedAngle", verticalLayout);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}