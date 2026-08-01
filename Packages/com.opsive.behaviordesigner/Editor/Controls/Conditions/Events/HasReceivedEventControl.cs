/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Conditions.Events
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Conditions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using HasReceivedEventCondition = Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.HasReceivedEvent;

    /// <summary>
    /// Implements TypeControlBase for the HasReceivedEvent conditional.
    /// </summary>
    [ControlType(typeof(HasReceivedEventCondition))]
    public class HasReceivedEventControl : ConditionalTypeControlBase<HasReceivedEventCondition>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, HasReceivedEventCondition target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var targetContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                ConditionalFieldControlUtility.SetDisplay(targetContainer, !ConditionalFieldControlUtility.GetValue(target, "m_GlobalEvent", false));
            }

            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_EventName", verticalLayout);
            var refreshGlobalEvent = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_GlobalEvent", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_GlobalEvent", verticalLayout, (object obj) => {
                refreshGlobalEvent?.Invoke();
                UpdateVisibility();
            });
            ConditionalConditionControlBuilder.AddFields(input, target, targetContainer, "m_TargetGameObject", "m_TreeIndex");
            verticalLayout.Add(targetContainer);
            ConditionalConditionControlBuilder.AddFields(input, target, verticalLayout, "m_StoredValue1", "m_StoredValue2", "m_StoredValue3");
            UpdateVisibility();
            return verticalLayout;
        }
    }
}