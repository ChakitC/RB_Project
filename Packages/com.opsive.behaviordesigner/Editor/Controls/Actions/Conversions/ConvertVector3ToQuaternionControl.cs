/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Conversions
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using ConvertVector3ToQuaternionAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Conversions.ConvertVector3ToQuaternion;

    /// <summary>
    /// Implements TypeControlBase for the ConvertVector3ToQuaternion task.
    /// </summary>
    [ControlType(typeof(ConvertVector3ToQuaternionAction))]
    public class ConvertVector3ToQuaternionControl : ConditionalTypeControlBase<ConvertVector3ToQuaternionAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, ConvertVector3ToQuaternionAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var upVectorContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var conversionType = ConditionalFieldControlUtility.GetEnumValue(target, "m_ConversionType", ConvertVector3ToQuaternionAction.ConversionType.EulerAngles);
                ConditionalFieldControlUtility.SetDisplay(upVectorContainer, conversionType == ConvertVector3ToQuaternionAction.ConversionType.LookRotation);
            }

            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_InputVector", verticalLayout);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_ConversionType", verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_UpVector", upVectorContainer);
            verticalLayout.Add(upVectorContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_OutputQuaternion", verticalLayout);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}