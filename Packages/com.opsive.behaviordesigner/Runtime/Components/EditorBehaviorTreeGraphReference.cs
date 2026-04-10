#if GRAPH_DESIGNER && UNITY_EDITOR
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Components
{
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// Editor-only metadata used to resolve the authoring behavior tree graph from a pure entity selection.
    /// </summary>
    public class EditorBehaviorTreeGraphReference : IComponentData
    {
        [Tooltip("The GlobalObjectId string for the authoring BehaviorTree component.")]
        public string AuthoringBehaviorTreeGlobalObjectId;
        [Tooltip("The design-time graph ID used to validate the resolved graph.")]
        public int DesignGraphUniqueID;
        [Tooltip("Maps design-time logic node index to runtime task index for the baked entity.")]
        public ushort[] LogicNodeRuntimeIndices;
    }
}
#endif