/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls
{
    using Opsive.BehaviorDesigner.Editor.Inspectors;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Editor.Controls;
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Editor.UIElements.Controls;
    using Opsive.Shared.Editor.UIElements.Controls.Types;
    using UnityEngine.UIElements;

    /// <summary>
    /// Implements AttributeControlBase for the SubtreeReference type.
    /// </summary>
    [ControlType(typeof(SubtreeReference))]
    public class SubtreeReferenceControl : TypeControlBase
    {
        /// <summary>
        /// Does the control use a label?
        /// </summary>
        public override bool UseLabel { get { return false; } }

        /// <summary>
        /// Returns the control that should be used for the specified ControlType.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input)
        {
            var container = new VisualElement();

            // Sync the mapped variable references when the external tree changes.
            var subTreeReference = input.Value as SubtreeReference;
            FieldInspectorView.AddField(input.UnityObject, subTreeReference, "m_Subtrees", container, (object obj) =>
            {
                VariableUtility.SyncReferenceVariables(input.UnityObject as IGraph);

                // Explicitly refresh the variabels for any SharedVariableOverrideElement objects.
                var sharedVariableOverrideElement = container.Q<SharedVariableOverridesListAttributeControl.SharedVariableOverrideElement>();
                if (sharedVariableOverrideElement != null) {
                    sharedVariableOverrideElement.RefreshVariables(subTreeReference.SharedVariableOverrides);
                }

                input.OnChangeEvent(obj);

            });
            FieldInspectorView.AddField(input.UnityObject, subTreeReference, "m_Variables", container, (object obj) => { input.OnChangeEvent(obj); });

            return container;
        }
    }
}