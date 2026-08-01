/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Events
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using SendEventAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.SendEvent;

    /// <summary>
    /// Implements TypeControlBase for the SendEvent task.
    /// </summary>
    [ControlType(typeof(SendEventAction))]
    public class SendEventControl : ConditionalTypeControlBase<SendEventAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, SendEventAction target)
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
            ConditionalActionControlBuilder.AddFields(input, target, targetContainer, "m_TargetGameObject", "m_TreeIndex");
            verticalLayout.Add(targetContainer);
            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_Argument1", "m_Argument2", "m_Argument3");
            UpdateVisibility();
            return verticalLayout;
        }
    }
}