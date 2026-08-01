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
    using InstantiateAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks.Instantiate;

    /// <summary>
    /// Implements TypeControlBase for the Instantiate task.
    /// </summary>
    [ControlType(typeof(InstantiateAction))]
    public class InstantiateControl : ConditionalTypeControlBase<InstantiateAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, InstantiateAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var useLocalTransformContainer = ConditionalFieldControlUtility.CreateContainer();
            var positionContainer = ConditionalFieldControlUtility.CreateContainer();
            var useLocationObjectRotationContainer = ConditionalFieldControlUtility.CreateContainer();
            var rotationContainer = ConditionalFieldControlUtility.CreateContainer();
            var scaleContainer = ConditionalFieldControlUtility.CreateContainer();
            var poolingContainer = ConditionalFieldControlUtility.CreateContainer();
            var destroyDurationContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var hasLocation = ConditionalFieldControlUtility.HasAssignedReference(target, "m_Location");
                var useLocationObjectRotation = ConditionalFieldControlUtility.GetValue(target, "m_UseLocationObjectRotation", true);
                ConditionalFieldControlUtility.SetDisplay(useLocalTransformContainer, hasLocation);
                ConditionalFieldControlUtility.SetDisplay(positionContainer, !hasLocation);
                ConditionalFieldControlUtility.SetDisplay(useLocationObjectRotationContainer, hasLocation);
                ConditionalFieldControlUtility.SetDisplay(rotationContainer, !hasLocation || !useLocationObjectRotation);
                ConditionalFieldControlUtility.SetDisplay(scaleContainer, !ConditionalFieldControlUtility.GetValue(target, "m_UsePrefabScale", false));
                ConditionalFieldControlUtility.SetDisplay(poolingContainer, ConditionalFieldControlUtility.GetValue(target, "m_UsePooling", false));
                ConditionalFieldControlUtility.SetDisplay(destroyDurationContainer, ConditionalFieldControlUtility.GetValue(target, "m_AutoDestroy", false));
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_Prefab", "m_Delay");
            var refreshLocation = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_Location", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Location", verticalLayout, (object obj) => {
                refreshLocation?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_UseLocalTransform", useLocalTransformContainer);
            verticalLayout.Add(useLocalTransformContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Position", positionContainer);
            verticalLayout.Add(positionContainer);
            var refreshUseLocationRotation = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_UseLocationObjectRotation", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_UseLocationObjectRotation", useLocationObjectRotationContainer, (object obj) => {
                refreshUseLocationRotation?.Invoke();
                UpdateVisibility();
            });
            verticalLayout.Add(useLocationObjectRotationContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Rotation", rotationContainer);
            verticalLayout.Add(rotationContainer);
            var refreshUsePrefabScale = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_UsePrefabScale", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_UsePrefabScale", verticalLayout, (object obj) => {
                refreshUsePrefabScale?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Scale", scaleContainer);
            verticalLayout.Add(scaleContainer);
            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_Parent");
            var refreshUsePooling = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_UsePooling", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_UsePooling", verticalLayout, (object obj) => {
                refreshUsePooling?.Invoke();
                UpdateVisibility();
            });
            ConditionalActionControlBuilder.AddFields(input, target, poolingContainer, "m_PoolSize", "m_MaxPoolSize");
            verticalLayout.Add(poolingContainer);
            var refreshAutoDestroy = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_AutoDestroy", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_AutoDestroy", verticalLayout, (object obj) => {
                refreshAutoDestroy?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_DestroyDuration", destroyDurationContainer);
            verticalLayout.Add(destroyDurationContainer);
            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_ObjectName", "m_InstantiatedObject");
            UpdateVisibility();
            return verticalLayout;
        }
    }
}