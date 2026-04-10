#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using System;
    using System.Reflection;
    using UnityEngine;

    /// <summary>
    /// Storage class for the graph data.
    /// </summary>
    public partial class BehaviorTreeData
    {
        /// <summary>
        /// Data structure which contains the properties for a subtree that will be injected.
        /// When EventNodeType is typeof(Start), this describes the logic block that replaces the subtree reference.
        /// Otherwise it describes an event node to append to the parent tree's event list.
        /// </summary>
        private struct SubtreeAssignment
        {
            [Tooltip("The type of event node. typeof(Start) = logic block assignment; otherwise = event node to append.")]
            public Type EventNodeType;
            [Tooltip("The index of the EventNode within the subtree.")]
            public ushort EventNodeIndex;
            [Tooltip("The index of the starting logic node within the subtree.")]
            public ushort SourceIndex;
            [Tooltip("The index of the SubtreeNodesReference element.")]
            public int ReferenceIndex;
            [Tooltip("The index of the ISubtreeReferenceNode task.")]
            public ushort NodeIndex;
            [Tooltip("The index of the Subtree.")]
            public int SubtreeIndex;
            [Tooltip("The subtree that the task references.")]
            public Subtree Subtree;
            [Tooltip("The offset of the index. This will change as subtrees are added.")]
            public ushort IndexOffset;
            [Tooltip("The original parent index of the ISubtreeReferenceNode task.")]
            public ushort ParentIndex;
            [Tooltip("The original sibling index of the ISubtreeReferenceNode task.")]
            public ushort SiblingIndex;
            [Tooltip("The number of nodes that are a child of the ISubtreeReferenceNode.")]
            public ushort NodeCount;
#if UNITY_EDITOR
            [Tooltip("The position of the ISubtreeReferenceNode task.")]
            public Vector2 NodePropertiesPosition;
            [Tooltip("Is the ISubtreeReferenceNode task collapsed?")]
            public bool Collapsed;
#endif
        }

        /// <summary>
        /// Contains a reference to the subtree index and nodes.
        /// </summary>
        internal struct InjectedSubtreeReference
        {
            [Tooltip("The ISubtreeReference.")]
            public IGraphReferenceNode GraphReference { get; set; }
            [Tooltip("The index of the ISubtreeReference.")]
            public ushort NodeIndex { get; set; }
            [Tooltip("The total number of nodes contained within the ISubtreeReference.")]
            public ushort NodeCount { get; set; }
            [Tooltip("A reference to the subtrees that are loaded.")]
            public Subtree[] Subtrees { get; set; }

#if UNITY_EDITOR
            [Tooltip("The NodeProperties for the graph reference node.")]
            public LogicNodeProperties GraphReferenceNodeProperties { get; set; }
#endif
            [Tooltip("The deserialized nodes.")]
            public ITreeLogicNode[][] m_Nodes;

            public IGraph[] Graphs {
                get => Subtrees;
                set {
                    if (value == null) {
                        Subtrees = null;
                        return;
                    }

                    Subtrees = new Subtree[value.Length];
                    for (int i = 0; i < Subtrees.Length; ++i) {
                        Subtrees[i] = (Subtree)value[i];
                    }
                }
            }
            public ITreeLogicNode[][] TreeNodes { get => m_Nodes; set => m_Nodes = value; }
            public ILogicNode[][] Nodes {
                get => m_Nodes;
                set {
                    if (value == null) {
                        m_Nodes = null;
                        return;
                    }
                    m_Nodes = new ITreeLogicNode[value.Length][];
                    for (int i = 0; i < m_Nodes.Length; ++i) {
                        if (value[i] == null) {
                            continue;
                        }
                        m_Nodes[i] = new ITreeLogicNode[value[i].Length];
                        for (int j = 0; j < m_Nodes[i].Length; ++j) {
                            m_Nodes[i][j] = (ITreeLogicNode)value[i][j];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Keeps a reference to the graph variables allowing them to be overwritten if a subtree is set.
        /// </summary>
        private struct VariableField
        {
            [Tooltip("The field that the SharedVariable is assigned to.")]
            public FieldInfo Field;
            [Tooltip("The task that the SharedVariable is assigned to.")]
            public object Task;
            [Tooltip("The name of the SharedVariable.")]
            public string Name;
        }

        /// <summary>
        /// Internal data structure for referencing a SharedVariable to its name/scope.
        /// </summary>
        public struct VariableAssignment
        {
            [Tooltip("The name of the SharedVariable.")]
            public PropertyName Name;
            [Tooltip("The scope of the SharedVariable.")]
            public SharedVariable.SharingScope Scope;

            /// <summary>
            /// VariableAssignment constructor.
            /// </summary>
            /// <param name="name">The name of the SharedVariable.</param>
            /// <param name="scope">The scope of the SharedVariable.</param>
            public VariableAssignment(PropertyName name, SharedVariable.SharingScope scope)
            {
                Name = name;
                Scope = scope;
            }
        }

        /// <summary>
        /// Internal data structure for restoring a task reference after it has been deserialized.
        /// </summary>
        public struct TaskAssignment
        {
            [Tooltip("The field of the task.")]
            public FieldInfo Field;
            [Tooltip("The task that the field belongs to.")]
            public object Target;
            [Tooltip("The value of the field. This will be the task object that should be assigned after the tree has been loaded.")]
            public object Value;
        }
    }
}
#endif