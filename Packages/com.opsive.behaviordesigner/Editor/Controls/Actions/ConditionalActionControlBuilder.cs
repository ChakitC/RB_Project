/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.Shared.Editor.UIElements.Controls.Types;
    using UnityEngine.UIElements;
    /// <summary>
    /// Shared builders used by the conditional action controls.
    /// </summary>
    internal static class ConditionalActionControlBuilder
    {
        /// <summary>
        /// Adds the specified watched fields to the container.
        /// </summary>
        /// <typeparam name="TTask">The task type.</typeparam>
        /// <param name="input">The input to the control.</param>
        /// <param name="task">The task being inspected.</param>
        /// <param name="container">The container that receives the fields.</param>
        /// <param name="fieldNames">The field names to add.</param>
        public static void AddFields<TTask>(TypeControlBase.TypeControlInput input, TTask task, VisualElement container, params string[] fieldNames) where TTask : class
        {
            if (fieldNames == null) {
                return;
            }

            for (int i = 0; i < fieldNames.Length; ++i) {
                ConditionalFieldControlUtility.AddWatchedField(input, task, fieldNames[i], container);
            }
        }

        /// <summary>
        /// Builds a control that shows a secondary field group when the primary field requires it.
        /// </summary>
        /// <typeparam name="TTask">The task type.</typeparam>
        /// <param name="input">The input to the control.</param>
        /// <param name="task">The task being inspected.</param>
        /// <param name="leadingFieldNames">The fields shown before the conditional field.</param>
        /// <param name="primaryFieldName">The field that controls conditional visibility.</param>
        /// <param name="showConditionalFields">The predicate that determines whether conditional fields are shown.</param>
        /// <param name="conditionalFieldNames">The fields shown in the conditional container.</param>
        /// <param name="trailingFieldNames">The fields shown after the conditional container.</param>
        /// <returns>The created control.</returns>
        public static VisualElement BuildConditionalControl<TTask>(TypeControlBase.TypeControlInput input, TTask task, string[] leadingFieldNames, string primaryFieldName,
            System.Func<TTask, bool> showConditionalFields, string[] conditionalFieldNames, params string[] trailingFieldNames) where TTask : class
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var conditionalContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                ConditionalFieldControlUtility.SetDisplay(conditionalContainer, showConditionalFields(task));
            }

            AddFields(input, task, verticalLayout, leadingFieldNames);
            var refreshBinding = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, task, primaryFieldName, UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, task, primaryFieldName, verticalLayout, (object obj) => {
                refreshBinding?.Invoke();
                UpdateVisibility();
            });
            AddFields(input, task, conditionalContainer, conditionalFieldNames);
            verticalLayout.Add(conditionalContainer);
            AddFields(input, task, verticalLayout, trailingFieldNames);
            UpdateVisibility();
            return verticalLayout;
        }

        /// <summary>
        /// Builds a control that falls back to secondary fields when the target reference is not assigned.
        /// </summary>
        /// <typeparam name="TTask">The task type.</typeparam>
        /// <param name="input">The input to the control.</param>
        /// <param name="task">The task being inspected.</param>
        /// <param name="leadingFieldNames">The fields shown before the target field.</param>
        /// <param name="targetFieldName">The target field that controls fallback visibility.</param>
        /// <param name="fallbackFieldNames">The fields shown when the target field is not assigned.</param>
        /// <param name="trailingFieldNames">The fields shown after the fallback container.</param>
        /// <returns>The created control.</returns>
        public static VisualElement BuildTargetFallbackControl<TTask>(TypeControlBase.TypeControlInput input, TTask task, string[] leadingFieldNames, string targetFieldName,
            string[] fallbackFieldNames, params string[] trailingFieldNames) where TTask : class
        {
            return BuildConditionalControl(input, task, leadingFieldNames, targetFieldName,
                (TTask currentTask) => !ConditionalFieldControlUtility.HasAssignedReference(currentTask, targetFieldName),
                fallbackFieldNames, trailingFieldNames);
        }

        /// <summary>
        /// Builds a control that falls back to secondary fields when the string field is null or empty.
        /// </summary>
        /// <typeparam name="TTask">The task type.</typeparam>
        /// <param name="input">The input to the control.</param>
        /// <param name="task">The task being inspected.</param>
        /// <param name="leadingFieldNames">The fields shown before the string field.</param>
        /// <param name="stringFieldName">The string field that controls fallback visibility.</param>
        /// <param name="fallbackFieldNames">The fields shown when the string field is null or empty.</param>
        /// <param name="trailingFieldNames">The fields shown after the fallback container.</param>
        /// <returns>The created control.</returns>
        public static VisualElement BuildStringFallbackControl<TTask>(TypeControlBase.TypeControlInput input, TTask task, string[] leadingFieldNames, string stringFieldName,
            string[] fallbackFieldNames, params string[] trailingFieldNames) where TTask : class
        {
            return BuildConditionalControl(input, task, leadingFieldNames, stringFieldName,
                (TTask currentTask) => ConditionalFieldControlUtility.IsNullOrEmpty(currentTask, stringFieldName),
                fallbackFieldNames, trailingFieldNames);
        }
    }
}