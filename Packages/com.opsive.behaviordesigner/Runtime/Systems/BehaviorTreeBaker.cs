#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.BehaviorDesigner.Runtime.Groups;
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Events;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;
#endif

    /// <summary>
    /// Converts the behavior tree to a DOTS entity.
    /// </summary>
    [BakeDerivedTypes]
    public class BehaviorTreeBaker : Baker<BehaviorTree>
    {
        private static MethodInfo s_GetTypeOfSystemMethod;

        /// <summary>
        /// Bakes the behavior tree to the DOTS entity.
        /// </summary>
        /// <param name="behaviorTree">The authoring behavior tree.</param>
        public override void Bake(BehaviorTree behaviorTree)
        {
            if (!behaviorTree.enabled) {
                return;
            }

#if UNITY_EDITOR
            // Live conversion runs after every graph edit. Do not force-deserialize or initialize an incomplete ECS graph
            // while its editor data is in use by the graph window.
            var editingGraph = IsBakingForEditor() && !EditorApplication.isPlayingOrWillChangePlaymode;
            if (editingGraph && !ValidateECSTasks(behaviorTree, behaviorTree.Data, false)) {
                return;
            }
#endif

            var entity = GetEntity(behaviorTree, TransformUsageFlags.Dynamic);
            var data = behaviorTree.DeserializeForBaking();
            if (data == null) {
                return;
            }

            var logInvalidTasks = true;
#if UNITY_EDITOR
            // Entering Play Mode or building still reports an invalid task if edit-time data was unavailable above.
            logInvalidTasks = !editingGraph;
#endif
            if (!ValidateECSTasks(behaviorTree, data, logInvalidTasks)) {
                return;
            }
            RegisterSubtreeDependencies(behaviorTree, data);

            // Initialize an isolated entity to discover the complete runtime archetype and metadata. The actual baking-world
            // entity is populated by BehaviorTreeBakingSystem so every structural component is declared through the Baker API.
            using var scratchWorld = new World("Behavior Tree Baking Scratch", WorldFlags.Conversion);
            var scratchEntity = scratchWorld.EntityManager.CreateEntity();
            try {
                if (!behaviorTree.InitializeTree(scratchWorld, scratchEntity, data)) {
                    return;
                }
#if UNITY_EDITOR
                ushort[] logicNodeRuntimeIndices = null;
                var logicNodes = data.LogicNodes;
                if (logicNodes != null && logicNodes.Length > 0) {
                    logicNodeRuntimeIndices = new ushort[logicNodes.Length];
                    for (int i = 0; i < logicNodes.Length; ++i) {
                        logicNodeRuntimeIndices[i] = logicNodes[i].RuntimeIndex;
                    }
                }
#endif

                var connectedIndex = GetStartTaskConnectedIndex(behaviorTree, data);
                if (connectedIndex == -1 || connectedIndex == ushort.MaxValue) {
                    return;
                }

                var taskComponents = scratchWorld.EntityManager.GetBuffer<TaskComponent>(scratchEntity);
                var tagStableTypeHashes = new ulong[taskComponents.Length];
                for (int i = 0; i < taskComponents.Length; ++i) {
                    tagStableTypeHashes[i] = TypeManager.GetTypeInfo(taskComponents[i].FlagComponentType.TypeIndex).StableTypeHash;
                }

                ulong[] reevaluateFlagStableTypeHashes = null;
                if (scratchWorld.EntityManager.HasBuffer<ReevaluateTaskComponent>(scratchEntity)) {
                    var reevaluateTaskComponents = scratchWorld.EntityManager.GetBuffer<ReevaluateTaskComponent>(scratchEntity);
                    reevaluateFlagStableTypeHashes = new ulong[reevaluateTaskComponents.Length];
                    for (int i = 0; i < reevaluateTaskComponents.Length; ++i) {
                        reevaluateFlagStableTypeHashes[i] = TypeManager.GetTypeInfo(reevaluateTaskComponents[i].ReevaluateFlagComponentType.TypeIndex).StableTypeHash;
                    }
                }

                var snapshot = BehaviorTreeBakingSnapshotUtility.Capture(scratchWorld.EntityManager, entity, scratchEntity);
                var runtimeComponentTypes = snapshot.ComponentTypes;
                for (int i = 0; i < runtimeComponentTypes.Length; ++i) {
                    AddComponent(entity, runtimeComponentTypes[i]);
                }

                AddComponentObject(entity, new BehaviorTreeBakingData(snapshot));
                AddComponentObject(entity, new BakedBehaviorTree
                {
                    StartEventConnectedIndex = connectedIndex,
                    StartWhenEnabled = behaviorTree.StartWhenEnabled,
                    StartEvaluation = behaviorTree.UpdateMode == UpdateMode.EveryFrame,
                    ReevaluateTaskSystems = GetTaskSystems<ReevaluateTaskSystemGroup>(scratchWorld),
                    InterruptTaskSystems = GetTaskSystems<InterruptTaskSystemGroup>(scratchWorld),
                    TraversalTaskSystems = GetTaskSystems<TraversalTaskSystemGroup>(scratchWorld),
                    TagStableTypeHashes = tagStableTypeHashes,
                    ReevaluateFlagStableTypeHashes = reevaluateFlagStableTypeHashes,
                });
#if UNITY_EDITOR
                // Stored in a separate component so StripEditorBehaviorTreeReferenceSystem can remove it during the entity scene optimization pass, keeping it out of the build.
                AddComponentObject(entity, new BakedEditorReference
                {
                    AuthoringBehaviorTreeGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(behaviorTree).ToString(),
                    DesignGraphUniqueID = behaviorTree.UniqueID,
                    LogicNodeRuntimeIndices = logicNodeRuntimeIndices,
                });
#endif
            } finally {
                behaviorTree.ReleaseBakingState(scratchWorld, scratchEntity, data);
            }
        }

        /// <summary>
        /// Validates that every logic node can run as an ECS task.
        /// </summary>
        /// <param name="behaviorTree">The behavior tree being baked.</param>
        /// <param name="data">The effective behavior tree data being baked.</param>
        /// <param name="logInvalidTasks">Should invalid tasks be logged?</param>
        /// <returns>True if every logic node is an ECS task.</returns>
        private bool ValidateECSTasks(BehaviorTree behaviorTree, BehaviorTreeData data, bool logInvalidTasks)
        {
            var logicNodes = data?.LogicNodes;
            if (logicNodes == null) {
                return true;
            }

            var valid = true;
            for (int i = 0; i < logicNodes.Length; ++i) {
                if (logicNodes[i] == null || logicNodes[i] is IAuthoringTask) {
                    continue;
                }

                valid = false;
                if (logInvalidTasks) {
                    Debug.LogError($"Error: The behavior tree {behaviorTree.name} contains the non-ECS task {logicNodes[i].GetType().FullName} at index {i} and will not be baked.", behaviorTree);
                }
            }
            return valid;
        }

        /// <summary>
        /// Registers all static subtrees as bake dependencies.
        /// </summary>
        /// <param name="behaviorTree">The behavior tree being baked.</param>
        /// <param name="data">The effective behavior tree data being baked.</param>
        private void RegisterSubtreeDependencies(BehaviorTree behaviorTree, BehaviorTreeData data)
        {
            var visitedSubtrees = new HashSet<Subtree>();
            if (behaviorTree.m_Subtree != null && visitedSubtrees.Add(behaviorTree.m_Subtree)) {
                DependsOn(behaviorTree.m_Subtree);
            }
            RegisterSubtreeDependencies(data, visitedSubtrees);
        }

        /// <summary>
        /// Registers all injected subtrees in the specified graph data as bake dependencies.
        /// </summary>
        /// <param name="data">The graph data that may contain injected subtree references.</param>
        /// <param name="visitedSubtrees">The subtrees that have already been registered.</param>
        private void RegisterSubtreeDependencies(BehaviorTreeData data, HashSet<Subtree> visitedSubtrees)
        {
            if (data == null || data.InjectedSubtreeReferences == null) {
                return;
            }

            for (int i = 0; i < data.InjectedSubtreeReferences.Count; ++i) {
                var subtrees = data.InjectedSubtreeReferences[i].Subtrees;
                if (subtrees == null) {
                    continue;
                }

                for (int j = 0; j < subtrees.Length; ++j) {
                    if (subtrees[j] == null || !visitedSubtrees.Add(subtrees[j])) {
                        continue;
                    }
                    DependsOn(subtrees[j]);
                    RegisterSubtreeDependencies(subtrees[j].Data, visitedSubtrees);
                }
            }
        }

        /// <summary>
        /// Returns the index of the node connection for the start event task.
        /// </summary>
        /// <param name="behaviorTree">The interested behavior tree.</param>
        /// <param name="data">The effective behavior tree data being baked.</param>
        /// <returns>The index of the node connection for the start event task.</returns>
        private int GetStartTaskConnectedIndex(BehaviorTree behaviorTree, BehaviorTreeData data)
        {
            // The behavior tree has to first be initialized.
            if (behaviorTree.World == null || behaviorTree.Entity == Entity.Null) {
                Debug.LogError($"Error: Unable to retrieve the connected index on behavior tree {behaviorTree}. The behavior tree has to first be initialized.");
                return -1;
            }

            for (int i = 0; i < data.EventNodes.Length; ++i) {
                if (data.EventNodes[i].GetType() == typeof(Start)) {
                    // The connected index may not be valid.
                    if (data.EventNodes[i].ConnectedIndex == ushort.MaxValue) {
                        return ushort.MaxValue;
                    }

                    // The branch cannot start if it is disabled.
                    if (!data.IsNodeEnabled(false, i)) {
                        return -1;
                    }

                    return data.LogicNodes[data.EventNodes[i].ConnectedIndex].RuntimeIndex;
                }
            }
            return -1;
        }

        /// <summary>
        /// Returns all of the system type indicies within the systems of the specified type.
        /// </summary>
        /// <param name="world">The world that the systems were added to.</param>
        /// <returns>The system type indicies within the systems of the specified type (can be null).</returns>
        private string[] GetTaskSystems<T>(World world) where T : ComponentSystemGroup
        {
            var systemGroup = world.GetExistingSystemManaged<T>();
            if (systemGroup == null) {
                return null;
            }

            var systems = systemGroup.GetAllSystems();
            if (systems.Length == 0) {
                systems.Dispose();
                return null;
            }

            // Use reflection to call WorldUnmanaged.GetTypeOfSystem. This method is only called during baking at edit time so reflection is ok, though it would be better
            // if this method was eventually made public.
            if (s_GetTypeOfSystemMethod == null) {
                s_GetTypeOfSystemMethod = typeof(WorldUnmanaged).GetMethod("GetTypeOfSystem", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (s_GetTypeOfSystemMethod == null) {
                    Debug.LogError("Error: Unable to find WorldUnmanaged.GetTypeOfSystem. Please email support@opsive.com with your Unity version and Entity package version.");
                    return null;
                }
            }

            var systemTypes = new string[systems.Length];
            for (int i = 0; i < systems.Length; ++i) {
                var systemTypeIndex = TypeManager.GetSystemTypeIndex((Type)s_GetTypeOfSystemMethod.Invoke(world.Unmanaged, new object[] { systems[i] }));
                systemTypes[i] = systemTypeIndex.ToString();
            }
            systems.Dispose();
            return systemTypes;
        }
    }

    /// <summary>
    /// Stores all unmanaged behavior-tree component values captured from a scratch entity.
    /// </summary>
    internal sealed class BehaviorTreeBakingSnapshot
    {
        internal readonly Entity TargetEntity;
        internal readonly ComponentType[] ComponentTypes;
        internal readonly BehaviorTreeComponentBakingSnapshot[] Components;

        /// <summary>
        /// Creates a component snapshot for the specified target entity.
        /// </summary>
        /// <param name="targetEntity">The baking-world entity that receives the component values.</param>
        /// <param name="componentTypes">The component types in the snapshot.</param>
        /// <param name="components">The captured component values.</param>
        internal BehaviorTreeBakingSnapshot(Entity targetEntity, ComponentType[] componentTypes, BehaviorTreeComponentBakingSnapshot[] components)
        {
            TargetEntity = targetEntity;
            ComponentTypes = componentTypes;
            Components = components;
        }
    }

    /// <summary>
    /// Stores one unmanaged component or buffer value captured during scratch conversion.
    /// </summary>
    internal sealed class BehaviorTreeComponentBakingSnapshot
    {
        internal readonly ComponentType ComponentType;
        internal readonly object Value;
        internal readonly bool IsBuffer;
        internal readonly bool Enabled;

        /// <summary>
        /// Creates a captured component value.
        /// </summary>
        /// <param name="componentType">The captured component type.</param>
        /// <param name="value">The boxed component value or buffer array.</param>
        /// <param name="isBuffer">True when the value represents a dynamic buffer.</param>
        /// <param name="enabled">The enableable component state.</param>
        internal BehaviorTreeComponentBakingSnapshot(ComponentType componentType, object value, bool isBuffer, bool enabled)
        {
            ComponentType = componentType;
            Value = value;
            IsBuffer = isBuffer;
            Enabled = enabled;
        }
    }

    /// <summary>
    /// Captures and restores scratch-world component values without changing the Baker-owned archetype.
    /// </summary>
    internal static class BehaviorTreeBakingSnapshotUtility
    {
        private static readonly MethodInfo s_CaptureBufferMethod = typeof(BehaviorTreeBakingSnapshotUtility).GetMethod(nameof(CaptureBuffer), BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo s_CaptureComponentMethod = typeof(BehaviorTreeBakingSnapshotUtility).GetMethod(nameof(CaptureComponent), BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo s_ApplyBufferMethod = typeof(BehaviorTreeBakingSnapshotUtility).GetMethod(nameof(ApplyBuffer), BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo s_ApplyComponentMethod = typeof(BehaviorTreeBakingSnapshotUtility).GetMethod(nameof(ApplyComponent), BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// Captures all unmanaged components from a scratch entity.
        /// </summary>
        /// <param name="entityManager">The scratch-world entity manager.</param>
        /// <param name="targetEntity">The baking-world target entity.</param>
        /// <param name="sourceEntity">The scratch entity to capture.</param>
        /// <returns>The captured entity data.</returns>
        internal static BehaviorTreeBakingSnapshot Capture(EntityManager entityManager, Entity targetEntity, Entity sourceEntity)
        {
            using var componentTypes = entityManager.GetComponentTypes(sourceEntity, Allocator.Temp);
            var types = componentTypes.ToArray();
            var components = new BehaviorTreeComponentBakingSnapshot[types.Length];
            for (int i = 0; i < types.Length; ++i) {
                var componentType = types[i];
                var typeInfo = TypeManager.GetTypeInfo(componentType.TypeIndex);
                var managedType = TypeManager.GetType(componentType.TypeIndex);
                object value = null;
                var isBuffer = typeInfo.Category == TypeManager.TypeCategory.BufferData;
                if (isBuffer) {
                    value = s_CaptureBufferMethod.MakeGenericMethod(managedType).Invoke(null, new object[] { entityManager, sourceEntity });
                } else if (typeInfo.Category == TypeManager.TypeCategory.ComponentData) {
                    if (componentType.TypeIndex.IsManagedComponent) {
                        throw new InvalidOperationException($"Behavior Designer scratch baking cannot snapshot the managed component {managedType.FullName}.");
                    }
                    if (!typeInfo.IsZeroSized) {
                        value = s_CaptureComponentMethod.MakeGenericMethod(managedType).Invoke(null, new object[] { entityManager, sourceEntity });
                    }
                } else {
                    throw new InvalidOperationException($"Behavior Designer scratch baking cannot snapshot component category {typeInfo.Category} for {managedType.FullName}.");
                }

                var enabled = !componentType.IsEnableable || entityManager.IsComponentEnabled(sourceEntity, componentType);
                components[i] = new BehaviorTreeComponentBakingSnapshot(componentType, value, isBuffer, enabled);
            }
            return new BehaviorTreeBakingSnapshot(targetEntity, types, components);
        }

        /// <summary>
        /// Restores a captured entity snapshot onto its baking-world target.
        /// </summary>
        /// <param name="entityManager">The baking-world entity manager.</param>
        /// <param name="snapshot">The snapshot to restore.</param>
        internal static void Apply(EntityManager entityManager, BehaviorTreeBakingSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.Components.Length; ++i) {
                var component = snapshot.Components[i];
                var managedType = TypeManager.GetType(component.ComponentType.TypeIndex);
                if (component.IsBuffer) {
                    s_ApplyBufferMethod.MakeGenericMethod(managedType).Invoke(null, new object[] { entityManager, snapshot.TargetEntity, component.Value });
                } else if (component.Value != null) {
                    s_ApplyComponentMethod.MakeGenericMethod(managedType).Invoke(null, new object[] { entityManager, snapshot.TargetEntity, component.Value });
                }

                if (component.ComponentType.IsEnableable) {
                    entityManager.SetComponentEnabled(snapshot.TargetEntity, component.ComponentType, component.Enabled);
                }
            }
        }

        /// <summary>
        /// Captures a dynamic buffer as a managed array.
        /// </summary>
        /// <typeparam name="T">The buffer element type.</typeparam>
        /// <param name="entityManager">The entity manager that owns the buffer.</param>
        /// <param name="entity">The entity containing the buffer.</param>
        /// <returns>The captured buffer values.</returns>
        private static T[] CaptureBuffer<T>(EntityManager entityManager, Entity entity) where T : unmanaged, IBufferElementData
        {
            var buffer = entityManager.GetBuffer<T>(entity);
            var values = new T[buffer.Length];
            for (int i = 0; i < buffer.Length; ++i) {
                values[i] = buffer[i];
            }
            return values;
        }

        /// <summary>
        /// Captures an unmanaged component value.
        /// </summary>
        /// <typeparam name="T">The component type.</typeparam>
        /// <param name="entityManager">The entity manager that owns the component.</param>
        /// <param name="entity">The entity containing the component.</param>
        /// <returns>The captured component value.</returns>
        private static T CaptureComponent<T>(EntityManager entityManager, Entity entity) where T : unmanaged, IComponentData
        {
            return entityManager.GetComponentData<T>(entity);
        }

        /// <summary>
        /// Restores a captured dynamic buffer.
        /// </summary>
        /// <typeparam name="T">The buffer element type.</typeparam>
        /// <param name="entityManager">The entity manager that owns the target entity.</param>
        /// <param name="entity">The entity containing the target buffer.</param>
        /// <param name="boxedValues">The boxed managed array of buffer values.</param>
        private static void ApplyBuffer<T>(EntityManager entityManager, Entity entity, object boxedValues) where T : unmanaged, IBufferElementData
        {
            var values = (T[])boxedValues;
            var buffer = entityManager.GetBuffer<T>(entity);
            buffer.Clear();
            buffer.EnsureCapacity(values.Length);
            for (int i = 0; i < values.Length; ++i) {
                buffer.Add(values[i]);
            }
        }

        /// <summary>
        /// Restores a captured unmanaged component value.
        /// </summary>
        /// <typeparam name="T">The component type.</typeparam>
        /// <param name="entityManager">The entity manager that owns the target entity.</param>
        /// <param name="entity">The entity containing the target component.</param>
        /// <param name="boxedValue">The boxed component value.</param>
        private static void ApplyComponent<T>(EntityManager entityManager, Entity entity, object boxedValue) where T : unmanaged, IComponentData
        {
            entityManager.SetComponentData(entity, (T)boxedValue);
        }
    }

    /// <summary>
    /// Contains the captured behavior-tree data needed during the baking-system pass.
    /// </summary>
    [TemporaryBakingType]
    internal sealed class BehaviorTreeBakingData : IComponentData
    {
        internal readonly BehaviorTreeBakingSnapshot Snapshot;

        /// <summary>
        /// Creates empty temporary baking data for the Entities type registry.
        /// </summary>
        public BehaviorTreeBakingData()
        {
        }

        /// <summary>
        /// Creates the temporary baking data.
        /// </summary>
        /// <param name="snapshot">The captured behavior-tree entity data.</param>
        internal BehaviorTreeBakingData(BehaviorTreeBakingSnapshot snapshot)
        {
            Snapshot = snapshot;
        }
    }

    /// <summary>
    /// Populates the behavior-tree components declared by <see cref="BehaviorTreeBaker"/>.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    internal partial class BehaviorTreeBakingSystem : SystemBase
    {
        /// <summary>
        /// Populates every behavior tree that was processed during the current bake pass.
        /// </summary>
        protected override void OnUpdate()
        {
            CompleteDependency();
            var query = SystemAPI.QueryBuilder().WithAll<BehaviorTreeBakingData>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities).Build();
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; ++i) {
                var bakingData = EntityManager.GetComponentObject<BehaviorTreeBakingData>(entities[i]);
                if (bakingData?.Snapshot == null) {
                    continue;
                }
                BehaviorTreeBakingSnapshotUtility.Apply(EntityManager, bakingData.Snapshot);
            }
        }
    }
}
#endif