#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks
{
    using Opsive.GraphDesigner.Runtime;

    /// <summary>
    /// Interface for tasks that can load subtrees.
    /// </summary>
    public interface ISubtreeReferenceNode : IGraphReferenceNode
    {
        /// <summary>
        /// The Subtrees that should be used at runtime.
        /// </summary>
        Subtree[] Subtrees { get; }
    }

    [System.Obsolete("ISubtreeReference has been deprecated. Use ISubtreeReferenceNode instead.")]
    public interface ISubtreeReference { }
}
#endif