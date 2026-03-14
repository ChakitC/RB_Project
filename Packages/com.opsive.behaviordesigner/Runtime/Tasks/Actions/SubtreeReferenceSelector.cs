#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions
{
    using Opsive.BehaviorDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    /// <summary>
    /// Allows for subtrees to be loaded at runtime into the tree.
    /// </summary>
    [Opsive.Shared.Utility.Description("Selects a subtree based on the index value.")]
    public class SubtreeReferenceSelector : SubtreeReference
    {
        [Tooltip("The index of the subtree that should be selected.")]
        [SerializeField] protected SharedVariable<int> m_Index;

        private Subtree[] m_Selection;

        public override Subtree[] Subtrees => m_Selection != null ? m_Selection : m_Subtrees;

        /// <summary>
        /// Performs any runtime operations to evaluate the array of subtrees that should be returned.
        /// </summary>
        /// <param name="graphComponent">The component that the node is attached to.</param>
        public override void EvaluateSubtrees(IGraphComponent graphComponent)
        {
            if (m_Index.Value < 0 || m_Index.Value >= m_Subtrees.Length) {
                return;
            }

            m_Selection = new Subtree[] { m_Subtrees[m_Index.Value] };
        }
    }
}

#endif