/// ---------------------------------------------
/// Shared Add-On for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.Shared.Editor
{
    using Opsive.BehaviorDesigner.AddOns.TacticalPack.Runtime.Tasks;
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Editor.UIElements.Controls;
    using System;
    using UnityEngine.UIElements;

    /// <summary>
    /// Implements TypeControlBase for the TacticalBase type.
    /// </summary>
    [ControlType(typeof(TacticalBase))]
    public class TacticalBaseTypeControl : FormationsBaseTypeControl
    {
        /// <summary>
        /// The TacticalBaseView displays the contents of the TacticalBase.
        /// </summary>
        public class TacticalBaseView : FormationsBaseView
        {
            /// <summary>
            /// TacticalBaseView constructor.
            /// </summary>
            /// <param name="unityObject">A reference to the owning Unity Object.</param>
            /// <param name="tacticalBase">The TacticalBase task being drawn.</param>
            /// <param name="onChangeEvent">An event that is sent when the value changes. Returns false if the control cannot be changed.</param>
            public TacticalBaseView(UnityEngine.Object unityObject, TacticalBase tacticalBase, Func<object, bool> onChangeEvent) : base(unityObject, tacticalBase, onChangeEvent) { }

            /// <summary>
            /// Adds the task fields.
            /// </summary>
            protected override void AddTaskFields()
            {
                base.AddTaskFields();

                var tacticalContainer = new VisualElement();
                AddHeader(tacticalContainer, "Tactical");
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_Targets", tacticalContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_AttackDelay", tacticalContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_MovingTarget", tacticalContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                base.AddTaskFields(tacticalContainer);
                Add(tacticalContainer);
            }

            /// <summary>
            /// Adds task-specific fields.
            /// </summary>
            protected override void AddTaskFields(VisualElement container)
            {
                // Intentionally left empty to change the order of when the fields are drawn.
            }
        }

        /// <summary>
        /// Returns the control that should be used for the specified ControlType.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input)
        {
            return new TacticalBaseView(input.UnityObject, input.Value as TacticalBase, input.OnChangeEvent);
        }
    }
} 