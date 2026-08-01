/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Time;
    using Opsive.GraphDesigner.Editor.Elements;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Editor.UIElements.Controls;
    using Opsive.Shared.Editor.UIElements.Controls.Types;
    using UnityEngine.UIElements;

    /// <summary>
    /// Implements TypeControlBase for the Wait/SharedWait type.
    /// </summary>
    [ControlType(typeof(Wait))]
    [ControlType(typeof(SharedWait))]
    public class WaitControl : TypeControlBase
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
            var randomDurationContainer = new VisualElement();
            var randomDuration = false;

            var index = -1;
            var logicNode = input.Target as ILogicNode;
            if (logicNode == null) {
                logicNode = ((object[])input.UserData)[0] as ILogicNode; // SharedWait will be contained within the StackedTask.
                if (logicNode is StackedTask stackedTask) {
                    for (int i = 0; i < stackedTask.Nodes.Length; ++i) {
                        if (input.Target == stackedTask.Nodes[i]) {
                            index = i;
                            break;
                        }
                    }
                }
            }
            FieldInspectorView.AddField(input.UnityObject, input.Target, "m_Duration", (VisualElement element, System.Reflection.FieldInfo field) =>
            {
                container.Add(new WatchedContent(logicNode, index, field, element));
            }, (object obj) => { input.OnChangeEvent(obj); });
            FieldInspectorView.AddField(input.UnityObject, input.Target, "m_RandomDuration", (VisualElement element, System.Reflection.FieldInfo field) =>
            {
                container.Add(new WatchedContent(logicNode, index, field, element));
            }, (object obj) => {
                randomDuration = UsesRandomDuration(obj);
                randomDurationContainer.style.display = randomDuration ? DisplayStyle.Flex : DisplayStyle.None;
                input.OnChangeEvent(obj); 
            });

            container.Add(randomDurationContainer);
            FieldInspectorView.AddField(input.UnityObject, input.Target, "m_Seed", (VisualElement element, System.Reflection.FieldInfo field) =>
            {
                randomDurationContainer.Add(new WatchedContent(logicNode, index, field, element));
            }, (object obj) => { input.OnChangeEvent(obj); });
            FieldInspectorView.AddField(input.UnityObject, input.Target, "m_RandomDurationRange", (VisualElement element, System.Reflection.FieldInfo field) =>
            {
                randomDurationContainer.Add(new WatchedContent(logicNode, index, field, element));
            }, (object obj) => { input.OnChangeEvent(obj); });

            randomDuration = UsesRandomDuration(input.Target);
            randomDurationContainer.style.display = randomDuration ? DisplayStyle.Flex : DisplayStyle.None;

            return container;
        }

        /// <summary>
        /// Returns true if the target should use a random wait duration.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <returns>True if the task uses a random wait duration.</returns>
        private bool UsesRandomDuration(object target)
        {
            if (target is SharedWait sharedWait) {
                return sharedWait.RandomDuration.Value;
            }

            return ((Wait)target).RandomDuration.Value;
        }
    }
}