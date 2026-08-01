#if GRAPH_DESIGNER && UNITY_EDITOR
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Systems
{
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Editor utility methods for resolving BehaviorTree instances from ECS entity references.
    /// </summary>
    internal static class BehaviorTreeEditorUtility
    {
        /// <summary>
        /// Resolves the authoring BehaviorTree from a GlobalObjectId string.
        /// </summary>
        /// <param name="authoringBehaviorTreeGlobalObjectId">The GlobalObjectId string for the authoring BehaviorTree.</param>
        /// <param name="designGraphUniqueID">The design-time graph ID.</param>
        /// <returns>The resolved BehaviorTree, or null if it could not be found.</returns>
        internal static BehaviorTree ResolveBehaviorTreeFromGlobalObjectId(string authoringBehaviorTreeGlobalObjectId, int designGraphUniqueID)
        {
            if (!GlobalObjectId.TryParse(authoringBehaviorTreeGlobalObjectId, out var globalObjectId)) {
                return null;
            }

            return BehaviorTree.ResolveBehaviorTreeFromObject(GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId), designGraphUniqueID);
        }
    }
}
#endif