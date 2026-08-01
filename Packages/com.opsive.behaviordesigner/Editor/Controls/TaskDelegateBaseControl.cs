/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.GraphDesigner.Editor.Controls;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.Shared.Editor.UIElements.Controls;
    using Opsive.Shared.Editor.UIElements.Controls.Types;
    using Opsive.Shared.Utility;
    using System;
    using System.Reflection;
    using UnityEngine.UIElements;
    using static UnityEditorInternal.VersionControl.ListControl;

    /// <summary>
    /// Implements AttributeControlBase for the TaskDelegate type.
    /// </summary>
    [ControlType(typeof(TaskDelegateBase))]
    public class TaskDelegateBaseControl : TypeControlBase
    {
        private const string c_TargetFieldName = "m_TargetGameObject";
        private const string c_ParameterFieldName = "m_Parameter";
        private const string c_ResultFieldName = "m_Result";

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
            var taskDelegate = input.Target as TaskDelegateBase;

            // Get the type and the method to determine if the method is static.
            var actionType = TypeUtility.GetType(taskDelegate.ReflectedType);
            if (actionType == null) {
                return new Label($"Error: Unable to find the type {taskDelegate.ReflectedType}.");
            }
            Type[] parameterTypes;
            if (taskDelegate.ParameterTypes != null) {
                parameterTypes = new Type[taskDelegate.ParameterTypes.Length];
                for (int i = 0; i < parameterTypes.Length; ++i) {
                    parameterTypes[i] = TypeUtility.GetType(taskDelegate.ParameterTypes[i]);
                    if (parameterTypes[i] == null) {
                        return new Label($"Error: Unable to find the parameter type {taskDelegate.ParameterTypes[i]}.");
                    }
                }
            } else {
                parameterTypes = new Type[0];
            }

            var method = actionType.GetMethod(taskDelegate.MethodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static, null, parameterTypes, null);
            if (method == null) {
                return new Label($"Error: Unable to find the method {taskDelegate.MethodName}.");
            }

            var verticalLayout = new VisualElement();
            verticalLayout.AddToClassList("vertical-layout");
            verticalLayout.AddToClassList("flex-grow");

            var userDataArray = input.UserData as object[];
            var node = userDataArray[0] as ILogicNode;
            var index = (int)userDataArray[1];

            // The owner does not need to be drawn if the method is a static type.
            if (!method.IsStatic) {
                WatchedFieldUtility.AddWatchedField(input.UnityObject, node, taskDelegate, index, c_TargetFieldName, verticalLayout, (object obj) => {
                    input.OnChangeEvent(obj);
                });
            }

            // All of the TaskDelegates will be drawn from the single control type. Add the parameters if they exist.
            var parameters = method.GetParameters();
            if (parameters != null) {
                for (int i = 0; i < parameters.Length; ++i) {
                    AddParameter(input.UnityObject, node, index, taskDelegate, method, i, verticalLayout, input.OnChangeEvent);
                }
            }

            // The method may return a type.
            if (method.ReturnType != typeof(void)) {
                WatchedFieldUtility.AddWatchedField(input.UnityObject, node, taskDelegate, index, c_ResultFieldName, verticalLayout, (object obj) => {
                    input.OnChangeEvent(obj);
                });
            }

            return verticalLayout;
        }

        /// <summary>
        /// Adds the parameter at the specified index to the control.
        /// </summary>
        /// <param name="unityObject">The Unity Object that the target belongs to.</param>
        /// <param name="node">The ILogicNode that contains the parameter.</param>
        /// <param name="nodeIndex">The index of the node.</param>
        /// <param name="actionDelegate">The TaskDelegate that contains the parameters.</param>
        /// <param name="method">The method that has at least one parameter.</param>
        /// <param name="parameterIndex">The index of the parameter.</param>
        /// <param name="parent">The VisualElement that the parameter should be added to.</param>
        /// <param name="onChangeEvent">An event that is sent when the value changes. Returns false if the control cannot be changed.</param>
        private void AddParameter(UnityEngine.Object unityObject, ILogicNode node, int nodeIndex, TaskDelegateBase actionDelegate, MethodInfo method, int parameterIndex, VisualElement parent, Func<object, bool> onChangeEvent)
        {
            // Use WatchedFieldUtility to add the parameter field.
            var fieldName = c_ParameterFieldName + (parameterIndex + 1);
            var childCountBefore = parent.childCount;
            WatchedFieldUtility.AddWatchedField(unityObject, node, actionDelegate, nodeIndex, fieldName, parent, (object obj) => {
                onChangeEvent(obj);
            });

            // Update the label with the reflected parameter name.
            // Find the LabelControl in the most recently added child element.
            var displayName = UnityEditor.ObjectNames.NicifyVariableName(method.GetParameters()[parameterIndex].Name);
            if (parent.childCount > childCountBefore) {
                var lastChild = parent[parent.childCount - 1];
                var labelControl = lastChild.Q<LabelControl>();
                if (labelControl != null) {
                    labelControl.Label.text = displayName;
                } else {
                    // If not found directly, search within the last child's hierarchy.
                    labelControl = lastChild.Query<LabelControl>().First();
                    if (labelControl != null) {
                        labelControl.Label.text = displayName;
                    }
                }
            }
        }
    }
}