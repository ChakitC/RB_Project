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
    using OrbitAroundAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks.OrbitAround;

    /// <summary>
    /// Implements TypeControlBase for the OrbitAround task.
    /// </summary>
    [ControlType(typeof(OrbitAroundAction))]
    public class OrbitAroundControl : ConditionalTypeControlBase<OrbitAroundAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, OrbitAroundAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var targetPositionContainer = ConditionalFieldControlUtility.CreateContainer();
            var customAxisContainer = ConditionalFieldControlUtility.CreateContainer();
            var upVectorContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var orbitAxis = ConditionalFieldControlUtility.GetEnumValue(target, "m_OrbitAxis", OrbitAroundAction.OrbitAxis.Y);
                ConditionalFieldControlUtility.SetDisplay(targetPositionContainer, !ConditionalFieldControlUtility.HasAssignedReference(target, "m_Target"));
                ConditionalFieldControlUtility.SetDisplay(customAxisContainer, orbitAxis == OrbitAroundAction.OrbitAxis.Custom);
                ConditionalFieldControlUtility.SetDisplay(upVectorContainer, ConditionalFieldControlUtility.GetValue(target, "m_LookAtCenter", false));
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_TargetGameObject");
            var refreshTarget = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_Target", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Target", verticalLayout, (object obj) => {
                refreshTarget?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_TargetPosition", targetPositionContainer);
            verticalLayout.Add(targetPositionContainer);
            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_Radius", "m_OrbitSpeed");
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_OrbitAxis", verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_CustomAxis", customAxisContainer);
            verticalLayout.Add(customAxisContainer);
            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_InitialAngle", "m_Clockwise");
            var refreshLookAtCenter = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_LookAtCenter", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_LookAtCenter", verticalLayout, (object obj) => {
                refreshLookAtCenter?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_UpVector", upVectorContainer);
            verticalLayout.Add(upVectorContainer);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}