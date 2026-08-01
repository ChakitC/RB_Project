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
    using ReparentChildrenAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks.ReparentChildren;

    /// <summary>
    /// Implements TypeControlBase for the ReparentChildren task.
    /// </summary>
    [ControlType(typeof(ReparentChildrenAction))]
    public class ReparentChildrenControl : ConditionalTypeControlBase<ReparentChildrenAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, ReparentChildrenAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var nameContainer = ConditionalFieldControlUtility.CreateContainer();
            var tagContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                ConditionalFieldControlUtility.SetDisplay(nameContainer, ConditionalFieldControlUtility.GetValue(target, "m_FilterByName", false));
                ConditionalFieldControlUtility.SetDisplay(tagContainer, ConditionalFieldControlUtility.GetValue(target, "m_FilterByTag", false));
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_SourceGameObject", "m_NewParent", "m_WorldPositionStays");
            var refreshFilterByName = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_FilterByName", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_FilterByName", verticalLayout, (object obj) => {
                refreshFilterByName?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_NamePattern", nameContainer);
            verticalLayout.Add(nameContainer);
            var refreshFilterByTag = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_FilterByTag", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_FilterByTag", verticalLayout, (object obj) => {
                refreshFilterByTag?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Tag", tagContainer);
            verticalLayout.Add(tagContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_ReparentedCount", verticalLayout);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}