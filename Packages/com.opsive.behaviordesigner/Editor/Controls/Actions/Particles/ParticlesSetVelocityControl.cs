/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Particles
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using ParticlesSetVelocityAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.ParticlesTasks.SetVelocity;

    /// <summary>
    /// Implements TypeControlBase for the Particles SetVelocity task.
    /// </summary>
    [ControlType(typeof(ParticlesSetVelocityAction))]
    public class ParticlesSetVelocityControl : ConditionalTypeControlBase<ParticlesSetVelocityAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, ParticlesSetVelocityAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var variationContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var velocityMode = ConditionalFieldControlUtility.GetEnumValue(target, "m_VelocityMode", ParticlesSetVelocityAction.VelocityMode.Linear);
                ConditionalFieldControlUtility.SetDisplay(variationContainer, velocityMode == ParticlesSetVelocityAction.VelocityMode.Random);
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_TargetGameObject");
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_VelocityMode", verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Velocity", verticalLayout);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_VelocityVariation", variationContainer);
            verticalLayout.Add(variationContainer);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}