/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls
{
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Variables;
    using Opsive.GraphDesigner.Editor.Elements;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Editor.UIElements.Controls;
    using Opsive.Shared.Editor.UIElements.Controls.Types;
    using System.Reflection;
    using UnityEngine.UIElements;

    /// <summary>
    /// Implements TypeControlBase for scoped SetVariable tasks.
    /// </summary>
    [ControlType(typeof(SetVariableBase))]
    [ControlType(typeof(SetBoolVariable))]
    [ControlType(typeof(SetColorVariable))]
    [ControlType(typeof(SetFloatVariable))]
    [ControlType(typeof(SetGameObjectVariable))]
    [ControlType(typeof(SetIntVariable))]
    [ControlType(typeof(SetListVariable))]
    [ControlType(typeof(SetStringVariable))]
    [ControlType(typeof(SetTransformVariable))]
    [ControlType(typeof(SetVector2Variable))]
    [ControlType(typeof(SetVector3Variable))]
    [ControlType(typeof(SetVector4Variable))]
    public class SetVariableControl : TypeControlBase
    {
        /// <summary>
        /// Does the control use a label?
        /// </summary>
        public override bool UseLabel => true;

        /// <summary>
        /// Returns the control that should be used for the specified ControlType.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input)
        {
            var target = input.Target as SetVariableBase;
            if (target == null) {
                return new Label($"Error: Target is not a {nameof(SetVariableBase)}.");
            }

            var verticalLayout = new VisualElement();
            verticalLayout.AddToClassList("vertical-layout");
            verticalLayout.AddToClassList("flex-grow");

            var targetGameObjectContainer = new VisualElement();
            var treeIndexContainer = new VisualElement();
            GetWatchedFieldTarget(input, out var node, out var index);

            void UpdateVisibility()
            {
                var variableScope = GetVariableScope(target);
                targetGameObjectContainer.style.display =
                    variableScope == SetVariableBase.VariableScope.Graph || variableScope == SetVariableBase.VariableScope.GameObject ? DisplayStyle.Flex : DisplayStyle.None;
                treeIndexContainer.style.display = variableScope == SetVariableBase.VariableScope.Graph ? DisplayStyle.Flex : DisplayStyle.None;
            }

            AddWatchedField(input, node, index, target, "m_VariableScope", verticalLayout, (object obj) => {
                UpdateVisibility();
            });

            AddWatchedField(input, node, index, target, "m_TargetGameObject", targetGameObjectContainer);
            verticalLayout.Add(targetGameObjectContainer);
            AddWatchedField(input, node, index, target, "m_TreeIndex", treeIndexContainer);
            verticalLayout.Add(treeIndexContainer);

            AddWatchedField(input, node, index, target, "m_VariableName", verticalLayout);
            AddWatchedField(input, node, index, target, "m_Value", verticalLayout);

            UpdateVisibility();
            return verticalLayout;
        }

        /// <summary>
        /// Adds a watched field to the specified container.
        /// </summary>
        private static void AddWatchedField(TypeControlInput input, ILogicNode node, int index, object target, string fieldName, VisualElement container, System.Action<object> onChangeEvent = null)
        {
            FieldInspectorView.AddField(input.UnityObject, target, fieldName, (VisualElement element, FieldInfo field) => {
                container.Add(new WatchedContent(node, index, field, element));
            }, (object obj) => {
                onChangeEvent?.Invoke(obj);
                input.OnChangeEvent?.Invoke(obj);
            }, searchBaseTypes: true, container: container, userData: input.UserData);
        }

        /// <summary>
        /// Returns the field watch owner for the task.
        /// </summary>
        private static void GetWatchedFieldTarget(TypeControlInput input, out ILogicNode node, out int index)
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

        /// <summary>
        /// Returns the selected variable scope.
        /// </summary>
        private static SetVariableBase.VariableScope GetVariableScope(SetVariableBase target)
        {
            var type = target.GetType();
            while (type != null && type != typeof(object)) {
                var fieldInfo = type.GetField("m_VariableScope", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (fieldInfo != null) {
                    return fieldInfo.GetValue(target) is SetVariableBase.VariableScope variableScope ? variableScope : SetVariableBase.VariableScope.Graph;
                }
                type = type.BaseType;
            }

            return SetVariableBase.VariableScope.Graph;
        }
    }

    /// <summary>
    /// Implements TypeControlBase for scoped GetVariable tasks.
    /// </summary>
    [ControlType(typeof(GetVariableBase))]
    [ControlType(typeof(GetBoolVariable))]
    [ControlType(typeof(GetColorVariable))]
    [ControlType(typeof(GetFloatVariable))]
    [ControlType(typeof(GetGameObjectVariable))]
    [ControlType(typeof(GetIntVariable))]
    [ControlType(typeof(GetListVariable))]
    [ControlType(typeof(GetStringVariable))]
    [ControlType(typeof(GetTransformVariable))]
    [ControlType(typeof(GetVector2Variable))]
    [ControlType(typeof(GetVector3Variable))]
    [ControlType(typeof(GetVector4Variable))]
    public class GetVariableControl : TypeControlBase
    {
        /// <summary>
        /// Does the control use a label?
        /// </summary>
        public override bool UseLabel => true;

        /// <summary>
        /// Returns the control that should be used for the specified ControlType.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input)
        {
            var target = input.Target as GetVariableBase;
            if (target == null) {
                return new Label($"Error: Target is not a {nameof(GetVariableBase)}.");
            }

            var verticalLayout = new VisualElement();
            verticalLayout.AddToClassList("vertical-layout");
            verticalLayout.AddToClassList("flex-grow");

            var targetGameObjectContainer = new VisualElement();
            var treeIndexContainer = new VisualElement();
            GetWatchedFieldTarget(input, out var node, out var index);

            void UpdateVisibility()
            {
                var variableScope = GetVariableScope(target);
                targetGameObjectContainer.style.display =
                    variableScope == GetVariableBase.VariableScope.Graph || variableScope == GetVariableBase.VariableScope.GameObject ? DisplayStyle.Flex : DisplayStyle.None;
                treeIndexContainer.style.display = variableScope == GetVariableBase.VariableScope.Graph ? DisplayStyle.Flex : DisplayStyle.None;
            }

            AddWatchedField(input, node, index, target, "m_VariableScope", verticalLayout, (object obj) => {
                UpdateVisibility();
            });

            AddWatchedField(input, node, index, target, "m_TargetGameObject", targetGameObjectContainer);
            verticalLayout.Add(targetGameObjectContainer);
            AddWatchedField(input, node, index, target, "m_TreeIndex", treeIndexContainer);
            verticalLayout.Add(treeIndexContainer);

            AddWatchedField(input, node, index, target, "m_VariableName", verticalLayout);
            AddWatchedField(input, node, index, target, "m_Destination", verticalLayout);

            UpdateVisibility();
            return verticalLayout;
        }

        /// <summary>
        /// Adds a watched field to the specified container.
        /// </summary>
        /// <param name="input">The type control input.</param>
        /// <param name="node">The logic node that owns the field.</param>
        /// <param name="index">The stacked node index.</param>
        /// <param name="target">The target object.</param>
        /// <param name="fieldName">The field name.</param>
        /// <param name="container">The container that should receive the field.</param>
        /// <param name="onChangeEvent">The optional change callback.</param>
        private static void AddWatchedField(TypeControlInput input, ILogicNode node, int index, object target, string fieldName, VisualElement container, System.Action<object> onChangeEvent = null)
        {
            FieldInspectorView.AddField(input.UnityObject, target, fieldName, (VisualElement element, FieldInfo field) => {
                container.Add(new WatchedContent(node, index, field, element));
            }, (object obj) => {
                onChangeEvent?.Invoke(obj);
                input.OnChangeEvent?.Invoke(obj);
            }, searchBaseTypes: true, container: container, userData: input.UserData);
        }

        /// <summary>
        /// Returns the field watch owner for the task.
        /// </summary>
        /// <param name="input">The type control input.</param>
        /// <param name="node">The logic node that owns the field.</param>
        /// <param name="index">The stacked node index.</param>
        private static void GetWatchedFieldTarget(TypeControlInput input, out ILogicNode node, out int index)
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

        /// <summary>
        /// Returns the selected variable scope.
        /// </summary>
        /// <param name="target">The GetVariable task.</param>
        /// <returns>The selected variable scope.</returns>
        private static GetVariableBase.VariableScope GetVariableScope(GetVariableBase target)
        {
            var type = target.GetType();
            while (type != null && type != typeof(object)) {
                var fieldInfo = type.GetField("m_VariableScope", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (fieldInfo != null) {
                    return fieldInfo.GetValue(target) is GetVariableBase.VariableScope variableScope ? variableScope : GetVariableBase.VariableScope.Graph;
                }
                type = type.BaseType;
            }

            return GetVariableBase.VariableScope.Graph;
        }
    }
}