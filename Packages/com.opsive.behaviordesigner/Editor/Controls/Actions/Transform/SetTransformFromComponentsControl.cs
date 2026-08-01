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
    using SetTransformFromComponentsAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks.SetTransformFromComponents;

    /// <summary>
    /// Implements TypeControlBase for the SetTransformFromComponents task.
    /// </summary>
    [ControlType(typeof(SetTransformFromComponentsAction))]
    public class SetTransformFromComponentsControl : ConditionalTypeControlBase<SetTransformFromComponentsAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, SetTransformFromComponentsAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var positionContainer = ConditionalFieldControlUtility.CreateContainer();
            var rotationContainer = ConditionalFieldControlUtility.CreateContainer();
            var rotationEulerContainer = ConditionalFieldControlUtility.CreateContainer();
            var rotationQuaternionContainer = ConditionalFieldControlUtility.CreateContainer();
            var scaleContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var setPosition = ConditionalFieldControlUtility.GetValue(target, "m_SetPosition", false);
                var setRotation = ConditionalFieldControlUtility.GetValue(target, "m_SetRotation", false);
                var setScale = ConditionalFieldControlUtility.GetValue(target, "m_SetScale", false);
                var rotationType = ConditionalFieldControlUtility.GetEnumValue(target, "m_RotationType", SetTransformFromComponentsAction.RotationType.Euler);
                ConditionalFieldControlUtility.SetDisplay(positionContainer, setPosition);
                ConditionalFieldControlUtility.SetDisplay(rotationContainer, setRotation);
                ConditionalFieldControlUtility.SetDisplay(rotationEulerContainer, setRotation && rotationType == SetTransformFromComponentsAction.RotationType.Euler);
                ConditionalFieldControlUtility.SetDisplay(rotationQuaternionContainer, setRotation && rotationType == SetTransformFromComponentsAction.RotationType.Quaternion);
                ConditionalFieldControlUtility.SetDisplay(scaleContainer, setScale);
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_TargetGameObject");
            var refreshSetPosition = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_SetPosition", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_SetPosition", verticalLayout, (object obj) => {
                refreshSetPosition?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Position", positionContainer);
            verticalLayout.Add(positionContainer);
            var refreshSetRotation = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_SetRotation", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_SetRotation", verticalLayout, (object obj) => {
                refreshSetRotation?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_RotationType", rotationContainer, (object obj) => {
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_RotationEuler", rotationEulerContainer);
            rotationContainer.Add(rotationEulerContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_RotationQuaternion", rotationQuaternionContainer);
            rotationContainer.Add(rotationQuaternionContainer);
            verticalLayout.Add(rotationContainer);
            var refreshSetScale = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_SetScale", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_SetScale", verticalLayout, (object obj) => {
                refreshSetScale?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Scale", scaleContainer);
            verticalLayout.Add(scaleContainer);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}