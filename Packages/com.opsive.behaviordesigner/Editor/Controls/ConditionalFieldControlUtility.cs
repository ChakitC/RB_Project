/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.GraphDesigner.Editor.Elements;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Editor.UIElements.Controls.Types;
    using System.Collections;
    using System.Reflection;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Shared helpers for custom controls that conditionally show fields.
    /// </summary>
    internal static class ConditionalFieldControlUtility
    {
        private const BindingFlags c_BindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// Creates the root layout used by custom controls.
        /// </summary>
        /// <returns>The created layout.</returns>
        public static VisualElement CreateVerticalLayout()
        {
            var verticalLayout = new VisualElement();
            verticalLayout.AddToClassList("vertical-layout");
            verticalLayout.AddToClassList("flex-grow");
            return verticalLayout;
        }

        /// <summary>
        /// Creates a simple container that can be shown or hidden.
        /// </summary>
        /// <returns>The created container.</returns>
        public static VisualElement CreateContainer()
        {
            return new VisualElement();
        }

        /// <summary>
        /// Adds a watched field to the specified container.
        /// </summary>
        /// <param name="input">The type control input.</param>
        /// <param name="target">The target task.</param>
        /// <param name="fieldName">The serialized field name.</param>
        /// <param name="container">The container that should receive the field.</param>
        /// <param name="onChangeEvent">The optional change callback.</param>
        public static void AddWatchedField(TypeControlBase.TypeControlInput input, object target, string fieldName, VisualElement container, System.Action<object> onChangeEvent = null)
        {
            GetWatchedFieldTarget(input, out var node, out var index);
            FieldInspectorView.AddField(input.UnityObject, target, fieldName, (VisualElement element, FieldInfo field) => {
                container.Add(new WatchedContent(node, index, field, element));
            }, (object obj) => {
                onChangeEvent?.Invoke(obj);
                input.OnChangeEvent?.Invoke(obj);
            }, searchBaseTypes: true, container: container, userData: input.UserData);
        }

        /// <summary>
        /// Shows or hides the specified element.
        /// </summary>
        /// <param name="element">The element to show or hide.</param>
        /// <param name="visible">Should the element be visible?</param>
        public static void SetDisplay(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Returns the raw field value.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="fieldName">The field name.</param>
        /// <returns>The raw field value.</returns>
        public static object GetFieldValue(object target, string fieldName)
        {
            if (target == null) {
                return null;
            }

            var type = target.GetType();
            while (type != null && type != typeof(object)) {
                var fieldInfo = type.GetField(fieldName, c_BindingFlags);
                if (fieldInfo != null) {
                    return fieldInfo.GetValue(target);
                }
                type = type.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Returns the raw value stored within a SharedVariable field.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="fieldName">The field name.</param>
        /// <returns>The SharedVariable value.</returns>
        public static object GetSharedFieldValue(object target, string fieldName)
        {
            return (GetFieldValue(target, fieldName) as SharedVariable)?.GetValue();
        }

        /// <summary>
        /// Returns the value stored within the field. SharedVariable fields return their value.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="target">The target object.</param>
        /// <param name="fieldName">The field name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>The stored field value.</returns>
        public static T GetValue<T>(object target, string fieldName, T defaultValue = default)
        {
            var fieldValue = GetFieldValue(target, fieldName);
            if (fieldValue is T typedValue) {
                return typedValue;
            }

            if (fieldValue is SharedVariable sharedVariable && sharedVariable.GetValue() is T sharedTypedValue) {
                return sharedTypedValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Returns the selected enum value.
        /// </summary>
        /// <typeparam name="TEnum">The enum type.</typeparam>
        /// <param name="target">The target object.</param>
        /// <param name="fieldName">The field name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>The selected enum value.</returns>
        public static TEnum GetEnumValue<TEnum>(object target, string fieldName, TEnum defaultValue) where TEnum : struct
        {
            var fieldValue = GetFieldValue(target, fieldName);
            if (fieldValue is TEnum enumValue) {
                return enumValue;
            }

            if (fieldValue is SharedVariable sharedVariable && sharedVariable.GetValue() is TEnum sharedEnumValue) {
                return sharedEnumValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Returns true if the SharedVariable field contains a non-null reference or shared reference.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="fieldName">The field name.</param>
        /// <returns>True if the field has an assigned reference.</returns>
        public static bool HasAssignedReference(object target, string fieldName)
        {
            var sharedVariable = GetFieldValue(target, fieldName) as SharedVariable;
            if (sharedVariable == null) {
                return false;
            }

            if (sharedVariable.IsShared) {
                return true;
            }

            var value = sharedVariable.GetValue();
            if (value is UnityEngine.Object unityObject) {
                return unityObject != null;
            }

            return value != null;
        }

        /// <summary>
        /// Returns true if the SharedVariable field contains a non-empty array or list.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="fieldName">The field name.</param>
        /// <returns>True if the collection has at least one item.</returns>
        public static bool HasListItems(object target, string fieldName)
        {
            var value = GetSharedFieldValue(target, fieldName) as IList;
            return value != null && value.Count > 0;
        }

        /// <summary>
        /// Returns true if the SharedVariable string field is null or empty.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="fieldName">The field name.</param>
        /// <returns>True if the string is null or empty.</returns>
        public static bool IsNullOrEmpty(object target, string fieldName)
        {
            return string.IsNullOrEmpty(GetValue<string>(target, fieldName));
        }

        /// <summary>
        /// Watches the SharedVariable instance stored in the specified field.
        /// </summary>
        /// <param name="root">The root visual element.</param>
        /// <param name="target">The target object.</param>
        /// <param name="fieldName">The SharedVariable field name.</param>
        /// <param name="onValueChanged">The callback invoked when the value changes.</param>
        /// <returns>An action that refreshes the watched SharedVariable binding.</returns>
        public static System.Action WatchSharedVariableField(VisualElement root, object target, string fieldName, System.Action onValueChanged)
        {
            SharedVariable watchedVariable = null;

            void HandleValueChanged()
            {
                if (root.panel != null) {
                    root.schedule.Execute(onValueChanged).StartingIn(0);
                } else {
                    onValueChanged();
                }
            }

            void RefreshBinding()
            {
                var newVariable = GetFieldValue(target, fieldName) as SharedVariable;
                if (ReferenceEquals(newVariable, watchedVariable)) {
                    return;
                }

                if (watchedVariable != null) {
                    watchedVariable.OnValueChange -= HandleValueChanged;
                }

                watchedVariable = newVariable;
                if (watchedVariable != null) {
                    watchedVariable.OnValueChange += HandleValueChanged;
                }
            }

            RefreshBinding();
            root.RegisterCallback<DetachFromPanelEvent>(evt => {
                if (watchedVariable != null) {
                    watchedVariable.OnValueChange -= HandleValueChanged;
                    watchedVariable = null;
                }
            });

            return RefreshBinding;
        }

        /// <summary>
        /// Returns the field watch owner for the task.
        /// </summary>
        /// <param name="input">The type control input.</param>
        /// <param name="node">The logic node that owns the field.</param>
        /// <param name="index">The stacked node index.</param>
        private static void GetWatchedFieldTarget(TypeControlBase.TypeControlInput input, out ILogicNode node, out int index)
        {
            index = -1;
            node = input.Target as ILogicNode;
            if (node != null) {
                return;
            }

            if (input.UserData is object[] userDataArray && userDataArray.Length > 0) {
                node = userDataArray[0] as ILogicNode;
            } else {
                node = input.UserData as ILogicNode;
            }

            if (node is IContainerNode containerNode && containerNode.Nodes != null) {
                for (int i = 0; i < containerNode.Nodes.Length; ++i) {
                    if (ReferenceEquals(input.Target, containerNode.Nodes[i])) {
                        index = i;
                        break;
                    }
                }
            }
        }
    }

/// <summary>
    /// Base class for conditional controls with a strongly typed target.
    /// </summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    public abstract class ConditionalTypeControlBase<TTarget> : TypeControlBase where TTarget : class
    {
        /// <summary>
        /// Does the control use a label?
        /// </summary>
        public override bool UseLabel => true;

        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <returns>The created control.</returns>
        protected sealed override VisualElement GetControl(TypeControlInput input)
        {
            var typedTarget = input.Target as TTarget;
            if (typedTarget == null) {
                return new Label($"Error: Target is not a {typeof(TTarget).Name}.");
            }

            return GetControl(input, typedTarget);
        }

        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected abstract VisualElement GetControl(TypeControlInput input, TTarget target);
    }
}