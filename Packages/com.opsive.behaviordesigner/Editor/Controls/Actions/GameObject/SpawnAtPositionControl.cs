/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.GameObject
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using SpawnAtPositionAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks.SpawnAtPosition;

    /// <summary>
    /// Implements TypeControlBase for the SpawnAtPosition task.
    /// </summary>
    [ControlType(typeof(SpawnAtPositionAction))]
    public class SpawnAtPositionControl : ConditionalTypeControlBase<SpawnAtPositionAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, SpawnAtPositionAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var positionRotationContainer = ConditionalFieldControlUtility.CreateContainer();
            var destroyDurationContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                ConditionalFieldControlUtility.SetDisplay(positionRotationContainer, !ConditionalFieldControlUtility.HasAssignedReference(target, "m_SpawnTransform"));
                ConditionalFieldControlUtility.SetDisplay(destroyDurationContainer, ConditionalFieldControlUtility.GetValue(target, "m_AutoDestroy", false));
            }

            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Prefab", verticalLayout);
            var refreshSpawnTransform = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_SpawnTransform", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_SpawnTransform", verticalLayout, (object obj) => {
                refreshSpawnTransform?.Invoke();
                UpdateVisibility();
            });
            ConditionalActionControlBuilder.AddFields(input, target, positionRotationContainer, "m_SpawnPosition", "m_SpawnRotation");
            verticalLayout.Add(positionRotationContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Parent", verticalLayout);
            var refreshAutoDestroy = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_AutoDestroy", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_AutoDestroy", verticalLayout, (object obj) => {
                refreshAutoDestroy?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_DestroyDuration", destroyDurationContainer);
            verticalLayout.Add(destroyDurationContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_SpawnedObject", verticalLayout);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}