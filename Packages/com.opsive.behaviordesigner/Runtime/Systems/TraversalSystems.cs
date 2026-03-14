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
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using UnityEngine;

    /// <summary>
    /// Traverses and ensures the correct tasks are active.
    /// </summary>
    [UpdateInGroup(typeof(TraversalSystemGroup))]
    [UpdateAfter(typeof(TraversalTaskSystemGroup))]
    public partial struct EvaluationSystem : ISystem
    {
        private bool m_JobScheduled;
        private EntityQuery m_Query;
        private JobHandle m_Dependency;
        private EntityCommandBuffer m_EntityCommandBuffer;

        /// <summary>
        /// The system has been created.
        /// </summary>
        /// <param name="state">The state of the system.</param>
        private void OnCreate(ref SystemState state)
        {
            m_JobScheduled = false;
            m_Query = SystemAPI.QueryBuilder().WithAllRW<TaskComponent>().WithAllRW<BranchComponent>().WithAbsent<BakedBehaviorTree>().Build();
        }

        /// <summary>
        /// Starts the job which traverses the tree.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnUpdate(ref SystemState state)
        {
            m_JobScheduled = true;

            m_EntityCommandBuffer = new EntityCommandBuffer(state.WorldUpdateAllocator);
            m_Dependency = state.Dependency = new EvaluationJob()
            {
                EntityCommandBuffer = m_EntityCommandBuffer.AsParallelWriter(),
            }.ScheduleParallel(m_Query, state.Dependency);
        }

        /// <summary>
        /// Completes the job and releases any memory.
        /// </summary>
        /// <param name="entityManager">The running EntityManager.</param>
        /// <param name="stopRunning">Has the system been stopped?</param>
        [BurstCompile]
        public void Complete(EntityManager entityManager, bool stopRunning = false)
        {
            if (!m_JobScheduled) {
                return;
            }

            if (!stopRunning) {
                m_Dependency.Complete();
                m_EntityCommandBuffer.Playback(entityManager);
                m_EntityCommandBuffer.Dispose();
            }

            m_JobScheduled = false;
        }

        /// <summary>
        /// Job which traverses the tree.
        /// </summary>
        [BurstCompile]
        public partial struct EvaluationJob : IJobEntity
        {
            [Tooltip("CommandBuffer which sets the component data.")]
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            /// <summary>
            /// Executes the job.
            /// </summary>
            /// <param name="entity">The entity that is being acted upon.</param>
            /// <param name="entityIndex">The index of the entity.</param
            /// <param name="branchComponents">An array of branch components.</param>
            /// <param name="taskComponents">An array of task components.</param>
            [BurstCompile]
            public void Execute(Entity entity, [EntityIndexInQuery] int entityIndex, ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents)
            {
                for (int i = 0; i < branchComponents.Length; ++i) {
                    var branchComponent = branchComponents[i];
                    if (branchComponent.ActiveIndex != ushort.MaxValue && branchComponent.ActiveIndex == branchComponent.NextIndex) {
                        var activeTask = taskComponents[branchComponent.ActiveIndex];
                        if (activeTask.Status == TaskStatus.Success || activeTask.Status == TaskStatus.Failure) {
                            branchComponent.NextIndex = activeTask.ParentIndex;
                        }
                    }
                    if (branchComponent.ActiveIndex != branchComponent.NextIndex) {
                        // Do not switch into a disabled task.
                        if (branchComponent.NextIndex != ushort.MaxValue && taskComponents[branchComponent.NextIndex].Disabled) {
                            var taskComponent = taskComponents[branchComponent.NextIndex];
                            taskComponent.Status = TaskStatus.Inactive;
                            var taskComponentBuffer = taskComponents;
                            taskComponentBuffer[branchComponent.NextIndex] = taskComponent;

                            branchComponent.NextIndex = branchComponent.ActiveIndex;
                        } else {
                            // The status for all children should be reset back to their inactive state if the next task is within a new branch. This will prevent
                            // the return status from being reset when the task ends normally.
                            var taskComponentBuffer = taskComponents;
                            if (branchComponent.NextIndex != ushort.MaxValue &&
                                !TraversalUtility.IsParent((ushort)branchComponent.ActiveIndex, (ushort)branchComponent.NextIndex, ref taskComponentBuffer)) {
                                var nextTaskComponent = taskComponents[branchComponent.NextIndex];
                                if (branchComponent.ActiveIndex != ushort.MaxValue && nextTaskComponent.Status != TaskStatus.Running) { // If the next task is already running then an interrupt has already reset the children.
                                    var childCount = TraversalUtility.GetChildCount(branchComponent.NextIndex, ref taskComponentBuffer);
                                    for (int j = 0; j < childCount; ++j) {
                                        var childTaskComponent = taskComponents[branchComponent.NextIndex + j + 1];
                                        childTaskComponent.Status = TaskStatus.Inactive;
                                        taskComponentBuffer[branchComponent.NextIndex + j + 1] = childTaskComponent;
                                    }
                                }
                                nextTaskComponent.Status = nextTaskComponent.Status == TaskStatus.Running ? TaskStatus.Running : TaskStatus.Queued;
                                taskComponentBuffer[branchComponent.NextIndex] = nextTaskComponent;
                            }
                            branchComponent.ActiveIndex = branchComponent.NextIndex;

                            // Change the component tag if the task type is different.
                            var componentType = branchComponent.ActiveIndex != ushort.MaxValue ? taskComponents[branchComponent.ActiveIndex].FlagComponentType : new ComponentType();
                            if (componentType != branchComponent.ActiveFlagComponentType) {
                                if (branchComponent.ActiveFlagComponentType.TypeIndex != TypeIndex.Null) {
                                    var deactivateTag = true;
                                    for (int j = 0; j < branchComponents.Length; ++j) {
                                        // The tag should be deactivated if no other tasks have the same tag type.
                                        if (i != j && branchComponents[j].ActiveIndex != ushort.MaxValue &&
                                            branchComponent.ActiveFlagComponentType == branchComponents[j].ActiveFlagComponentType) {
                                            deactivateTag = false;
                                            break;
                                        }
                                    }

                                    // The task of that type is no longer active - disable the system to prevent it from running.
                                    if (deactivateTag) {
                                        EntityCommandBuffer.SetComponentEnabled(entityIndex, entity, branchComponent.ActiveFlagComponentType, false);
                                    }
                                }
                                // A new system type should start.
                                if (branchComponent.ActiveIndex != ushort.MaxValue) {
                                    var taskComponent = taskComponents[branchComponent.ActiveIndex];
                                    EntityCommandBuffer.SetComponentEnabled(entityIndex, entity, taskComponent.FlagComponentType, true);
                                }
                                branchComponent.ActiveFlagComponentType = componentType;
                            }
                        }
                        var branchComponentBuffer = branchComponents;
                        branchComponentBuffer[i] = branchComponent;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Loops through the active tasks to determine if the system should stay active for the current tick.
    /// </summary>
    [UpdateInGroup(typeof(TraversalSystemGroup))]
    [UpdateAfter(typeof(EvaluationSystem))]
    public partial struct DetermineEvaluationSystem : ISystem
    {
        [Tooltip("Should the group stay active? An inactive tree does not run.")]
        public bool Active { get; private set; }
        [Tooltip("Should the group be evaluated? This bool indicates if the entire tree should be evaluated instead of the reevaluation" +
                 "concept for conditional aborts. The tree will be reevaluated if any of the leaf tasks have a status of running.")]
        public bool Evaluate { get; private set; }

        private bool m_JobScheduled;
        private JobHandle m_Dependency;

        private EntityQuery m_Query32;
        private EntityQuery m_Query64;
        private EntityQuery m_Query128;
        private EntityQuery m_Query512;
        private EntityQuery m_Query4096;
        private EntityCommandBuffer m_EntityCommandBuffer32;
        private EntityCommandBuffer m_EntityCommandBuffer64;
        private EntityCommandBuffer m_EntityCommandBuffer128;
        private EntityCommandBuffer m_EntityCommandBuffer512;
        private EntityCommandBuffer m_EntityCommandBuffer4096;

        private NativeArray<bool> m_Results;

        /// <summary>
        /// The system has been created.
        /// </summary>
        /// <param name="state">The state of the system.</param>
        private void OnCreate(ref SystemState state)
        {
            Active = Evaluate = true;
            m_JobScheduled = false;
            m_Query32 = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAll<TaskComponent>().WithAll<EvaluationComponent32>().WithAll<EvaluateFlag>().WithAbsent<BakedBehaviorTree>().Build();
            m_Query64 = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAll<TaskComponent>().WithAll<EvaluationComponent64>().WithAll<EvaluateFlag>().WithAbsent<BakedBehaviorTree>().Build();
            m_Query128 = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAll<TaskComponent>().WithAll<EvaluationComponent128>().WithAll<EvaluateFlag>().WithAbsent<BakedBehaviorTree>().Build();
            m_Query512 = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAll<TaskComponent>().WithAll<EvaluationComponent512>().WithAll<EvaluateFlag>().WithAbsent<BakedBehaviorTree>().Build();
            m_Query4096 = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAll<TaskComponent>().WithAll<EvaluationComponent4096>().WithAll<EvaluateFlag>().WithAbsent<BakedBehaviorTree>().Build();
        }

        /// <summary>
        /// Executes the job to determine if the system should stay active and evaluating.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            Active = Evaluate = true;
            m_JobScheduled = true;

            m_EntityCommandBuffer32 = new EntityCommandBuffer(Allocator.TempJob);
            m_EntityCommandBuffer64 = new EntityCommandBuffer(Allocator.TempJob);
            m_EntityCommandBuffer128 = new EntityCommandBuffer(Allocator.TempJob);
            m_EntityCommandBuffer512 = new EntityCommandBuffer(Allocator.TempJob);
            m_EntityCommandBuffer4096 = new EntityCommandBuffer(Allocator.TempJob);

            m_Results = new NativeArray<bool>(3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < m_Results.Length; ++i) {
                m_Results[i] = false;
            }

            m_Dependency = state.Dependency;
            
            var job32 = new DetermineEvaluationJob32()
            {
                EntityCommandBuffer = m_EntityCommandBuffer32.AsParallelWriter(),
                Results = m_Results
            }.ScheduleParallel(m_Query32, m_Dependency);
            var job64 = new DetermineEvaluationJob64()
            {
                EntityCommandBuffer = m_EntityCommandBuffer64.AsParallelWriter(),
                Results = m_Results
            }.ScheduleParallel(m_Query64, m_Dependency);
            var job128 = new DetermineEvaluationJob128()
            {
                EntityCommandBuffer = m_EntityCommandBuffer128.AsParallelWriter(),
                Results = m_Results
            }.ScheduleParallel(m_Query128, m_Dependency);
            var job512 = new DetermineEvaluationJob512()
            {
                EntityCommandBuffer = m_EntityCommandBuffer512.AsParallelWriter(),
                Results = m_Results
            }.ScheduleParallel(m_Query512, m_Dependency);
            var job4096 = new DetermineEvaluationJob4096()
            {
                EntityCommandBuffer = m_EntityCommandBuffer4096.AsParallelWriter(),
                Results = m_Results
            }.ScheduleParallel(m_Query4096, m_Dependency);

            m_Dependency = state.Dependency = JobHandle.CombineDependencies(JobHandle.CombineDependencies(job32, job64, job128), job512, job4096);
        }

        /// <summary>
        /// Completes the job and releases any memory.
        /// </summary>
        /// <param name="entityManager">The running EntityManager.</param>
        [BurstCompile]
        public void Complete(EntityManager entityManager)
        {
            if (!m_JobScheduled) {
                return;
            }

            m_Dependency.Complete();
            m_EntityCommandBuffer32.Playback(entityManager);
            m_EntityCommandBuffer32.Dispose();
            m_EntityCommandBuffer64.Playback(entityManager);
            m_EntityCommandBuffer64.Dispose();
            m_EntityCommandBuffer128.Playback(entityManager);
            m_EntityCommandBuffer128.Dispose();
            m_EntityCommandBuffer512.Playback(entityManager);
            m_EntityCommandBuffer512.Dispose();
            m_EntityCommandBuffer4096.Playback(entityManager);
            m_EntityCommandBuffer4096.Dispose();

            if (m_Results.IsCreated) {
                if (m_Results[0]) {
                    Active = m_Results[1];
                    Evaluate = m_Results[2];
                } else {
                    // If the first element is false then no trees executed.
                    Active = Evaluate = false;
                }
                m_Results.Dispose();
            }
            m_JobScheduled = false;
        }

        /// <summary>
        /// The system has been destroyed.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnDestroy(ref SystemState state)
        {
            if (m_Dependency.IsCompleted) {
                return;
            }

            m_Results.Dispose();
        }

        /// <summary>
        /// Job which determine if the system should stay active. If any behavior tree should stay active then the entire system must remain active.
        /// </summary>
        [BurstCompile]
        public partial struct DetermineEvaluationJob32 : IJobEntity
        {
            [Tooltip("CommandBuffer which sets the component data.")]
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;
            [Tooltip("The computed results.")]
            [NativeDisableParallelForRestriction] public NativeArray<bool> Results;

            /// <summary>
            /// Executes the job.
            /// </summary>
            /// <param name="entity">The entity that is being acted upon.</param>
            /// <param name="entityIndex">The index of the entity.</param>
            /// <param name="branchComponents">An array of branch components.</param>
            /// <param name="taskComponents">An array of task components.</param>
            /// <param name="evaluationComponent">The EvaluationComponent that belongs to the entity.</param>
            [BurstCompile]
            private void Execute(Entity entity, [EntityIndexInQuery] int entityIndex, ref DynamicBuffer<BranchComponent> branchComponents, in DynamicBuffer<TaskComponent> taskComponents, ref EvaluationComponent32 evaluationComponent)
            {
                var evaluatedTasks = evaluationComponent.EvaluatedTasks;
                EvaluationUtility.DetermineEvaluation(entity, entityIndex, ref branchComponents, taskComponents, ref evaluatedTasks, evaluationComponent.EvaluationType, evaluationComponent.MaxEvaluationCount, EntityCommandBuffer, Results);
                evaluationComponent.EvaluatedTasks = evaluatedTasks;
            }
        }

        /// <summary>
        /// Job which determine if the system should stay active. If any behavior tree should stay active then the entire system must remain active.
        /// </summary>
        [BurstCompile]
        public partial struct DetermineEvaluationJob64 : IJobEntity
        {
            [Tooltip("CommandBuffer which sets the component data.")]
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;
            [Tooltip("The computed results.")]
            [NativeDisableParallelForRestriction] public NativeArray<bool> Results;

            /// <summary>
            /// Executes the job.
            /// </summary>
            /// <param name="entity">The entity that is being acted upon.</param>
            /// <param name="entityIndex">The index of the entity.</param>
            /// <param name="branchComponents">An array of branch components.</param>
            /// <param name="taskComponents">An array of task components.</param>
            /// <param name="evaluationComponent">The EvaluationComponent that belongs to the entity.</param>
            [BurstCompile]
            private void Execute(Entity entity, [EntityIndexInQuery] int entityIndex, ref DynamicBuffer<BranchComponent> branchComponents, in DynamicBuffer<TaskComponent> taskComponents, ref EvaluationComponent64 evaluationComponent)
            {
                var evaluatedTasks = evaluationComponent.EvaluatedTasks;
                EvaluationUtility.DetermineEvaluation(entity, entityIndex, ref branchComponents, taskComponents, ref evaluatedTasks, evaluationComponent.EvaluationType, evaluationComponent.MaxEvaluationCount, EntityCommandBuffer, Results);
                evaluationComponent.EvaluatedTasks = evaluatedTasks;
            }
        }

        /// <summary>
        /// Job which determine if the system should stay active. If any behavior tree should stay active then the entire system must remain active.
        /// </summary>
        [BurstCompile]
        public partial struct DetermineEvaluationJob128 : IJobEntity
        {
            [Tooltip("CommandBuffer which sets the component data.")]
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;
            [Tooltip("The computed results.")]
            [NativeDisableParallelForRestriction] public NativeArray<bool> Results;

            /// <summary>
            /// Executes the job.
            /// </summary>
            /// <param name="entity">The entity that is being acted upon.</param>
            /// <param name="entityIndex">The index of the entity.</param>
            /// <param name="branchComponents">An array of branch components.</param>
            /// <param name="taskComponents">An array of task components.</param>
            /// <param name="evaluationComponent">The EvaluationComponent that belongs to the entity.</param>
            [BurstCompile]
            private void Execute(Entity entity, [EntityIndexInQuery] int entityIndex, ref DynamicBuffer<BranchComponent> branchComponents, in DynamicBuffer<TaskComponent> taskComponents, ref EvaluationComponent128 evaluationComponent)
            {
                var evaluatedTasks = evaluationComponent.EvaluatedTasks;
                EvaluationUtility.DetermineEvaluation(entity, entityIndex, ref branchComponents, taskComponents, ref evaluatedTasks, evaluationComponent.EvaluationType, evaluationComponent.MaxEvaluationCount, EntityCommandBuffer, Results);
                evaluationComponent.EvaluatedTasks = evaluatedTasks;
            }
        }


        /// <summary>
        /// Job which determine if the system should stay active. If any behavior tree should stay active then the entire system must remain active.
        /// </summary>
        [BurstCompile]
        public partial struct DetermineEvaluationJob512 : IJobEntity
        {
            [Tooltip("CommandBuffer which sets the component data.")]
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;
            [Tooltip("The computed results.")]
            [NativeDisableParallelForRestriction] public NativeArray<bool> Results;

            /// <summary>
            /// Executes the job.
            /// </summary>
            /// <param name="entity">The entity that is being acted upon.</param>
            /// <param name="entityIndex">The index of the entity.</param>
            /// <param name="branchComponents">An array of branch components.</param>
            /// <param name="taskComponents">An array of task components.</param>
            /// <param name="evaluationComponent">The EvaluationComponent that belongs to the entity.</param>
            [BurstCompile]
            private void Execute(Entity entity, [EntityIndexInQuery] int entityIndex, ref DynamicBuffer<BranchComponent> branchComponents, in DynamicBuffer<TaskComponent> taskComponents, ref EvaluationComponent512 evaluationComponent)
            {
                var evaluatedTasks = evaluationComponent.EvaluatedTasks;
                EvaluationUtility.DetermineEvaluation(entity, entityIndex, ref branchComponents, taskComponents, ref evaluatedTasks, evaluationComponent.EvaluationType, evaluationComponent.MaxEvaluationCount, EntityCommandBuffer, Results);
                evaluationComponent.EvaluatedTasks = evaluatedTasks;
            }
        }

        /// <summary>
        /// Job which determine if the system should stay active. If any behavior tree should stay active then the entire system must remain active.
        /// </summary>
        [BurstCompile]
        public partial struct DetermineEvaluationJob4096 : IJobEntity
        {
            [Tooltip("CommandBuffer which sets the component data.")]
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;
            [Tooltip("The computed results.")]
            [NativeDisableParallelForRestriction] public NativeArray<bool> Results;

            /// <summary>
            /// Executes the job.
            /// </summary>
            /// <param name="entity">The entity that is being acted upon.</param>
            /// <param name="entityIndex">The index of the entity.</param>
            /// <param name="branchComponents">An array of branch components.</param>
            /// <param name="taskComponents">An array of task components.</param>
            /// <param name="evaluationComponent">The EvaluationComponent that belongs to the entity.</param>
            [BurstCompile]
            private void Execute(Entity entity, [EntityIndexInQuery] int entityIndex, ref DynamicBuffer<BranchComponent> branchComponents, in DynamicBuffer<TaskComponent> taskComponents, ref EvaluationComponent4096 evaluationComponent)
            {
                var evaluatedTasks = evaluationComponent.EvaluatedTasks;
                EvaluationUtility.DetermineEvaluation(entity, entityIndex, ref branchComponents, taskComponents, ref evaluatedTasks, evaluationComponent.EvaluationType, evaluationComponent.MaxEvaluationCount, EntityCommandBuffer, Results);
                evaluationComponent.EvaluatedTasks = evaluatedTasks;
            }
        }
    }

    /// <summary>
    /// Utility functions for the task evaluation.
    /// </summary>
    [BurstCompile]
    public struct EvaluationUtility
    {
        /// <summary>
        /// Is the task at the specified index a parent task.
        /// </summary>
        /// <param name="taskComponents">An array of task components.</param>
        /// <param name="index">The index to check if it is a parent.</param>
        /// <returns>True if the task at the specified index is a parent task.</returns>
        [BurstCompile]
        public static bool IsParentTask(ref DynamicBuffer<TaskComponent> taskComponents, int index)
        {
            // The last task cannot be a parent.
            if (index == taskComponents.Length - 1) {
                return false;
            }

            // The next child will have a parent of the current task.
            if (taskComponents[index + 1].ParentIndex == index) {
                return true;
            }

            // The parent index is different - the current task is not a parent.
            return false;
        }

        /// <summary>
        /// Core evaluation logic that works with any FixedList type for EvaluatedTasks.
        /// </summary>
        /// <typeparam name="TFixedList">The type of FixedList for EvaluatedTasks.</typeparam>
        /// <param name="entity">The entity that is being acted upon.</param>
        /// <param name="entityIndex">The index of the entity.</param>
        /// <param name="branchComponents">An array of branch components.</param>
        /// <param name="taskComponents">An array of task components.</param>
        /// <param name="evaluatedTasks">The evaluated tasks list.</param>
        /// <param name="evaluationType">The evaluation type.</param>
        /// <param name="maxEvaluationCount">The maximum evaluation count.</param>
        /// <param name="entityCommandBuffer">The command buffer for setting component data.</param>
        /// <param name="results">The computed results array.</param>
        [BurstCompile]
        public static void DetermineEvaluation<TFixedList>(Entity entity, int entityIndex, ref DynamicBuffer<BranchComponent> branchComponents, DynamicBuffer<TaskComponent> taskComponents, ref TFixedList evaluatedTasks, EvaluationType evaluationType, ushort maxEvaluationCount, EntityCommandBuffer.ParallelWriter entityCommandBuffer, NativeArray<bool> results) where TFixedList : struct, INativeList<ulong>
        {
            results[0] = true; // The first element indicates that the job has been executed.

            // No branches may be active.
            var active = false;
            var evaluate = false;
            var evaluatedMask = new FixedList4096Bytes<ulong>();
            for (int i = 0; i < branchComponents.Length; ++i) {
                var branchComponent = branchComponents[i];
                if (branchComponent.ActiveIndex == ushort.MaxValue) {
                    continue;
                }
                active = true;

                // Interrupts are processed in a separate system that is run outside of the task execution system. As a result the branch should not continue to evaluate.
                if (branchComponent.InterruptType != InterruptType.None) {
                    continue;
                }

                var taskComponent = taskComponents[branchComponent.ActiveIndex];
                var isParentTask = EvaluationUtility.IsParentTask(ref taskComponents, branchComponent.ActiveIndex);
                // The branch can evaluate if the active task is an outer node (action or conditional) and is not running OR
                // the task is an inner node (composite or decorator), is running, and is not a parallel task. Parent tasks cannot run without an active child.
                if ((!isParentTask && taskComponent.Status != TaskStatus.Running && taskComponent.ParentIndex != ushort.MaxValue) ||
                    (isParentTask && (taskComponent.Status == TaskStatus.Queued || taskComponent.Status == TaskStatus.Running))) {

                    if (evaluationType == EvaluationType.EntireTree) {
                        // Compute active task bit positions.
                        var bitIndex = branchComponent.ActiveIndex + 1;
                        var arrayIndex = bitIndex / ComponentUtility.ulongBitSize;
                        var bitInUlong = bitIndex % ComponentUtility.ulongBitSize;
                        while (evaluatedMask.Length <= arrayIndex) evaluatedMask.Add(0UL);
                        evaluatedMask[arrayIndex] |= (1UL << bitInUlong);

                        // Prevent evaluating the same task again within the same tick.
                        if (branchComponent.ActiveIndex == branchComponent.LastActiveIndex) {
                            continue;
                        }

                        // Decision to evaluate:
                        // - For parent tasks: always eveluate. The parent task should never be the last executing task.
                        // - For non-parent tasks: evaluate if this task hasn't been evaluated yet.
                        var shouldEvaluate = isParentTask;
                        if (!shouldEvaluate) {
                            shouldEvaluate = (evaluatedTasks[arrayIndex] & (1UL << bitInUlong)) == 0;
                        }

                        if (shouldEvaluate) {
                            evaluate = true;
                            branchComponent.LastActiveIndex = branchComponent.ActiveIndex;
                            branchComponents.ElementAt(i) = branchComponent;
                        }
                    } else {
                        evaluate = true;
                    }
                }
            }

            // If a branch is active then at least one task within that branch is active.
            if (active) {
                results[1] = true; // Active result.

                if (evaluate) {
                    if (evaluationType == EvaluationType.Count) {
                        // Use EvaluatedTasks[0] as the counter.
                        evaluatedTasks[0]++;
                        if (evaluatedTasks[0] >= maxEvaluationCount) {
                            evaluatedTasks[0] = 0;
                            entityCommandBuffer.SetComponentEnabled<EvaluateFlag>(entityIndex, entity, false);
                        } else {
                            results[2] = true; // Evaluate result.
                        }
                    } else {
                        results[2] = true; // Evaluate result.

                        // OR the mask into the EvaluatedTasks list to indicate that the tasks have been evaluated.
                        for (int j = 0; j < evaluatedMask.Length; ++j) {
                            evaluatedTasks[j] |= evaluatedMask[j];
                        }
                    }
                } else {
                    entityCommandBuffer.SetComponentEnabled<EvaluateFlag>(entityIndex, entity, false);
                    if (evaluationType == EvaluationType.EntireTree) {
                        // The system is going to stop evaluating this entity. It will be resumed immediately the next update. Because the DetermineEvaluationJob is run after the tasks
                        // update the EvaluatedTasks value should be set to the next active task. If this value is set to 0 then one extra task will always be executed with subsequent frames.
                        for (int j = 0; j < evaluatedMask.Length; ++j) {
                            evaluatedTasks[j] = evaluatedMask[j];
                        }
                    }
                }
            }
        }
    }
}
#endif