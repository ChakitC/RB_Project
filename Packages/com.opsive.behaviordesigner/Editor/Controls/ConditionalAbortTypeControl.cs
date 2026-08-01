/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.Shared.Editor.UIElements.Controls.Types
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.GraphDesigner.Editor.Events;
    using UnityEngine.UIElements;

    /// <summary>
    /// Implements TypeControlBase for the ConditionalAbortType ControlType.
    /// </summary>
    [ControlType(typeof(ConditionalAbortType))]
    public class ConditionalAbortTypeControl : EnumControl
    {
        /// <summary>
        /// Does the control use a label?
        /// </summary>
        public override bool UseLabel { get { return true; } }

        /// <summary>
        /// Returns the control that should be used for the specified ControlType.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input)
        {
            var enumField = base.GetControl(input) as EnumField;
            enumField.RegisterValueChangedCallback(c =>
            {
                GraphEventHandler.Invoke(GraphEventType.NodeValueUpdated, input.Target);
            });
            return enumField;
        }
    }
}