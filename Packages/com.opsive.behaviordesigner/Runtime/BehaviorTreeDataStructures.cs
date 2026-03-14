#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.Shared.Utility;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;

    /// <summary>
    /// Storage class for the graph data.
    /// </summary>
    public partial class BehaviorTreeData
    {
        /// <summary>
        /// Data structure which contains the properties for a subtree that will be injected.
        /// </summary>
        private struct SubtreeAssignment
        {
            [Tooltip("The index of the SubtreeNodesReference element.")]
            public int ReferenceIndex;
            [Tooltip("The index of the ISubtreeReference task.")]
            public ushort NodeIndex;
            [Tooltip("The index of the Subtree.")]
            public int SubtreeIndex;
            [Tooltip("The subtree that the task references.")]
            public Subtree Subtree;
            [Tooltip("The offset of the index. This will change as subtrees are added.")]
            public ushort IndexOffset;
            [Tooltip("The original parent index of the ISubtreeReference task.")]
            public ushort ParentIndex;
            [Tooltip("The original sibling index of the ISubtreeReference task.")]
            public ushort SiblingIndex;
            [Tooltip("The number of nodes that are a child of the ISubtreeReference.")]
            public ushort NodeCount;
#if UNITY_EDITOR
            [Tooltip("The position of the ISubtreeReference task.")]
            public Vector2 NodePropertiesPosition;
            [Tooltip("Is the ISubtreeReference task collapsed?")]
            public bool Collapsed;
#endif
        }

        /// <summary>
        /// Contains a reference to the subtree index and nodes.
        /// </summary>
        internal struct SubtreeNodesReference
        {
            [Tooltip("The ISubtreeReference.")]
            public ISubtreeReference SubtreeReference;
            [Tooltip("The index of the ISubtreeReference.")]
            public ushort NodeIndex;
            [Tooltip("The total number of nodes contained within the ISubtreeReference.")]
            public ushort NodeCount;
            [Tooltip("A reference to the subtrees that are loaded.")]
            public Subtree[] Subtrees;
            [Tooltip("The deserialized nodes.")]
            public ITreeLogicNode[][] Nodes;
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