/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls
{
    using Opsive.BehaviorDesigner.Editor.Inspectors;
    using Opsive.BehaviorDesigner.Runtime;
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Editor.UIElements.Controls;
    using Opsive.Shared.Editor.UIElements.Controls.Attributes;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Implements AttributeControlBase for the SubtreeListAttribute attribute.
    /// </summary>
    [ControlType(typeof(SubtreeListAttribute))]
    public class SubtreeListAttributeControl : AttributeControlBase
    {
        /// <summary>
        /// VisualElement for showing the Subtree list.
        /// </summary>
        public class SubtreeElement : VisualElement
        {
            private ReorderableList m_ReorderableList;

            public IList ItemsSource => m_ReorderableList.ItemsSource;

            /// <summary>
            /// SubtreeElement constructor.
            /// </summary>
            /// <param name="input">The input to the control.</param>
            public SubtreeElement(AttributeControlInput input)
            {
                var subtreeList = input.Value as IList<Subtree>;
                m_ReorderableList = new ReorderableList(subtreeList as IList, (VisualElement parent, int index) => // Make item.
                {
                    parent.Add(new SubtreeListElement(this, (object value) =>
                    {
                        input.Field.SetValue(input.Target, value);
                        return input.OnChangeEvent(value);
                    }));
                }, (VisualElement parent, int index) => // Bind item.
                {
                    var variableElement = parent.ElementAt(0) as SubtreeListElement;
                    variableElement.BindToVariable(index, (Subtree)m_ReorderableList.ItemsSource[index]);
                }, (VisualElement parent) => // Header.
                {
                    parent.Add(new Label("Subtrees"));
                }, (int index) => // Element height.
                {
                    return 22;
                }, null, 
                () => // Add.
                {
                    if (input.Field.FieldType.IsArray) {
                        if (subtreeList == null) {
                            subtreeList = Array.CreateInstance(typeof(Subtree), 1) as IList<Subtree>;
                        } else {
                            var variableArray = (Subtree[])subtreeList;
                            Array.Resize(ref variableArray, subtreeList.Count + 1);
                            subtreeList = variableArray;
                        }
                    } else {
                        if (subtreeList == null) {
                            subtreeList = Activator.CreateInstance(typeof(List<>).MakeGenericType(typeof(Subtree))) as IList<Subtree>;
                        }
                        subtreeList.Add(null);
                    }
                    m_ReorderableList.Refresh((IList)subtreeList);
                    input.OnChangeEvent(subtreeList);
                }, 
                (int index) => // Remove.
                {
                    if (input.Field.FieldType.IsArray) {
                        var variableArray = (Subtree[])subtreeList;
                        ArrayUtility.RemoveAt(ref variableArray, index);
                        subtreeList = variableArray;
                    } else {
                        subtreeList.RemoveAt(index);
                    }
                    m_ReorderableList.Refresh((IList)subtreeList);
                    input.OnChangeEvent(subtreeList);
                }, (int fromIndex, int toIndex) => // Rearrange.
                {
                    input.OnChangeEvent(subtreeList);
                });
                Add(m_ReorderableList);
            }
        }

        /// <summary>
        /// Single row within the Subtree ReorderableList.
        /// </summary>
        private class SubtreeListElement : VisualElement
        {
            private SubtreeElement m_SubtreeElement;
            private Func<object, bool> m_OnChangeEvent;

            private int m_Index;

            /// <summary>
            /// SubtreeListElement constructor.
            /// </summary>
            /// <param name="subtreeElement">The parent SubtreeElement.</param>
            /// <param name="onChangeEvent">An event that is sent when the value changes. Returns false if the control cannot be changed.</param>
            public SubtreeListElement(SubtreeElement subtreeElement, Func<object, bool> onChangeEvent)
            {
                m_SubtreeElement = subtreeElement;
                m_OnChangeEvent = onChangeEvent;
            }

            /// <summary>
            /// Binds the row element to the specified index and value.
            /// </summary>
            /// <param name="index">The index of the Subtree.</param>
            /// <param name="value">The Subtree reference.</param>
            public void BindToVariable(int index, Subtree value)
            {
                Clear();
                m_Index = index;

                var subtreeField = new ObjectField();
                subtreeField.objectType = typeof(Subtree);
                subtreeField.value = value;
                subtreeField.RegisterValueChangedCallback(c =>
                {
                    m_SubtreeElement.ItemsSource[m_Index] = (Subtree)c.newValue;
                    m_OnChangeEvent(m_SubtreeElement.ItemsSource);

                    VariableUtility.SyncReferenceVariables(BehaviorDesignerWindow.Instance.Graph);
                });
                Add(subtreeField);
            }
        }

        /// <summary>
        /// Does the control use a label?
        /// </summary>
        public override bool UseLabel { get { return false; } }

        /// <summary>
        /// Does the attribute override the type control?
        /// </summary>
        public override bool OverrideTypeControl => true;

        /// <summary>
        /// Returns the attribute control that should be used for the specified AttributeControlType.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(AttributeControlInput input)
        {
            if (!typeof(IList<Subtree>).IsAssignableFrom(input.Field.FieldType)) {
                Debug.LogError("Error: The SubtreeListAttribute can only be used on lists of Subtrees.");
                return null;
            }

            return new SubtreeElement(input);
        }
    }
}