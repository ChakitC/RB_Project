#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Components
{
    using Opsive.BehaviorDesigner.Runtime.Groups;
    using Opsive.BehaviorDesigner.Runtime.Systems;
    using Opsive.BehaviorDesigner.Runtime.Utility;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// Indicates that the behavior tree was baked.
    /// </summary>
    public class BakedBehaviorTree : IComponentData
    {
        [Tooltip("The index of the connected start task.")]
        public int StartEventConnectedIndex;
        [Tooltip("Should the behavior tree be started after it has been initialized?")]
        public bool StartWhenEnabled;
        [Tooltip("Should the behavior tree be started after it has been baked?")]
        public bool StartEvaluation;
        [Tooltip("The indicies of the reevaluate task systems.")]
        public string[] ReevaluateTaskSystems;
        [Tooltip("The indicies of the interrupt task systems.")]
        public string[] InterruptTaskSystems;
        [Tooltip("The indicies of the traversal task systems.")]
        public string[] TraversalTaskSystems;
        [Tooltip("The hashes that correspond to the TaskComponent's ComponentType.")]
        public ulong[] TagStableTypeHashes;
        [Tooltip("The hashes that correspond to the ReevaluateTaskComponent's ComponentType.")]
        public ulong[] ReevaluateFlagStableTypeHashes;
    }

    /// <summary>
    /// Baked editor-only metadata used to link a baked entity back to its authoring BehaviorTree for runtime debugging. 
    /// Populated by BehaviorTreeBaker only in editor builds and stripped from entity scenes before they are packaged into a player build by StripEditorBehaviorTreeReferenceSystem.
    /// </summary>
    public class BakedEditorReference : IComponentData
    {
        [Tooltip("The GlobalObjectId string for the authoring BehaviorTree component.")]
        public string AuthoringBehaviorTreeGlobalObjectId;
        [Tooltip("The design-time graph unique ID.")]
        public int DesignGraphUniqueID;
        [Tooltip("Maps design-time logic node index to runtime task index.")]
        public ushort[] LogicNodeRuntimeIndices;
    }

    /// <summary>
    /// Stores the start data for a baked behavior tree that should be started manually.
    /// </summary>
    public struct DeferredBakedBehaviorTreeStart : IComponentData
    {
        [Tooltip("The index of the connected start task.")]
        public ushort StartEventConnectedIndex;
        [Tooltip("Should the behavior tree start evaluation after the branch has started?")]
        public bool StartEvaluation;
    }

    /// <summary>
    /// The behavior tree has been baked. Start the tree using the baked data.
    /// </summary>
    public partial struct StartBakedBehaviorTreeSystem : ISystem
    {
        /// <summary>
        /// Restricts when the system should run.
        /// </summary>
        /// <param name="state">The current SystemState.</param>
        private void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BakedBehaviorTree>();
        }

        /// <summary>
        /// Starts the baked behavior tree.
        /// </summary>
        /// <param name="state">The current SystemState.</param>
        private void OnUpdate(ref SystemState state)
        {
            // The components are baked, but systems are not baked. Create the required systems within the current world.
            var reevaluateTaskSystemGroup = state.World.GetOrCreateSystemManaged<ReevaluateTaskSystemGroup>();
            var interruptTaskSystemGroup = state.World.GetOrCreateSystemManaged<InterruptTaskSystemGroup>();
            var traversalTaskSystemGroup = state.World.GetOrCreateSystemManaged<TraversalTaskSystemGroup>();

            // Add the necessary cleanup systems.
            var behaviorTreeSystemGroup = state.World.GetOrCreateSystemManaged<BehaviorTreeSystemGroup>();
            behaviorTreeSystemGroup.AddSystemToUpdateList(state.World.GetOrCreateSystem<EvaluationCleanupSystem>());
            behaviorTreeSystemGroup.AddSystemToUpdateList(state.World.GetOrCreateSystem<InterruptedCleanupSystem>());

            var canReevaluate = false;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
#if UNITY_EDITOR
            // Collected during the foreach and applied after ecb.Playback() because ECB.AddComponentObject only supports EntityQuery, not individual Entity.
            var editorGraphReferences = new System.Collections.Generic.List<(Entity entity, EditorBehaviorTreeGraphReference reference)>();
#endif
            foreach (var (bakedBehaviorTree, entity) in SystemAPI.Query<BakedBehaviorTree>().WithEntityAccess()) {
                AddSystems(state.World, reevaluateTaskSystemGroup, bakedBehaviorTree.ReevaluateTaskSystems);
                AddSystems(state.World, interruptTaskSystemGroup, bakedBehaviorTree.InterruptTaskSystems);
                AddSystems(state.World, traversalTaskSystemGroup, bakedBehaviorTree.TraversalTaskSystems);

                // ComponentTypes cannot be serialized. Convert the StableTypeHash to a ComponentType.
                var taskComponents = state.World.EntityManager.GetBuffer<TaskComponent>(entity);
                for (int i = 0; i < taskComponents.Length; ++i) {
                    var taskComponent = taskComponents[i];
                    taskComponent.FlagComponentType = ComponentType.FromTypeIndex(TypeManager.GetTypeIndexFromStableTypeHash(bakedBehaviorTree.TagStableTypeHashes[i]));
                    taskComponents[i] = taskComponent;
                }
                TraversalUtility.PopulateChildUpperIndices(ref taskComponents);

                if (state.World.EntityManager.HasBuffer<ReevaluateTaskComponent>(entity)) {
                    var reevaluateComponents = state.World.EntityManager.GetBuffer<ReevaluateTaskComponent>(entity);
                    canReevaluate = true;
                    for (int i = 0; i < reevaluateComponents.Length; ++i) {
                        var reevaluateComponent = reevaluateComponents[i];
                        reevaluateComponent.ReevaluateFlagComponentType = ComponentType.FromTypeIndex(TypeManager.GetTypeIndexFromStableTypeHash(bakedBehaviorTree.ReevaluateFlagStableTypeHashes[i]));
                        reevaluateComponents[i] = reevaluateComponent;
                    }
                }

                // All of the systems have been added. Start the behavior tree or defer the start.
                if (bakedBehaviorTree.StartWhenEnabled) {
                    BehaviorTree.StartBranch(state.World, entity, (ushort)bakedBehaviorTree.StartEventConnectedIndex, bakedBehaviorTree.StartEvaluation);
                } else {
                    ecb.AddComponent(entity, new DeferredBakedBehaviorTreeStart
                    {
                        StartEventConnectedIndex = (ushort)bakedBehaviorTree.StartEventConnectedIndex,
                        StartEvaluation = bakedBehaviorTree.StartEvaluation
                    });
                }
#if UNITY_EDITOR
                // BakedEditorReference is stripped from player build entity scenes by StripEditorBehaviorTreeReferenceSystem before serialization, so it only ever reaches
                // this system during editor play mode.
                if (state.EntityManager.HasComponent<BakedEditorReference>(entity)) {
                    var editorRef = state.EntityManager.GetComponentObject<BakedEditorReference>(entity);
                    if (!string.IsNullOrEmpty(editorRef.AuthoringBehaviorTreeGlobalObjectId)) {
                        editorGraphReferences.Add((entity, new EditorBehaviorTreeGraphReference
                        {
                            AuthoringBehaviorTreeGlobalObjectId = editorRef.AuthoringBehaviorTreeGlobalObjectId,
                            DesignGraphUniqueID = editorRef.DesignGraphUniqueID,
                            LogicNodeRuntimeIndices = editorRef.LogicNodeRuntimeIndices,
                        }));
                    }
                    ecb.RemoveComponent<BakedEditorReference>(entity);
                }
#endif
                ecb.RemoveComponent<BakedBehaviorTree>(entity);
            }
            if (canReevaluate) {
                state.World.GetOrCreateSystemManaged<BeforeTraversalSystemGroup>().AddSystemToUpdateList(state.World.GetOrCreateSystem(typeof(ReevaluateSystem)));
            }

            reevaluateTaskSystemGroup.SortSystems();
            interruptTaskSystemGroup.SortSystems();
            traversalTaskSystemGroup.SortSystems();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
#if UNITY_EDITOR
            foreach (var (entity, reference) in editorGraphReferences) {
                state.EntityManager.AddComponentObject(entity, reference);
            }
#endif
        }

        /// <summary>
        /// Adds the systems indicated by the SystemTypeIndex to the specified group.
        /// </summary>
        /// <param name="world">The current World.</param>
        /// <param name="group">The group that the systems should be added to.</param>
        /// <param name="systemTypes">The types of systems that should be added.</param>
        private void AddSystems(World world, ComponentSystemGroup group, string[] systemTypes)
        {
            if (systemTypes == null) { return; }

            for (int i = 0; i < systemTypes.Length; ++i) {
                group.AddSystemToUpdateList(world.GetOrCreateSystem(Shared.Utility.TypeUtility.GetType(systemTypes[i])));
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Strips BakedEditorReference from all entities during the entity scene optimization pass, which runs
    /// after baking but before the .entities file is serialized to disk for player builds. This ensures the
    /// type hash for BakedEditorReference never appears in a standalone build's entity scene, while the
    /// component remains available during editor play mode for StartBakedBehaviorTreeSystem to consume.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.EntitySceneOptimizations)]
    public partial class StripEditorBehaviorTreeReferenceSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var query = GetEntityQuery(ComponentType.ReadOnly<BakedEditorReference>());
            EntityManager.RemoveComponent<BakedEditorReference>(query);
        }
    }
#endif
}
#endif