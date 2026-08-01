/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Conditions
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.Shared.Editor.UIElements.Controls.Types;
    using UnityEngine.UIElements;

    /// <summary>
    /// Shared builders used by the conditional condition controls.
    /// </summary>
    internal static class ConditionalConditionControlBuilder
    {
        /// <summary>
        /// Adds the specified watched fields to the container.
        /// </summary>
        /// <typeparam name="TCondition">The condition type.</typeparam>
        /// <param name="input">The input to the control.</param>
        /// <param name="condition">The condition being inspected.</param>
        /// <param name="container">The container that receives the fields.</param>
        /// <param name="fieldNames">The field names to add.</param>
        public static void AddFields<TCondition>(TypeControlBase.TypeControlInput input, TCondition condition, VisualElement container, params string[] fieldNames) where TCondition : class
        {
            if (fieldNames == null) {
                return;
            }

            for (int i = 0; i < fieldNames.Length; ++i) {
                ConditionalFieldControlUtility.AddWatchedField(input, condition, fieldNames[i], container);
            }
        }

        /// <summary>
        /// Builds a control that shows a secondary field group when the primary field requires it.
        /// </summary>
        /// <typeparam name="TCondition">The condition type.</typeparam>
        /// <param name="input">The input to the control.</param>
        /// <param name="condition">The condition being inspected.</param>
        /// <param name="leadingFieldNames">The fields shown before the conditional field.</param>
        /// <param name="primaryFieldName">The field that controls the conditional visibility.</param>
        /// <param name="showConditional">The predicate that determines whether conditional fields should be shown.</param>
        /// <param name="conditionalFieldNames">The fields shown within the conditional container.</param>
        /// <param name="trailingFieldNames">The fields shown after the conditional container.</param>
        /// <returns>The created control.</returns>
        public static VisualElement BuildSimpleConditionalControl<TCondition>(TypeControlBase.TypeControlInput input, TCondition condition, string[] leadingFieldNames, string primaryFieldName,
            System.Func<bool> showConditional, string[] conditionalFieldNames, params string[] trailingFieldNames) where TCondition : class
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var conditionalContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                ConditionalFieldControlUtility.SetDisplay(conditionalContainer, showConditional());
            }

            AddFields(input, condition, verticalLayout, leadingFieldNames);
            ConditionalFieldControlUtility.AddWatchedField(input, condition, primaryFieldName, verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            AddFields(input, condition, conditionalContainer, conditionalFieldNames);
            verticalLayout.Add(conditionalContainer);
            AddFields(input, condition, verticalLayout, trailingFieldNames);

            UpdateVisibility();
            return verticalLayout;
        }
    }
}