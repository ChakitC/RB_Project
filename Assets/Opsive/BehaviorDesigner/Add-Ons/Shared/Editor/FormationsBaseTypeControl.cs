/// ---------------------------------------------
/// Shared Add-On for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.Shared.Editor
{
    using Opsive.BehaviorDesigner.AddOns.Shared.Runtime.Tasks;
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Editor.UIElements.Controls;
    using System;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Implements TypeControlBase for the FormationsBase type.
    /// </summary>
    [ControlType(typeof(FormationsBase))]
    public class FormationsBaseTypeControl : PathfinderTypeControlBase
    {
        /// <summary>
        /// The FormationsBaseView displays the contents of the FormationsBase.
        /// </summary>
        public class FormationsBaseView : PathfinderView
        {
            protected FormationsBase m_FormationsBase;

            /// <summary>
            /// FormationsBaseView constructor.
            /// </summary>
            /// <param name="unityObject">A reference to the owning Unity Object.</param>
            /// <param name="formationsBase">The FormationsBase task being drawn.</param>
            /// <param name="onChangeEvent">An event that is sent when the value changes. Returns false if the control cannot be changed.</param>
            public FormationsBaseView(UnityEngine.Object unityObject, FormationsBase formationsBase, Func<object, bool> onChangeEvent) : base(unityObject, onChangeEvent, FormationsBase.PathfinderTypeKey)
            {
                m_FormationsBase = formationsBase;

                InitializePathfinder(
                    () => m_FormationsBase.Pathfinder,
                    (pathfinder) => m_FormationsBase.Pathfinder = pathfinder
                );

                AddTaskFields();
            }

            /// <summary>
            /// Adds the task fields.
            /// </summary>
            protected virtual void AddTaskFields()
            {
                // Formation Group.
                var formationGroupContainer = new VisualElement();
                AddHeader(formationGroupContainer, "Formation Group");
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_Is2D", formationGroupContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_FormationGroupID", formationGroupContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_ForceLeader", formationGroupContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                Add(formationGroupContainer);

                // Leader.
                var leaderContainer = new VisualElement();
                AddHeader(leaderContainer, "Leader");
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_TargetPosition", leaderContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                var specifiedDirectionContainer = new VisualElement();
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_FormationDirection", leaderContainer, (object obj) => { 
                    m_OnChangeEvent?.Invoke(obj); 
                    specifiedDirectionContainer.style.display = m_FormationsBase.FormationDirection == FormationsBase.OrientationDirection.Specified ? DisplayStyle.Flex : DisplayStyle.None;
                });
                leaderContainer.Add(specifiedDirectionContainer);
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_SpecifiedDirection", specifiedDirectionContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                specifiedDirectionContainer.style.display = m_FormationsBase.FormationDirection == FormationsBase.OrientationDirection.Specified ? DisplayStyle.Flex : DisplayStyle.None;
                if (m_FormationsBase.CanMoveIntoInitialFormation) {
                    FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_MoveToInitialFormation", leaderContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                }
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_FailOnAgentRemoval", leaderContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_UpdateUnitLocationsOnAgentRemoval", leaderContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                Add(leaderContainer);

                // Formation.
                var formationContainer = new VisualElement();
                AddHeader(formationContainer, "Formation");
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_RotationSpeed", formationContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_RotationThreshold", formationContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_OutOfRangeDistanceDelta", formationContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_InRangeDistanceDelta", formationContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_OutOfRangeSpeedMultiplier", formationContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_StuckDuration", formationContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, "m_StopOnTaskEnd", formationContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                AddTaskFields(formationContainer);
                Add(formationContainer);
            }

            /// <summary>
            /// Adds a header to the specified container.
            /// </summary>
            /// <param name="container">The container to add the header to.</param>
            /// <param name="text">The header text.</param>
            protected void AddHeader(VisualElement container, string text)
            {
                var header = new Label(text);
                header.text = $"<font-weight=700><size=+1>{text}</size></font-weight>";
                header.style.unityTextAlign = TextAnchor.MiddleCenter;
                header.style.marginTop = 5f;
                container.Add(header);
            }

            /// <summary>
            /// Adds task-specific fields.
            /// </summary>
            protected virtual void AddTaskFields(VisualElement container)
            {
                // Get all fields from the derived type. The FormationBase fields have already been added.
                var fields = m_FormationsBase.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                for (int i = 0; i < fields.Length; ++i) {
                    var field = fields[i];
                    if (field.DeclaringType.IsAbstract) {
                        continue;
                    }
                    
                    // Only show private fields that have the SerializeField attribute
                    if ((field.IsPrivate || field.IsFamily) && !Attribute.IsDefined(field, typeof(UnityEngine.SerializeField))) {
                        continue;
                    }

                    FieldInspectorView.AddField(m_UnityObject, m_FormationsBase, field.Name, container, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                }
            }
        }

        /// <summary>
        /// Returns the control that should be used for the specified ControlType.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input)
        {
            return new FormationsBaseView(input.UnityObject, input.Value as FormationsBase, input.OnChangeEvent);
        }
    }
} 