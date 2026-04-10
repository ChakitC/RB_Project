#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Systems
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.BehaviorDesigner.Runtime.Groups;
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.BehaviorDesigner.Runtime.Utility;
    using Opsive.GraphDesigner.Runtime;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// Specifies that the node is an object task which can specify the next child that should run.
    /// </summary>
    public interface ITaskObjectParentNode
    {
        /// <summary>
        /// Returns the index of the next child that should run. Set to ushort.MaxValue to ignore.
        /// </summary>
        ushort NextChildIndex { get; }
    }

    /// <summary>
    /// The DOTS data structure for the TaskObject class.
    /// </summary>
    public struct TaskObjectComponent : IBufferElementData
    {
        [Tooltip("The index of the task.")]
        public ushort Index;
    }

    /// <summary>
    /// A DOTS flag indicating when an TaskObject node is active.
    /// </summary>
    public struct TaskObjectFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Utility methods for synchronizing ECS-backed SharedVariables around managed task execution.
    /// </summary>
    internal static class TaskObjectSharedVariableSyncUtility
    {
        /// <summary>
        /// Syncs ECS-backed SharedVariables to the managed variables once for the specified entity.
        /// </summary>
        /// <param name="world">The world containing the entity.</param>
        /// <param name="entity">The entity being updated.</param>
        /// <param name="behaviorTree">The behavior tree associated with the entity.</param>
        /// <param name="syncedEntities">The entities that have already been synchronized this pass.</param>
        /// <param name="touchedEntities">The entities that need their managed values flushed back to ECS.</param>
        public static void SyncToManagedIfNeeded(World world, Entity entity, BehaviorTree behaviorTree, NativeParallelHashSet<Entity> syncedEntities, NativeList<Entity> touchedEntities)
        {
            if (behaviorTree == null || !behaviorTree.HasECSVariableSync(world, entity) || !syncedEntities.Add(entity)) {
                return;
            }

            behaviorTree.SyncECSVariablesToManaged(world, entity);
            touchedEntities.Add(entity);
        }

        /// <summary>
        /// Flushes the managed SharedVariable values for the touched entities back into ECS.
        /// </summary>
        /// <param name="world">The world containing the entities.</param>
        /// <param name="touchedEntities">The entities whose managed SharedVariables should be synced back into ECS.</param>
        public static void SyncTouchedEntitiesToECS(World world, NativeList<Entity> touchedEntities)
        {
            for (int i = 0; i < touchedEntities.Length; ++i) {
                var entity = touchedEntities[i];
                var behaviorTree = BehaviorTree.GetBehaviorTree(entity);
                if (behaviorTree == null) {
                    continue;
                }

                behaviorTree.SyncManagedVariablesToECS(world, entity);
            }
        }
    }

    /// <summary>
    /// Runs the TaskObject logic.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(TraversalTaskSystemGroup), OrderLast = true)]
    public partial struct TaskObjectSystem : ISystem
    {
        private EntityQuery m_InterruptedTaskQuery;
        private EntityQuery m_TaskObjectQuery;

        /// <summary>
        /// Creates the queries used by the system.
        /// </summary>
        /// <param name="state">The current system state.</param>
        private void OnCreate(ref SystemState state)
        {
            m_InterruptedTaskQuery = SystemAPI.QueryBuilder().WithAll<InterruptedFlag, TaskObjectComponent, TaskComponent>().Build();
            m_TaskObjectQuery = SystemAPI.QueryBuilder().WithAll<TaskObjectFlag, EvaluateFlag, TaskObjectComponent, TaskComponent, BranchComponent>().Build();
        }

        /// <summary>
        /// Updates the logic.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnUpdate(ref SystemState state)
        {
            var entityCapacity = Mathf.Max(1, m_InterruptedTaskQuery.CalculateEntityCount() + m_TaskObjectQuery.CalculateEntityCount());
            using var syncedEntities = new NativeParallelHashSet<Entity>(entityCapacity, Allocator.Temp);
            using var touchedEntities = new NativeList<Entity>(entityCapacity, Allocator.Temp);

            // When the task is interrupted there is no callback which prevents Task.OnEnd from being called. Track the status within the referenced task object and if the status is different then
            // the task was aborted and OnEnd needs to be called.
            foreach (var (taskObjectComponents, taskComponents, entity) in
                SystemAPI.Query<DynamicBuffer<TaskObjectComponent>, DynamicBuffer<TaskComponent>>().WithAll<InterruptedFlag>().WithEntityAccess()) {
                var behaviorTree = BehaviorTree.GetBehaviorTree(entity);
                if (behaviorTree == null) {
                    continue;
                }

                TaskObjectSharedVariableSyncUtility.SyncToManagedIfNeeded(state.World, entity, behaviorTree, syncedEntities, touchedEntities);

                for (int i = 0; i < taskObjectComponents.Length; ++i) {
                    var taskObjectComponent = taskObjectComponents[i];
                    var taskComponent = taskComponents[taskObjectComponent.Index];
                    if (taskComponent.Status != TaskStatus.Queued && taskComponent.Status != TaskStatus.Running) {
                        var task = behaviorTree.GetTaskObject(taskObjectComponent.Index);
                        if (task == null) {
                            continue;
                        }
                        if (task.Status != taskComponent.Status) {
                            task.OnEnd();
                            task.Status = taskComponent.Status;
                        }
                    }
                }
            }

            // Update the task objects.
            foreach (var (taskObjectComponents, taskComponents, branchComponents, entity) in
                SystemAPI.Query<DynamicBuffer<TaskObjectComponent>, DynamicBuffer<TaskComponent>, DynamicBuffer<BranchComponent>>().WithAll<TaskObjectFlag, EvaluateFlag>().WithEntityAccess()) {

                var behaviorTree = BehaviorTree.GetBehaviorTree(entity);
                if (behaviorTree == null) {
                    continue;
                }
                TaskObjectSharedVariableSyncUtility.SyncToManagedIfNeeded(state.World, entity, behaviorTree, syncedEntities, touchedEntities);
                var hasInterruptComponents = SystemAPI.HasComponent<InterruptFlag>(entity);
                var interruptedFlagEnabled = hasInterruptComponents && SystemAPI.IsComponentEnabled<InterruptedFlag>(entity);
                var taskComponentBuffer = taskComponents;
                var branchComponentBuffer = branchComponents;

                for (int i = 0; i < taskObjectComponents.Length; ++i) {
                    var taskObjectComponent = taskObjectComponents[i];
                    var taskComponent = taskComponents[taskObjectComponent.Index];
                    var branchComponent = branchComponents[taskComponent.BranchIndex];
                    if (!branchComponent.CanExecute || branchComponent.ActiveIndex != taskComponent.Index) {
                        continue;
                    }

                    var task = behaviorTree.GetTaskObject(taskObjectComponent.Index);
                    if (task == null) {
                        continue;
                    }
                    if (taskComponent.Status == TaskStatus.Queued) {
                        task.Status = taskComponent.Status = TaskStatus.Running;
                        taskComponentBuffer[taskComponent.Index] = taskComponent;

                        task.OnStart();
                    }
                    if (taskComponent.Status != TaskStatus.Running) {
                        continue;
                    }

                    var status = task.OnUpdate();
                    // Update the status if has changed.
                    if (status != taskComponent.Status) {
                        task.Status = taskComponent.Status = status;
                        taskComponentBuffer[taskComponent.Index] = taskComponent;

                        // End the task if it is done running.
                        if (status != TaskStatus.Running) {
                            task.OnEnd();

                            branchComponent = branchComponentBuffer[taskComponent.BranchIndex];
                            branchComponent.NextIndex = taskComponent.ParentIndex;
                            branchComponentBuffer[taskComponent.BranchIndex] = branchComponent;
                        }
                    }

                    var taskObjectParentNode = behaviorTree.GetTaskObjectParent(taskObjectComponent.Index);
                    if (taskObjectParentNode != null) {
                        if (status == TaskStatus.Running) {
                            // Parent object tasks do not have a direct way to set the next child. Use the ITaskObjectParentNode to switch the child task.
                            var nextChildIndex = taskObjectParentNode.NextChildIndex;
                            if (nextChildIndex != ushort.MaxValue && nextChildIndex < taskComponents.Length) {
                                var nextTaskComponent = taskComponents[nextChildIndex];
                                if (nextTaskComponent.Status != TaskStatus.Running) {
                                    branchComponent = branchComponentBuffer[nextTaskComponent.BranchIndex];
                                    if (branchComponent.NextIndex != nextChildIndex) {
                                        branchComponent.NextIndex = nextChildIndex;
                                        branchComponentBuffer[nextTaskComponent.BranchIndex] = branchComponent;
                                    }

                                    if (nextTaskComponent.Status != TaskStatus.Queued) {
                                        nextTaskComponent.Status = TaskStatus.Queued;
                                        taskComponentBuffer[nextChildIndex] = nextTaskComponent;
                                    }
                                }
                            }
                        } else if (status == TaskStatus.Success || status == TaskStatus.Failure) {
                            // An interrupt should occur if the parent returns a success or failure status before the children.
                            var childCount = TraversalUtility.GetChildCount(taskComponent.Index, ref taskComponentBuffer);
                            var startIndex = taskComponent.Index + 1;
                            var endIndex = Mathf.Min(startIndex + childCount, taskComponentBuffer.Length);
                            for (int j = startIndex; j < endIndex; ++j) {
                                var childTaskComponent = taskComponentBuffer[j];
                                if (childTaskComponent.Status == TaskStatus.Running || childTaskComponent.Status == TaskStatus.Queued) {
                                    childTaskComponent.Status = status;
                                    taskComponentBuffer[j] = childTaskComponent;

                                    branchComponent = branchComponentBuffer[childTaskComponent.BranchIndex];
                                    if (!hasInterruptComponents) {
                                        ComponentUtility.AddInterruptComponents(behaviorTree.World.EntityManager, entity);
                                        hasInterruptComponents = true;
                                    }
                                    if (!interruptedFlagEnabled) {
                                        SystemAPI.SetComponentEnabled<InterruptedFlag>(entity, true);
                                        interruptedFlagEnabled = true;
                                    }
                                    if (branchComponent.ActiveIndex == childTaskComponent.Index) {
                                        branchComponent.NextIndex = ushort.MaxValue;
                                        branchComponentBuffer[childTaskComponent.BranchIndex] = branchComponent;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            TaskObjectSharedVariableSyncUtility.SyncTouchedEntitiesToECS(state.World, touchedEntities);
        }
    }

    /// <summary>
    /// A DOTS tag indicating when an TaskObject node needs to be reevaluated.
    /// </summary>
    public struct TaskObjectReevaluateFlag : IComponentData, IEnableableComponent
    {
    }

    /// <summary>
    /// Runs the TaskObject reevaluation logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct TaskObjectReevaluateSystem : ISystem
    {
        private EntityQuery m_ReevaluateTaskQuery;

        /// <summary>
        /// Creates the queries used by the system.
        /// </summary>
        /// <param name="state">The current system state.</param>
        private void OnCreate(ref SystemState state)
        {
            m_ReevaluateTaskQuery = SystemAPI.QueryBuilder().WithAll<TaskObjectReevaluateFlag, EvaluateFlag, TaskObjectComponent, TaskComponent>().Build();
        }

        /// <summary>
        /// Updates the reevaluation logic.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnUpdate(ref SystemState state)
        {
            var entityCapacity = Mathf.Max(1, m_ReevaluateTaskQuery.CalculateEntityCount());
            using var syncedEntities = new NativeParallelHashSet<Entity>(entityCapacity, Allocator.Temp);
            using var touchedEntities = new NativeList<Entity>(entityCapacity, Allocator.Temp);

            foreach (var (taskComponents, taskObjectComponents, entity) in
                SystemAPI.Query<DynamicBuffer<TaskComponent>, DynamicBuffer<TaskObjectComponent>>().WithAll<TaskObjectReevaluateFlag, EvaluateFlag>().WithEntityAccess()) {
                var behaviorTree = BehaviorTree.GetBehaviorTree(entity);
                if (behaviorTree == null) {
                    continue;
                }
                TaskObjectSharedVariableSyncUtility.SyncToManagedIfNeeded(state.World, entity, behaviorTree, syncedEntities, touchedEntities);
                for (int i = 0; i < taskObjectComponents.Length; ++i) {
                    var taskObjectComponent = taskObjectComponents[i];
                    var taskComponent = taskComponents[taskObjectComponent.Index];
                    if (!taskComponent.Reevaluate) {
                        continue;
                    }

                    var task = behaviorTree.GetConditionalReevaluationTaskObject(taskObjectComponent.Index);
                    if (task == null) {
                        continue;
                    }
                    var status = task.OnReevaluateUpdate();
                    if (status != taskComponent.Status) {
                        taskComponent.Status = status;
                        var buffer = taskComponents;
                        buffer[taskComponent.Index] = taskComponent;
                    }
                }
            }

            TaskObjectSharedVariableSyncUtility.SyncTouchedEntitiesToECS(state.World, touchedEntities);
        }
    }
}
#endif