/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.CharacterController
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using CharacterControllerTeleportAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.CharacterControllerTasks.Teleport;

    /// <summary>
    /// Implements TypeControlBase for the CharacterController Teleport task.
    /// </summary>
    [ControlType(typeof(CharacterControllerTeleportAction))]
    public class CharacterControllerTeleportControl : ConditionalTypeControlBase<CharacterControllerTeleportAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, CharacterControllerTeleportAction target)
        {
            target.MigrateLegacyFields();

            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var rotationContainer = ConditionalFieldControlUtility.CreateContainer();
            var groundSnapContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                ConditionalFieldControlUtility.SetDisplay(rotationContainer, ConditionalFieldControlUtility.GetValue(target, "m_ApplyRotation", false));
                ConditionalFieldControlUtility.SetDisplay(groundSnapContainer, ConditionalFieldControlUtility.GetValue(target, "m_SnapToGround", false));
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_TargetGameObject", "m_TargetPosition");
            var refreshApplyRotation = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_ApplyRotation", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_ApplyRotation", verticalLayout, (object obj) => {
                refreshApplyRotation?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_TargetRotation", rotationContainer);
            verticalLayout.Add(rotationContainer);
            var refreshSnapToGround = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_SnapToGround", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_SnapToGround", verticalLayout, (object obj) => {
                refreshSnapToGround?.Invoke();
                UpdateVisibility();
            });
            ConditionalActionControlBuilder.AddFields(input, target, groundSnapContainer, "m_GroundSnapDistance", "m_GroundLayerMask");
            verticalLayout.Add(groundSnapContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_TeleportSuccessful", verticalLayout);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}