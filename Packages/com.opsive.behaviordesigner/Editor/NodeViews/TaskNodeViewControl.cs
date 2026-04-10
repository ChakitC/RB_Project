#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.NodeViews
{
    using Opsive.BehaviorDesigner.Runtime;
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.BehaviorDesigner.Runtime.Groups;
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.GraphDesigner.Editor;
    using Opsive.GraphDesigner.Editor.Controls.NodeViews;
    using Opsive.GraphDesigner.Editor.Elements;
    using Opsive.GraphDesigner.Editor.Events;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Adds UI elements within the task node.
    /// </summary>
    [ControlType(typeof(IAction))]
    [ControlType(typeof(IConditional))]
    [ControlType(typeof(IComposite))]
    [ControlType(typeof(IDecorator))]
    public class TaskNodeViewControl : NodeViewBase
    {
        private const string c_DarkConditionalAbortLowerPriorityIconGUID = "ba6528926e3f4f7438d3b9737f595ec6";
        private const string c_LightConditionalAbortLowerPriorityIconGUID = "20be04a2e46cb9d40b601dccdfbe153b";
        private const string c_DarkConditionalAbortSelfIconGUID = "ff3ba64f23e708645b24cc7509b5ebe5";
        private const string c_LightConditionalAbortSelfIconGUID = "5d44e66bacdbe51408dd30e519c2b318";
        private const string c_DarkConditionalAbortBothIconGUID = "1c01950cc0f1c994cb5ff3576969ebbf";
        private const string c_LightConditionalAbortBothIconGUID = "90b22fc04519bdb44b4d83665e86381d";

        private const string c_DarkSuccessIconGUID = "240eed9b6e6dc004f94216f1e9fcc390";
        private const string c_LightSuccessIconGUID = "cf3f27e8ca1f20f4680890e078c7613a";
        private const string c_DarkSuccessReevaluateIconGUID = "0a5037ce131729b4fa0ffa9e1e13d387";
        private const string c_LightSuccessReevaluateIconGUID = "bba6bdc3af0aac44dadd1ca3a8485b05";
        private const string c_DarkFailureIconGUID = "8d159db7a8da43e41a50a77e43cfd6ba";
        private const string c_LightFailureIconGUID = "c3622912d9f7bcd41a54a95add672423";
        private const string c_DarkFailureReevaluateIconGUID = "26de8afeb313fd84291f98e68db44df7";
        private const string c_LightFailureReevaluateIconGUID = "5a6d713911c8ec3488639e2934329033";

        private ILogicNode m_Node;
        private GraphWindow m_GraphWindow;
        private BehaviorTree m_BehaviorTree;

        private Image m_ExecutionStatusIcon;
        private Image m_ConditionalAbortIcon;
        private LogicNode m_LogicNode;

        private Texture m_SuccessIcon;
        private Texture m_SuccessReevaluateIcon;
        private Texture m_FailureIcon;
        private Texture m_FailureReevaluateIcon;

        private TraversalTaskSystemGroup m_TraversalTaskSystemGroup;
        private int m_LastActiveFrame = -1;

        /// <summary>
        /// Addes the UIElements for the specified runtime node to the editor Node within the graph.
        /// </summary>
        /// <param name="graphWindow">A reference to the GraphWindow.</param>
        /// <param name="parent">The parent UIElement that should contain the node UIElements.</param>
        /// <param name="node">The node that the control represents.</param>
        public override void AddNodeView(GraphWindow graphWindow, VisualElement parent, object node)
        {
            graphWindow.rootVisualElement.styleSheets.Add(Shared.Editor.Utility.EditorUtility.LoadAsset<StyleSheet>("9c6834c10d404ac4b95be745f4411f96")); // TaskStyles.uss

            m_Node = node as ILogicNode;
            m_GraphWindow = graphWindow;
            m_BehaviorTree = (m_GraphWindow.AttachedToGraph != null ? m_GraphWindow.AttachedToGraph.Graph : m_GraphWindow.Graph) as BehaviorTree;
            m_LogicNode = parent.GetFirstAncestorOfType<LogicNode>();

            m_LastActiveFrame = Time.frameCount + 1;

            if (node is IConditionalAbortParent conditionalAbortParent) {
                m_ConditionalAbortIcon = new Image();
                m_ConditionalAbortIcon.name = "conditional-abort-icon";
                m_LogicNode.NodeContainer.Add(m_ConditionalAbortIcon);
                SetConditionalAbortIcon(conditionalAbortParent);

                m_ConditionalAbortIcon.RegisterCallback<AttachToPanelEvent>(c =>
                {
                    GraphEventHandler.RegisterEvent<object>(GraphEventType.NodeValueUpdated, UpdateNodeValue);
                });
                m_ConditionalAbortIcon.RegisterCallback<DetachFromPanelEvent>(c =>
                {
                    GraphEventHandler.UnregisterEvent<object>(GraphEventType.NodeValueUpdated, UpdateNodeValue);
                });
            }

            // Subtree references can click into its references.
            if (m_Node is ISubtreeReferenceNode) {
                m_LogicNode.RegisterCallback<MouseDownEvent>(OnSubtreeReferenceMouseDown);
            }

            // AddNodeView can be called multiple times. Ensure there is only one execution status image.
            var previousExecutionStatus = m_LogicNode.Q("execution-status");
            if (previousExecutionStatus != null) {
                previousExecutionStatus.parent.Remove(previousExecutionStatus);
            }
            m_ExecutionStatusIcon = new Image();
            m_ExecutionStatusIcon.name = "execution-status";
            m_LogicNode.NodeContainer.Add(m_ExecutionStatusIcon); // The execution status icon should be placed behind every node element.
            m_ExecutionStatusIcon.SendToBack();

            m_SuccessIcon = Shared.Editor.Utility.EditorUtility.LoadAsset<Texture>(EditorGUIUtility.isProSkin ? c_DarkSuccessIconGUID : c_LightSuccessIconGUID);
            m_SuccessReevaluateIcon = Shared.Editor.Utility.EditorUtility.LoadAsset<Texture>(EditorGUIUtility.isProSkin ? c_DarkSuccessReevaluateIconGUID : c_LightSuccessReevaluateIconGUID);
            m_FailureIcon = Shared.Editor.Utility.EditorUtility.LoadAsset<Texture>(EditorGUIUtility.isProSkin ? c_DarkFailureIconGUID : c_LightFailureIconGUID);
            m_FailureReevaluateIcon = Shared.Editor.Utility.EditorUtility.LoadAsset<Texture>(EditorGUIUtility.isProSkin ? c_DarkFailureReevaluateIconGUID : c_LightFailureReevaluateIconGUID);

            // Register UpdateNodeInternal to be called in OnPreUpdate when the traversal group becomes available.
            GraphEventHandler.RegisterEvent(GraphEventType.WindowUpdate, UpdateNode);
            if (!TryRegisterUpdateNode() && m_BehaviorTree != null && Application.isPlaying) {
                m_BehaviorTree.OnBehaviorTreeStarted += OnBehaviorTreeStarted;
            }
            m_ExecutionStatusIcon.RegisterCallback<DetachFromPanelEvent>(c => {
                GraphEventHandler.UnregisterEvent(GraphEventType.WindowUpdate, UpdateNode);
                if (m_BehaviorTree != null) {
                    m_BehaviorTree.OnBehaviorTreeStarted -= OnBehaviorTreeStarted;
                }
                if (m_TraversalTaskSystemGroup != null) {
                    m_TraversalTaskSystemGroup.OnPreUpdate -= UpdateNode;
                    m_TraversalTaskSystemGroup = null;
                }
            });
        }

        /// <summary>
        /// The behavior tree has started, so the world/group references may now exist.
        /// </summary>
        private void OnBehaviorTreeStarted()
        {
            TryRegisterUpdateNode();
        }

        /// <summary>
        /// Registers the UpdateNode callback when the runtime group becomes available.
        /// </summary>
        /// <returns>True if the callback was registered.</returns>
        private bool TryRegisterUpdateNode()
        {
            if (!Application.isPlaying || m_BehaviorTree == null) {
                return false;
            }

            var world = m_BehaviorTree.World;
            if (world == null || !world.IsCreated) {
                return false;
            }

            m_TraversalTaskSystemGroup = world.GetExistingSystemManaged<TraversalTaskSystemGroup>();
            m_TraversalTaskSystemGroup.OnPreUpdate += UpdateNode;
            return true;
        }

        /// <summary>
        /// Sets the conditional abort icon.
        /// </summary>
        /// <param name="conditionalAbortParent">The conditional abort node.</param>
        private void SetConditionalAbortIcon(IConditionalAbortParent conditionalAbortParent)
        {
            if (conditionalAbortParent.AbortType == ConditionalAbortType.LowerPriority) {
                m_ConditionalAbortIcon.image = Shared.Editor.Utility.EditorUtility.LoadAsset<Texture>(EditorGUIUtility.isProSkin ? c_DarkConditionalAbortLowerPriorityIconGUID : c_LightConditionalAbortLowerPriorityIconGUID);
            } else if (conditionalAbortParent.AbortType == ConditionalAbortType.Self) {
                m_ConditionalAbortIcon.image = Shared.Editor.Utility.EditorUtility.LoadAsset<Texture>(EditorGUIUtility.isProSkin ? c_DarkConditionalAbortSelfIconGUID : c_LightConditionalAbortSelfIconGUID);
            } else if (conditionalAbortParent.AbortType == ConditionalAbortType.Both) {
                m_ConditionalAbortIcon.image = Shared.Editor.Utility.EditorUtility.LoadAsset<Texture>(EditorGUIUtility.isProSkin ? c_DarkConditionalAbortBothIconGUID : c_LightConditionalAbortBothIconGUID);
            } else {
                m_ConditionalAbortIcon.image = null;
            }
        }

        /// <summary>
        /// A value has been updated for the specified node.
        /// </summary>
        /// <param name="node">The node that has been updated.</param>
        private void UpdateNodeValue(object node)
        {
            if (node != m_Node) {
                return;
            }

            SetConditionalAbortIcon(node as IConditionalAbortParent);
        }

        /// <summary>
        /// Updates the node with the current execution status.
        /// </summary>
        private void UpdateNode()
        {
            UpdateNodeInternal();
        }

        /// <summary>
        /// Internal method which updates the node with the current execution status.
        /// </summary>
        /// <returns>The status of the task.</returns>
        protected virtual TaskStatus UpdateNodeInternal()
        {
            var world = m_BehaviorTree != null ? m_BehaviorTree.World : null;
            if (m_BehaviorTree == null || world == null || !world.IsCreated || m_BehaviorTree.Entity.Index == 0 ||
                !world.EntityManager.Exists(m_BehaviorTree.Entity) || !world.EntityManager.HasBuffer<TaskComponent>(m_BehaviorTree.Entity)) {
                // The task is no longer active. Reset the status while keeping the previous execution state.
                m_LogicNode.SetColorState(m_GraphWindow.GraphEditor.IsNodeHierarchyEnabled(m_Node) ? ColorState.Default : ColorState.Disabled);
                if (m_ExecutionStatusIcon.image != null) {
                    if (m_ExecutionStatusIcon.image == m_SuccessReevaluateIcon) {
                        m_ExecutionStatusIcon.image = m_SuccessIcon;
                    } else if (m_ExecutionStatusIcon.image == m_FailureReevaluateIcon) {
                        m_ExecutionStatusIcon.image = m_FailureIcon;
                    }
                    m_ExecutionStatusIcon.style.width = m_ExecutionStatusIcon.image.width;
                }
                return TaskStatus.Inactive;
            }

            var taskComponents = world.EntityManager.GetBuffer<TaskComponent>(m_BehaviorTree.Entity);
            var nodeIndex = m_Node.RuntimeIndex;
            if (m_GraphWindow.AttachedToGraph != null) {
                // When viewing a loaded subtree graph, map the displayed node to its runtime index in the attached tree.
                var attachedToNodeIndex = m_LogicNode.GetAttachedToGraphNodeIndex();
                if (attachedToNodeIndex != ushort.MaxValue) {
                    nodeIndex = attachedToNodeIndex;
                }
            } else if (m_Node is ISubtreeReferenceNode) {
                // This is a reference node.
                var injectedReferences = m_BehaviorTree.InjectedGraphReferences;
                if (injectedReferences != null) {
                    for (int i = 0; i < injectedReferences.Length; ++i) {
                        var injectedReference = injectedReferences[i];
                        if ((injectedReference.GraphReference != m_Node && injectedReference.RuntimeNodeIndex != m_Node.Index) || injectedReference.RuntimeNodeIndex >= m_BehaviorTree.LogicNodes.Length) {
                            continue;
                        }

                        // A reference can map to multiple injected roots. Pick an active/queued node within its injected range.
                        var selectedRuntimeIndex = m_BehaviorTree.LogicNodes[injectedReference.RuntimeNodeIndex].RuntimeIndex;
                        var hasRunningStatus = false;
                        var rangeEnd = Mathf.Min(m_BehaviorTree.LogicNodes.Length, injectedReference.RuntimeNodeIndex + Mathf.Max(1, injectedReference.NodeCount));
                        for (int j = injectedReference.RuntimeNodeIndex; j < rangeEnd; ++j) {
                            var runtimeIndex = m_BehaviorTree.LogicNodes[j].RuntimeIndex;
                            if (runtimeIndex == ushort.MaxValue || runtimeIndex >= taskComponents.Length) {
                                continue;
                            }

                            var status = taskComponents[runtimeIndex].Status;
                            // Prefer the currently executing node so the reference shows active while any subtree branch is running.
                            if (status == TaskStatus.Running || status == TaskStatus.Queued) {
                                selectedRuntimeIndex = runtimeIndex;
                                hasRunningStatus = true;
                                break;
                            }

                            // Fallback to any non-inactive status if nothing is running yet.
                            if (!hasRunningStatus && status != TaskStatus.Inactive) {
                                selectedRuntimeIndex = runtimeIndex;
                                hasRunningStatus = true;
                            }
                        }

                        if (selectedRuntimeIndex != ushort.MaxValue) {
                            nodeIndex = selectedRuntimeIndex;
                        }
                        break;
                    }
                }
            }
            if (nodeIndex == ushort.MaxValue) {
                m_LogicNode.SetColorState(m_GraphWindow.GraphEditor.IsNodeHierarchyEnabled(m_Node) ? ColorState.Default : ColorState.Disabled);
                return TaskStatus.Inactive;
            }
            if (nodeIndex >= taskComponents.Length) {
                m_LogicNode.SetColorState(m_GraphWindow.GraphEditor.IsNodeHierarchyEnabled(m_Node) ? ColorState.Default : ColorState.Disabled);
                return TaskStatus.Inactive;
            }

            var taskComponent = taskComponents[nodeIndex];
            if (taskComponent.Status == TaskStatus.Running || taskComponent.Status == TaskStatus.Queued) {
                var nodeProperties = m_GraphWindow.Graph.LogicNodeProperties[m_Node.Index];
                if (m_LastActiveFrame <= Time.frameCount - 1) {
                    nodeProperties.StartTime = Time.realtimeSinceStartup;
                }
                m_LastActiveFrame = Time.frameCount;

                m_LogicNode.SetColorState(ColorState.Active);
            } else {
                m_LogicNode.SetColorState(m_GraphWindow.GraphEditor.IsNodeHierarchyEnabled(m_Node) ? ColorState.Default : ColorState.Disabled);
                m_LastActiveFrame = -1;
            }

            if (taskComponent.Status == TaskStatus.Success) {
                if (taskComponent.Reevaluate) {
                    m_ExecutionStatusIcon.image = m_SuccessReevaluateIcon;
                } else {
                    m_ExecutionStatusIcon.image = m_SuccessIcon;
                }
            } else if (taskComponent.Status == TaskStatus.Failure) {
                if (taskComponent.Reevaluate) {
                    m_ExecutionStatusIcon.image = m_FailureReevaluateIcon;
                } else {
                    m_ExecutionStatusIcon.image = m_FailureIcon;
                }
            } else if (m_ExecutionStatusIcon.image != null) {
                m_ExecutionStatusIcon.image = null;
            }

            if (m_ExecutionStatusIcon.image != null) {
                m_ExecutionStatusIcon.style.width = m_ExecutionStatusIcon.image.width;
            }
            return taskComponent.Status;
        }

        /// <summary>
        /// Enables or disables the NodeView.
        /// </summary>
        /// <param name="enable">True if the NodeView is enabled.</param>
        public override void SetEnabled(bool enable)
        {
            if (m_ConditionalAbortIcon != null) {
                m_ConditionalAbortIcon.SetEnabled(enable);
            }
        }

        /// <summary>
        /// The mouse has been pressed.
        /// </summary>
        /// <param name="evt">The event that triggered the press.</param>
        private void OnSubtreeReferenceMouseDown(MouseDownEvent evt)
        {
            if (evt.clickCount != 2) {
                return;
            }

            var subtreeReference = m_Node as ISubtreeReferenceNode;
            if (subtreeReference.Subtrees == null || subtreeReference.Subtrees.Length == 0) {
                return;
            }
            evt.StopImmediatePropagation();

            // Open the subgraph directly if there's only a single subtree.
            if (subtreeReference.Subtrees.Length == 1) {
                var subtree = subtreeReference.Subtrees[0];
                if (Application.isPlaying && !m_GraphWindow.GraphEditor.Settings.FlattenInjectedNodes) {
                    var attachedNodeIndex = m_LogicNode.GetAttachedToGraphNodeIndex();
                    if (attachedNodeIndex == ushort.MaxValue) {
                        var index = m_GraphWindow.AttachedToGraph != null ? m_GraphWindow.AttachedToGraph.NodeIndex : 0;
                        attachedNodeIndex = (ushort)(index + m_Node.Index);
                    }
                    m_GraphWindow.LoadGraph(subtree, true, false, new AttachedToGraph(m_BehaviorTree, attachedNodeIndex, 0));
                } else {
                    Selection.activeObject = subtree;
                }
                return;
            }

            // Show a context menu with multiple subtrees.
            var menu = new GenericMenu();
            for (ushort i = 0; i < subtreeReference.Subtrees.Length; ++i) {
                var subtree = subtreeReference.Subtrees[i];
                var subtreeIndex = i;
                menu.AddItem(new GUIContent(i + ": " + subtree.name), false, () => {
                    if (Application.isPlaying && !m_GraphWindow.GraphEditor.Settings.FlattenInjectedNodes) {
                        var attachedNodeIndex = m_LogicNode.GetAttachedToGraphNodeIndex();
                        if (attachedNodeIndex == ushort.MaxValue) {
                            var index = m_GraphWindow.AttachedToGraph != null ? m_GraphWindow.AttachedToGraph.NodeIndex : 0;
                            attachedNodeIndex = (ushort)(index + m_Node.Index);
                        }
                        m_GraphWindow.LoadGraph(subtree, true, false, new AttachedToGraph(m_BehaviorTree, attachedNodeIndex, subtreeIndex));
                    } else {
                        Selection.activeObject = subtree;
                    }
                });
            }
            menu.ShowAsContext();
        }
    }
}
#endif