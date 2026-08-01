/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls
{
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using System.Reflection;

    /// <summary>
    /// Implements AttributeControlBase for the SubtreeReferenceSelector type.
    /// </summary>
    [ControlType(typeof(SubtreeReferenceSelector))]
    public class SubtreeReferenceSelectorControl : SubtreeReferenceControl
    {
        /// <summary>
        /// Returns the control that should be used for the specified ControlType.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input)
        {
            var container = base.GetControl(input);
            var subTreeReference = input.Value as SubtreeReferenceSelector;
            FieldInspectorView.AddField(input.UnityObject, subTreeReference, "m_Index", (VisualElement element, FieldInfo field) =>
            {
                container.Insert(0, element);
            }, (object obj) => { input.OnChangeEvent(obj); });
            FieldInspectorView.AddField(input.UnityObject, subTreeReference, "m_ReevaluateOnIndexChange", (VisualElement element, FieldInfo field) =>
            {
                container.Insert(1, element);
            }, (object obj) => { input.OnChangeEvent(obj); });
            return container;
        }
    }
}