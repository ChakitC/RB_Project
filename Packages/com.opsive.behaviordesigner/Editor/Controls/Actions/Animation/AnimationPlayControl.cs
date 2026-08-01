/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Animation
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using AnimationPlayAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AnimationTasks.Play;

    /// <summary>
    /// Implements TypeControlBase for the legacy Animation Play task.
    /// </summary>
    [ControlType(typeof(AnimationPlayAction))]
    public class AnimationPlayControl : ConditionalTypeControlBase<AnimationPlayAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, AnimationPlayAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var nameContainer = ConditionalFieldControlUtility.CreateContainer();
            var indexContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var playType = ConditionalFieldControlUtility.GetEnumValue(target, "m_PlayType", Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AnimationTasks.AnimationPlayType.ByName);
                ConditionalFieldControlUtility.SetDisplay(nameContainer, playType == Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AnimationTasks.AnimationPlayType.ByName);
                ConditionalFieldControlUtility.SetDisplay(indexContainer, playType == Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AnimationTasks.AnimationPlayType.ByIndex);
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_TargetGameObject");
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_PlayType", verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_AnimationName", nameContainer);
            verticalLayout.Add(nameContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_AnimationIndex", indexContainer);
            verticalLayout.Add(indexContainer);
            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_PlayMode", "m_WaitForCompletion");
            UpdateVisibility();
            return verticalLayout;
        }
    }
}