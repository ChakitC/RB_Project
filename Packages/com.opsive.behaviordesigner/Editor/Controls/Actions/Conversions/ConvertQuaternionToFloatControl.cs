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
    using ConvertQuaternionToFloatAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Conversions.ConvertQuaternionToFloat;

    /// <summary>
    /// Implements TypeControlBase for the ConvertQuaternionToFloat task.
    /// </summary>
    [ControlType(typeof(ConvertQuaternionToFloatAction))]
    public class ConvertQuaternionToFloatControl : ConditionalTypeControlBase<ConvertQuaternionToFloatAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, ConvertQuaternionToFloatAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var axisContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var conversionType = ConditionalFieldControlUtility.GetEnumValue(target, "m_ConversionType", ConvertQuaternionToFloatAction.ConversionType.Angle);
                ConditionalFieldControlUtility.SetDisplay(axisContainer, conversionType == ConvertQuaternionToFloatAction.ConversionType.Angle);
            }

            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_InputQuaternion", verticalLayout);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_ConversionType", verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Axis", axisContainer);
            verticalLayout.Add(axisContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_OutputValue", verticalLayout);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}