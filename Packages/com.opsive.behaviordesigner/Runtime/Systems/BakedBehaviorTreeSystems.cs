#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Components
{
    using Opsive.BehaviorDesigner.Runtime.Groups;
    using Opsive.BehaviorDesigner.Runtime.Utility;
    using Unity.Entities;
    using UnityEngine;

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
            BehaviorTreeExecution.AddCleanupSystems(state.World, behaviorTreeSystemGroup);

            var canReevaluate = false;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
#if UNITY_EDITOR && !UNITY_6000_6_OR_NEWER
            // Collected during the foreach and applied after ecb.Playback() because ECB.AddComponentObject only supports EntityQuery, not individual Entity.
            var editorGraphReferences = new System.Collections.Generic.List<(Entity entity, EditorBehaviorTreeGraphReference reference)>();
#endif
#if UNITY_6000_6_OR_NEWER
            foreach (var (bakedBehaviorTreeReference, entity) in SystemAPI.Query<RefRO<BakedBehaviorTree>>().WithEntityAccess()) {
                var bakedBehaviorTree = bakedBehaviorTreeReference.ValueRO;
                ref var bakedData = ref bakedBehaviorTree.Data.Value;
                AddSystems(state.World, reevaluateTaskSystemGroup, ref bakedData.ReevaluateTaskSystems);
                AddSystems(state.World, interruptTaskSystemGroup, ref bakedData.InterruptTaskSystems);
                AddSystems(state.World, traversalTaskSystemGroup, ref bakedData.TraversalTaskSystems);
#else
            foreach (var (bakedBehaviorTree, entity) in SystemAPI.Query<BakedBehaviorTree>().WithEntityAccess()) {
                AddSystems(state.World, reevaluateTaskSystemGroup, bakedBehaviorTree.ReevaluateTaskSystems);
                AddSystems(state.World, interruptTaskSystemGroup, bakedBehaviorTree.InterruptTaskSystems);
                AddSystems(state.World, traversalTaskSystemGroup, bakedBehaviorTree.TraversalTaskSystems);
#endif

                // ComponentTypes cannot be serialized. Convert the StableTypeHash to a ComponentType.
                var taskComponents = state.World.EntityManager.GetBuffer<TaskComponent>(entity);
                for (int i = 0; i < taskComponents.Length; ++i) {
                    var taskComponent = taskComponents[i];
                    taskComponent.FlagComponentType = ComponentType.FromTypeIndex(TypeManager.GetTypeIndexFromStableTypeHash(
#if UNITY_6000_6_OR_NEWER
                        bakedData.TagStableTypeHashes[i]
#else
                        bakedBehaviorTree.TagStableTypeHashes[i]
#endif
                    ));
                    taskComponents[i] = taskComponent;
                }
                TraversalUtility.PopulateChildUpperIndices(ref taskComponents);

                if (state.World.EntityManager.HasBuffer<ReevaluateTaskComponent>(entity)) {
                    var reevaluateComponents = state.World.EntityManager.GetBuffer<ReevaluateTaskComponent>(entity);
                    canReevaluate = true;
                    for (int i = 0; i < reevaluateComponents.Length; ++i) {
                        var reevaluateComponent = reevaluateComponents[i];
                        reevaluateComponent.ReevaluateFlagComponentType = ComponentType.FromTypeIndex(TypeManager.GetTypeIndexFromStableTypeHash(
#if UNITY_6000_6_OR_NEWER
                            bakedData.ReevaluateFlagStableTypeHashes[i]
#else
                            bakedBehaviorTree.ReevaluateFlagStableTypeHashes[i]
#endif
                        ));
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
#if UNITY_6000_6_OR_NEWER
                    var editorRef = state.EntityManager.GetComponentData<BakedEditorReference>(entity);
                    if (editorRef.Data.IsCreated && editorRef.Data.Value.AuthoringBehaviorTreeGlobalObjectId.Length > 0) {
                        ecb.AddComponent(entity, new EditorBehaviorTreeGraphReference
                        {
                            DesignGraphUniqueID = editorRef.DesignGraphUniqueID,
                            Data = editorRef.Data,
                        });
                    }
#else
                    var editorRef = state.EntityManager.GetComponentObject<BakedEditorReference>(entity);
                    if (!string.IsNullOrEmpty(editorRef.AuthoringBehaviorTreeGlobalObjectId)) {
                        editorGraphReferences.Add((entity, new EditorBehaviorTreeGraphReference
                        {
                            AuthoringBehaviorTreeGlobalObjectId = editorRef.AuthoringBehaviorTreeGlobalObjectId,
                            DesignGraphUniqueID = editorRef.DesignGraphUniqueID,
                            LogicNodeRuntimeIndices = editorRef.LogicNodeRuntimeIndices,
                        }));
                    }
#endif
                    ecb.RemoveComponent<BakedEditorReference>(entity);
                }
#endif
                ecb.RemoveComponent<BakedBehaviorTree>(entity);
            }
            if (canReevaluate) {
                BehaviorTreeExecution.AddReevaluateSystem(state.World, state.World.GetOrCreateSystemManaged<BeforeTraversalSystemGroup>());
            }

            reevaluateTaskSystemGroup.SortSystems();
            interruptTaskSystemGroup.SortSystems();
            traversalTaskSystemGroup.SortSystems();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
#if UNITY_EDITOR && !UNITY_6000_6_OR_NEWER
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
#if UNITY_6000_6_OR_NEWER
        private void AddSystems(World world, ComponentSystemGroup group, ref BlobArray<BlobString> systemTypes)
        {
            for (int i = 0; i < systemTypes.Length; ++i) {
                group.AddSystemToUpdateList(world.GetOrCreateSystem(Shared.Utility.TypeUtility.GetType(systemTypes[i].ToString())));
            }
        }
#else
        private void AddSystems(World world, ComponentSystemGroup group, string[] systemTypes)
        {
            if (systemTypes == null) { return; }

            for (int i = 0; i < systemTypes.Length; ++i) {
                group.AddSystemToUpdateList(world.GetOrCreateSystem(Shared.Utility.TypeUtility.GetType(systemTypes[i])));
            }
        }
#endif
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