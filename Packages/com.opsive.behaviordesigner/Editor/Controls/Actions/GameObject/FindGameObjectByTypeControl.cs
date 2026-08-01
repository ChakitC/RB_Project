/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.GameObject
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using FindGameObjectByTypeAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks.FindGameObjectByType;

    /// <summary>
    /// Implements TypeControlBase for the FindGameObjectByType task.
    /// </summary>
    [ControlType(typeof(FindGameObjectByTypeAction))]
    public class FindGameObjectByTypeControl : ConditionalTypeControlBase<FindGameObjectByTypeAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, FindGameObjectByTypeAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var referencePositionContainer = ConditionalFieldControlUtility.CreateContainer();
            var rootGameObjectContainer = ConditionalFieldControlUtility.CreateContainer();
            var foundObjectsContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var findAll = ConditionalFieldControlUtility.GetValue(target, "m_FindAll", false);
                ConditionalFieldControlUtility.SetDisplay(referencePositionContainer, !findAll && ConditionalFieldControlUtility.GetValue(target, "m_FindClosest", false));
                ConditionalFieldControlUtility.SetDisplay(rootGameObjectContainer, ConditionalFieldControlUtility.GetValue(target, "m_ChildrenOnly", false));
                ConditionalFieldControlUtility.SetDisplay(foundObjectsContainer, findAll);
            }

            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_ComponentType", verticalLayout);
            var refreshFindClosest = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_FindClosest", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_FindClosest", verticalLayout, (object obj) => {
                refreshFindClosest?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_ReferencePosition", referencePositionContainer);
            verticalLayout.Add(referencePositionContainer);
            var refreshFindAll = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_FindAll", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_FindAll", verticalLayout, (object obj) => {
                refreshFindAll?.Invoke();
                UpdateVisibility();
            });
            var refreshChildrenOnly = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_ChildrenOnly", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_ChildrenOnly", verticalLayout, (object obj) => {
                refreshChildrenOnly?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_RootGameObject", rootGameObjectContainer);
            verticalLayout.Add(rootGameObjectContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_FoundObject", verticalLayout);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_FoundObjects", foundObjectsContainer);
            verticalLayout.Add(foundObjectsContainer);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}