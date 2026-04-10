#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.NodeViews
{
    using Opsive.BehaviorDesigner.Runtime;
    using Opsive.BehaviorDesigner.Runtime.Systems;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Decorators;
    using Opsive.GraphDesigner.Editor;
    using Opsive.GraphDesigner.Editor.Elements;
    using Opsive.GraphDesigner.Editor.Events;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.Shared.Editor.UIElements.Controls;
    using Unity.Entities;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Implements TypeControlBase for the PriorityEvaluator type.
    /// </summary>
    [ControlType(typeof(PriorityEvaluator))]
    public class PriorityEvaluatorNodeViewControl : TaskNodeViewControl
    {
        private BehaviorTree m_BehaviorTree;
        private ILogicNode m_Node;
        private EditorNode m_EditorNode;
        private ushort m_PriorityEvaluatorComponentIndex = ushort.MaxValue;

        private Label m_PriorityValueLabel;

        /// <summary>
        /// Addes the UIElements for the specified runtime node to the editor Node within the graph.
        /// </summary>
        /// <param name="graphWindow">A reference to the GraphWindow.</param>
        /// <param name="parent">The parent UIElement that should contain the node UIElements.</param>
        /// <param name="node">The node that the control represents.</param>
        public override void AddNodeView(GraphWindow graphWindow, VisualElement parent, object node)
        {
            base.AddNodeView(graphWindow, parent, node);

            if (!Application.isPlaying) {
                return;
            }

            m_BehaviorTree = (graphWindow.AttachedToGraph != null ? graphWindow.AttachedToGraph.Graph : graphWindow.Graph) as BehaviorTree;
            m_Node = node as ILogicNode;
            m_EditorNode = parent.GetFirstAncestorOfType<EditorNode>();

            parent.RegisterCallback<AttachToPanelEvent>(c =>
            {
                GraphEventHandler.RegisterEvent(GraphEventType.WindowUpdate, UpdateUtilityValue);
            });
            parent.RegisterCallback<DetachFromPanelEvent>(c =>
            {
                GraphEventHandler.UnregisterEvent(GraphEventType.WindowUpdate, UpdateUtilityValue);
            });

            m_PriorityValueLabel = new Label();
            m_PriorityValueLabel.style.alignSelf = Align.Center;
            parent.Add(m_PriorityValueLabel);
        }

        /// <summary>
        /// Updates the utility value.
        /// </summary>
        private void UpdateUtilityValue()
        {
            if (m_BehaviorTree == null || m_BehaviorTree.Entity == Entity.Null) {
                return;
            }

            var taskObjectComponents = m_BehaviorTree.World.EntityManager.GetBuffer<TaskObjectComponent>(m_BehaviorTree.Entity);
            if (m_PriorityEvaluatorComponentIndex == ushort.MaxValue) {
                var nodeIndex = m_EditorNode.GetAttachedToGraphNodeIndex();
                if (nodeIndex == ushort.MaxValue) {
                    nodeIndex = m_Node.RuntimeIndex;

                    if (nodeIndex == ushort.MaxValue) {
                        return;
                    }
                }

                // Find the corresponding index of the TaskObject.
                for (int i = 0; i < taskObjectComponents.Length; ++i) {
                    if (taskObjectComponents[i].Index == nodeIndex) {
                        m_PriorityEvaluatorComponentIndex = (ushort)i;
                        break;
                    }
                }

                if (m_PriorityEvaluatorComponentIndex == ushort.MaxValue) {
                    return;
                }
            }

            var priorityEvaluator = m_BehaviorTree.GetTask(taskObjectComponents[m_PriorityEvaluatorComponentIndex].Index) as PriorityEvaluator;
            if (priorityEvaluator == null) {
                return;
            }
            m_PriorityValueLabel.text = "Value: " + priorityEvaluator.GetPriorityValue();
        }
    }
}
#endif