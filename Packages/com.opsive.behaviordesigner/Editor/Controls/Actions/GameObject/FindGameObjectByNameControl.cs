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
    using FindGameObjectByNameAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks.FindGameObjectByName;

    /// <summary>
    /// Implements TypeControlBase for the FindGameObjectByName task.
    /// </summary>
    [ControlType(typeof(FindGameObjectByNameAction))]
    public class FindGameObjectByNameControl : ConditionalTypeControlBase<FindGameObjectByNameAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, FindGameObjectByNameAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var includeInactiveContainer = ConditionalFieldControlUtility.CreateContainer();
            var recursiveContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var searchScope = ConditionalFieldControlUtility.GetEnumValue(target, "m_SearchScope", FindGameObjectByNameAction.SearchScope.EntireScene);
                ConditionalFieldControlUtility.SetDisplay(includeInactiveContainer, searchScope != FindGameObjectByNameAction.SearchScope.ParentOnly);
                ConditionalFieldControlUtility.SetDisplay(recursiveContainer, searchScope == FindGameObjectByNameAction.SearchScope.ChildrenOnly);
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_TargetGameObject", "m_Name");
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_SearchScope", verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_IncludeInactive", includeInactiveContainer);
            verticalLayout.Add(includeInactiveContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Recursive", recursiveContainer);
            verticalLayout.Add(recursiveContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_FoundObject", verticalLayout);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}