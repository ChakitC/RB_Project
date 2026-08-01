/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Physics2D
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using SetRigidbody2DPropertiesAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Physics2DTasks.SetRigidbody2DProperties;

    /// <summary>
    /// Implements TypeControlBase for the SetRigidbody2DProperties task.
    /// </summary>
    [ControlType(typeof(SetRigidbody2DPropertiesAction))]
    public class SetRigidbody2DPropertiesControl : ConditionalTypeControlBase<SetRigidbody2DPropertiesAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, SetRigidbody2DPropertiesAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var bodyTypeContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                ConditionalFieldControlUtility.SetDisplay(bodyTypeContainer, ConditionalFieldControlUtility.GetValue(target, "m_SetBodyType", false));
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_TargetGameObject", "m_Mass", "m_LinearDrag", "m_AngularDrag", "m_GravityScale",
                "m_FreezePositionX", "m_FreezePositionY", "m_FreezeRotation");
            var refreshSetBodyType = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_SetBodyType", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_SetBodyType", verticalLayout, (object obj) => {
                refreshSetBodyType?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_BodyType", bodyTypeContainer);
            verticalLayout.Add(bodyTypeContainer);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}