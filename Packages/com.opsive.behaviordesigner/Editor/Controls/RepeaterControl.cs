/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Decorators;
    using Opsive.GraphDesigner.Editor.Elements;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Editor.UIElements.Controls;
    using Opsive.Shared.Editor.UIElements.Controls.Types;
    using UnityEngine.UIElements;

    /// <summary>
    /// Implements TypeControlBase for the Repeater type.
    /// </summary>
    [ControlType(typeof(Repeater))]
    public class RepeaterControl : TypeControlBase
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
            var countContainer = new VisualElement();
            var infiniteRepeat = false;

            var index = -1;
            var logicNode = input.Target as ILogicNode;
            if (logicNode == null) {
                logicNode = input.UserData as ILogicNode;
                if (logicNode is StackedTask stackedTask) {
                    for (int i = 0; i < stackedTask.Nodes.Length; ++i) {
                        if (input.Target == stackedTask.Nodes[i]) {
                            index = i;
                            break;
                        }
                    }
                }
            }
            FieldInspectorView.AddField(input.UnityObject, input.Target, "m_RepeatForever", (VisualElement element, System.Reflection.FieldInfo field) =>
            {
                container.Add(new WatchedContent(logicNode, index, field, element));
            }, (object obj) => {
                infiniteRepeat = ((Repeater)input.Target).RepeatForever.Value;
                countContainer.style.display = infiniteRepeat ? DisplayStyle.None : DisplayStyle.Flex;
                input.OnChangeEvent(obj); });
            FieldInspectorView.AddField(input.UnityObject, input.Target, "m_RepeatCount", (VisualElement element, System.Reflection.FieldInfo field) =>
            {
                countContainer.Add(new WatchedContent(logicNode, index, field, element));
            }, (object obj) => {
                input.OnChangeEvent(obj); 
            });
            container.Add(countContainer);

            FieldInspectorView.AddField(input.UnityObject, input.Target, "m_EndOnFailure", (VisualElement element, System.Reflection.FieldInfo field) =>
            {
                container.Add(new WatchedContent(logicNode, index, field, element));
            }, (object obj) => { input.OnChangeEvent(obj); });

            infiniteRepeat = ((Repeater)input.Target).RepeatForever.Value;
            countContainer.style.display = infiniteRepeat ? DisplayStyle.None : DisplayStyle.Flex;

            return container;
        }
    }
}