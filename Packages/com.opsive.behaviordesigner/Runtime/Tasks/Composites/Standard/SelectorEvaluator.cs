#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Composites
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.BehaviorDesigner.Runtime.Utility;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.Shared.Utility;
    using System;
    using Unity.Burst;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// A node representation of the selector evaluator task.
    /// </summary>
    [NodeIcon("3a531955b1343524db597a112895cd7a", "35126a690fb2fba4ba6f9d1af773992f")]
    [Opsive.Shared.Utility.Description("The selector evaluator is a selector task which reevaluates its children every tick. It will run the highest priority child which returns a task status of running. " +
                     "This is done each tick. If a lower priority child is running and the next frame a higher priority child wants to run it will interrupt the lower priority child. " +
                     "The selector evaluator will return success as soon as the first child returns success otherwise it will keep trying higher priority children. This task mimics " +
                     "the conditional abort functionality except the child tasks don't always have to be conditional tasks.")]
    public class SelectorEvaluator : ECSCompositeTask<SelectorEvaluatorTaskSystem, SelectorEvaluatorComponent, SelectorEvaluatorFlag>, IParentNode, IParallelNode, ISavableTask, ICloneable
    {
        private ushort m_ComponentIndex;

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override SelectorEvaluatorComponent GetBufferElement()
        {
            return new SelectorEvaluatorComponent()
            {
                Index = RuntimeIndex,
                ActiveChildIndex = ushort.MaxValue,
                ReevaluateChildIndex = ushort.MaxValue,
            };
        }

        /// <summary>
        /// Adds the IBufferElementData to the entity.
        /// </summary>
        /// <param name="world">The world that the entity exists in.</param>
        /// <param name="entity">The entity that the IBufferElementData should be assigned to.</param>
        /// <param name="registry">The ECS variable registry for registering SharedVariable fields.</param>
        /// <param name="gameObject">The GameObject that the entity is attached to.</param>
        /// <returns>The index of the element within the buffer.</returns>
        public override int AddBufferElement(World world, Entity entity, ECSVariableRegistry registry, GameObject gameObject)
        {
            m_ComponentIndex = (ushort)base.AddBufferElement(world, entity, registry, gameObject);
            ComponentUtility.AddInterruptComponents(world.EntityManager, entity);
            return m_ComponentIndex;
        }

        /// <summary>
        /// Specifies the type of reflection that should be used to save the task.
        /// </summary>
        /// <param name="index">The index of the sub-task. This is used for the task set allowing each contained task to have their own save type.</param>
        public MemberVisibility GetSaveReflectionType(int index) { return MemberVisibility.None; }

        /// <summary>
        /// Returns the current task state.
        /// </summary>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        /// <returns>The current task state.</returns>
        public object Save(World world, Entity entity)
        {
            var selectorEvaluatorComponents = world.EntityManager.GetBuffer<SelectorEvaluatorComponent>(entity);
            var selectorEvaluatorComponent = selectorEvaluatorComponents[m_ComponentIndex];

            return new object[] { selectorEvaluatorComponent.ActiveChildIndex, selectorEvaluatorComponent.ReevaluateChildIndex };
        }

        /// <summary>
        /// Loads the previous task state.
        /// </summary>
        /// <param name="saveData">The previous task state.</param>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        public void Load(object saveData, World world, Entity entity)
        {
            var selectorEvaluatorComponents = world.EntityManager.GetBuffer<SelectorEvaluatorComponent>(entity);
            var selectorEvaluatorComponent = selectorEvaluatorComponents[m_ComponentIndex];

            var taskSaveData = saveData as object[];
            if (taskSaveData != null && taskSaveData.Length == 2) {
                selectorEvaluatorComponent.ActiveChildIndex = (ushort)taskSaveData[0];
                selectorEvaluatorComponent.ReevaluateChildIndex = (ushort)taskSaveData[1];
            } else {
                selectorEvaluatorComponent.ActiveChildIndex = (ushort)saveData;
                selectorEvaluatorComponent.ReevaluateChildIndex = ushort.MaxValue;
            }
            selectorEvaluatorComponents[m_ComponentIndex] = selectorEvaluatorComponent;
        }

        /// <summary>
        /// Creates a deep clone of the component.
        /// </summary>
        /// <returns>A deep clone of the component.</returns>
        public object Clone()
        {
            var clone = Activator.CreateInstance<SelectorEvaluator>();
            clone.Index = Index;
            clone.ParentIndex = ParentIndex;
            clone.SiblingIndex = SiblingIndex;
            clone.Enabled = Enabled;
            return clone;
        }

        /// <summary>
        /// Returns true if the runtime index is within a child subtree that is only being reevaluated.
        /// </summary>
        /// <param name="runtimeIndex">The runtime index to test.</param>
        /// <param name="taskComponents">The runtime task components.</param>
        /// <param name="selectorEvaluatorComponents">The selector evaluator components.</param>
        /// <returns>True if the runtime index is within a reevaluation subtree.</returns>
        public static bool IsRuntimeIndexReevaluating(ushort runtimeIndex, DynamicBuffer<TaskComponent> taskComponents, DynamicBuffer<SelectorEvaluatorComponent> selectorEvaluatorComponents)
        {
            for (int i = 0; i < selectorEvaluatorComponents.Length; ++i) {
                var selectorEvaluatorComponent = selectorEvaluatorComponents[i];
                if (selectorEvaluatorComponent.ReevaluateChildIndex == ushort.MaxValue) {
                    continue;
                }

                if (IsRuntimeIndexWithinTaskRange(runtimeIndex, selectorEvaluatorComponent.ActiveChildIndex, taskComponents)) {
                    continue;
                }

                if (IsRuntimeIndexWithinTaskRange(runtimeIndex, selectorEvaluatorComponent.ReevaluateChildIndex, taskComponents)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if the runtime index is within the specified task's subtree.
        /// </summary>
        /// <param name="runtimeIndex">The runtime index to test.</param>
        /// <param name="taskRootIndex">The root of the subtree.</param>
        /// <param name="taskComponents">The runtime task components.</param>
        /// <returns>True if the runtime index is within the task range.</returns>
        private static bool IsRuntimeIndexWithinTaskRange(ushort runtimeIndex, ushort taskRootIndex, DynamicBuffer<TaskComponent> taskComponents)
        {
            if (taskRootIndex == ushort.MaxValue || taskRootIndex >= taskComponents.Length) {
                return false;
            }

            var taskComponent = taskComponents[taskRootIndex];
            return runtimeIndex >= taskRootIndex && runtimeIndex <= taskComponent.ChildUpperIndex;
        }
    }

    /// <summary>
    /// The DOTS data structure for the SelectorEvaluator class.
    /// </summary>
    public struct SelectorEvaluatorComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
        [Tooltip("The index of the child that is currently active.")]
        public ushort ActiveChildIndex;
        [Tooltip("The higher-priority child that is currently being reevaluated.")]
        public ushort ReevaluateChildIndex;
    }

    /// <summary>
    /// A DOTS tag indicating when a SelectorEvaluator node is active.
    /// </summary>
    public struct SelectorEvaluatorFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the SelectorEvaluator logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct SelectorEvaluatorTaskSystem : ISystem
    {
        private EntityQuery m_Query;

        /// <summary>
        /// Builds the query.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnCreate(ref SystemState state)
        {
            m_Query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<SelectorEvaluatorComponent>().WithAll<SelectorEvaluatorFlag, EvaluateFlag>().Build();
        }

        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            state.Dependency = new SelectorEvaluatorJob()
            {
                EntityCommandBuffer = ecb.AsParallelWriter()
            }.ScheduleParallel(m_Query, state.Dependency);
        }

        /// <summary>
        /// Job which executes the task logic.
        /// </summary>
        [BurstCompile]
        private partial struct SelectorEvaluatorJob : IJobEntity
        {
            [Tooltip("CommandBuffer which sets the component data.")]
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            /// <summary>
            /// Executes the selector evaluator logic.
            /// </summary>
            /// <param name="entity">The entity that is being acted upon.</param>
            /// <param name="entityIndex">The index of the entity.</param>
            /// <param name="branchComponents">An array of BranchComponents.</param>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="selectorEvaluatorComponents">An array of SelectorEvaluatorComponents.</param>
            [BurstCompile]
            public void Execute(Entity entity, [EntityIndexInQuery] int entityIndex, ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<SelectorEvaluatorComponent> selectorEvaluatorComponents)
            {
                for (int i = 0; i < selectorEvaluatorComponents.Length; ++i) {
                    var selectorEvaluatorComponent = selectorEvaluatorComponents[i];
                    var taskComponent = taskComponents[selectorEvaluatorComponent.Index];
                    var taskStatus = taskComponent.Status;

                    // Skip inactive tasks before any branch lookups.
                    if (taskStatus != TaskStatus.Queued && taskStatus != TaskStatus.Running) {
                        continue;
                    }

                    var branchComponent = branchComponents[taskComponent.BranchIndex];
                    // Do not continue if there will be an interrupt or the branch cannot execute.
                    if (branchComponent.InterruptType != InterruptType.None || !branchComponent.CanExecute) {
                        continue;
                    }

                    if (taskStatus == TaskStatus.Queued) {
                        StartSelector(ref selectorEvaluatorComponent, ref taskComponent, ref branchComponent, ref taskComponents, ref branchComponents);
                        selectorEvaluatorComponents[i] = selectorEvaluatorComponent;
                        taskComponents[taskComponent.Index] = taskComponent;
                        branchComponents[taskComponent.BranchIndex] = branchComponent;
                        continue;
                    }

                    var interrupted = false;
                    var ended = false;
                    var reevaluationProcessed = false;
                    if (selectorEvaluatorComponent.ReevaluateChildIndex != ushort.MaxValue) {
                        ended = UpdateReevaluation(ref selectorEvaluatorComponent, ref taskComponent, ref branchComponent, ref taskComponents, ref branchComponents, ref interrupted);
                        reevaluationProcessed = true;
                    }

                    if (!ended && !reevaluationProcessed) {
                        UpdateActiveChild(ref selectorEvaluatorComponent, ref taskComponent, ref branchComponent, ref taskComponents, ref branchComponents, ref interrupted);
                    }

                    if (interrupted) {
                        EntityCommandBuffer.SetComponentEnabled<InterruptedFlag>(entityIndex, entity, true);
                    }

                    selectorEvaluatorComponents[i] = selectorEvaluatorComponent;
                    taskComponents[taskComponent.Index] = taskComponent;
                    branchComponents[taskComponent.BranchIndex] = branchComponent;
                }
            }

            /// <summary>
            /// Starts the selector evaluator.
            /// </summary>
            /// <param name="selectorEvaluatorComponent">The selector evaluator component.</param>
            /// <param name="taskComponent">The task component.</param>
            /// <param name="branchComponent">The branch component.</param>
            /// <param name="taskComponents">All task components.</param>
            /// <param name="branchComponents">All branch components.</param>
            private static void StartSelector(ref SelectorEvaluatorComponent selectorEvaluatorComponent, ref TaskComponent taskComponent, ref BranchComponent branchComponent, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<BranchComponent> branchComponents)
            {
                taskComponent.Status = TaskStatus.Running;
                selectorEvaluatorComponent.ActiveChildIndex = (ushort)(taskComponent.Index + 1);
                selectorEvaluatorComponent.ReevaluateChildIndex = ushort.MaxValue;

                if (selectorEvaluatorComponent.ActiveChildIndex >= taskComponents.Length || taskComponents[selectorEvaluatorComponent.ActiveChildIndex].ParentIndex != taskComponent.Index) {
                    taskComponent.Status = TaskStatus.Failure;
                    selectorEvaluatorComponent.ActiveChildIndex = ushort.MaxValue;
                    branchComponent.NextIndex = taskComponent.ParentIndex;
                    return;
                }

                QueueChild(selectorEvaluatorComponent.ActiveChildIndex, ref taskComponents, ref branchComponents);
            }

            /// <summary>
            /// Updates the higher-priority child that is currently being reevaluated.
            /// </summary>
            /// <param name="selectorEvaluatorComponent">The selector evaluator component.</param>
            /// <param name="taskComponent">The task component.</param>
            /// <param name="branchComponent">The branch component.</param>
            /// <param name="taskComponents">All task components.</param>
            /// <param name="branchComponents">All branch components.</param>
            /// <param name="interrupted">Was a running child interrupted?</param>
            /// <returns>True if the selector evaluator has ended.</returns>
            private static bool UpdateReevaluation(ref SelectorEvaluatorComponent selectorEvaluatorComponent, ref TaskComponent taskComponent, ref BranchComponent branchComponent, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<BranchComponent> branchComponents, ref bool interrupted)
            {
                var reevaluateTaskComponent = taskComponents[selectorEvaluatorComponent.ReevaluateChildIndex];
                if (reevaluateTaskComponent.Status == TaskStatus.Queued) {
                    return false;
                }

                if (reevaluateTaskComponent.Status == TaskStatus.Running) {
                    var previousActiveChildIndex = selectorEvaluatorComponent.ActiveChildIndex;
                    selectorEvaluatorComponent.ActiveChildIndex = selectorEvaluatorComponent.ReevaluateChildIndex;
                    selectorEvaluatorComponent.ReevaluateChildIndex = ushort.MaxValue;
                    if (previousActiveChildIndex != selectorEvaluatorComponent.ActiveChildIndex) {
                        interrupted |= AbortChildSubtree(previousActiveChildIndex, TaskStatus.Failure, ref taskComponents, ref branchComponents);
                    }
                    return false;
                }

                if (reevaluateTaskComponent.Status == TaskStatus.Success) {
                    CompleteChildBranch(reevaluateTaskComponent.BranchIndex, ref branchComponents);
                    CompleteSelector(TaskStatus.Success, ref selectorEvaluatorComponent, ref taskComponent, ref branchComponent, ref taskComponents, ref branchComponents, ref interrupted);
                    return true;
                }

                CompleteChildBranch(reevaluateTaskComponent.BranchIndex, ref branchComponents);
                var nextReevaluateChildIndex = reevaluateTaskComponent.SiblingIndex;
                if (nextReevaluateChildIndex != ushort.MaxValue && nextReevaluateChildIndex != selectorEvaluatorComponent.ActiveChildIndex) {
                    selectorEvaluatorComponent.ReevaluateChildIndex = nextReevaluateChildIndex;
                    QueueChild(nextReevaluateChildIndex, ref taskComponents, ref branchComponents);
                } else {
                    selectorEvaluatorComponent.ReevaluateChildIndex = ushort.MaxValue;
                }

                return false;
            }

            /// <summary>
            /// Updates the active child.
            /// </summary>
            /// <param name="selectorEvaluatorComponent">The selector evaluator component.</param>
            /// <param name="taskComponent">The task component.</param>
            /// <param name="branchComponent">The branch component.</param>
            /// <param name="taskComponents">All task components.</param>
            /// <param name="branchComponents">All branch components.</param>
            /// <param name="interrupted">Was a running child interrupted?</param>
            private static void UpdateActiveChild(ref SelectorEvaluatorComponent selectorEvaluatorComponent, ref TaskComponent taskComponent, ref BranchComponent branchComponent, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<BranchComponent> branchComponents, ref bool interrupted)
            {
                if (selectorEvaluatorComponent.ActiveChildIndex == ushort.MaxValue || selectorEvaluatorComponent.ActiveChildIndex >= taskComponents.Length) {
                    CompleteSelector(TaskStatus.Failure, ref selectorEvaluatorComponent, ref taskComponent, ref branchComponent, ref taskComponents, ref branchComponents, ref interrupted);
                    return;
                }

                var activeTaskComponent = taskComponents[selectorEvaluatorComponent.ActiveChildIndex];
                if (activeTaskComponent.Status == TaskStatus.Success) {
                    CompleteChildBranch(activeTaskComponent.BranchIndex, ref branchComponents);
                    CompleteSelector(TaskStatus.Success, ref selectorEvaluatorComponent, ref taskComponent, ref branchComponent, ref taskComponents, ref branchComponents, ref interrupted);
                    return;
                }

                if (activeTaskComponent.Status == TaskStatus.Failure || activeTaskComponent.Status == TaskStatus.Inactive) {
                    CompleteChildBranch(activeTaskComponent.BranchIndex, ref branchComponents);
                    var nextChildIndex = activeTaskComponent.SiblingIndex;
                    if (nextChildIndex == ushort.MaxValue) {
                        CompleteSelector(TaskStatus.Failure, ref selectorEvaluatorComponent, ref taskComponent, ref branchComponent, ref taskComponents, ref branchComponents, ref interrupted);
                    } else {
                        selectorEvaluatorComponent.ActiveChildIndex = nextChildIndex;
                        selectorEvaluatorComponent.ReevaluateChildIndex = ushort.MaxValue;
                        QueueChild(nextChildIndex, ref taskComponents, ref branchComponents);
                    }
                    return;
                }

                if (activeTaskComponent.Status != TaskStatus.Running) {
                    return;
                }

                var firstChildIndex = (ushort)(taskComponent.Index + 1);
                if (firstChildIndex != selectorEvaluatorComponent.ActiveChildIndex && firstChildIndex < taskComponents.Length && taskComponents[firstChildIndex].ParentIndex == taskComponent.Index) {
                    selectorEvaluatorComponent.ReevaluateChildIndex = firstChildIndex;
                    QueueChild(firstChildIndex, ref taskComponents, ref branchComponents);
                }
            }

            /// <summary>
            /// Queues a child task on its parallel branch.
            /// </summary>
            /// <param name="childIndex">The child index to queue.</param>
            /// <param name="taskComponents">All task components.</param>
            /// <param name="branchComponents">All branch components.</param>
            private static void QueueChild(ushort childIndex, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<BranchComponent> branchComponents)
            {
                if (childIndex == ushort.MaxValue || childIndex >= taskComponents.Length) {
                    return;
                }

                var childTaskComponent = taskComponents[childIndex];
                childTaskComponent.Status = TaskStatus.Queued;
                taskComponents[childIndex] = childTaskComponent;

                var childBranchComponent = branchComponents[childTaskComponent.BranchIndex];
                childBranchComponent.NextIndex = childIndex;
                branchComponents[childTaskComponent.BranchIndex] = childBranchComponent;
            }

            /// <summary>
            /// Marks the child branch as complete so it does not traverse back into the parallel parent.
            /// </summary>
            /// <param name="branchIndex">The branch index that should be completed.</param>
            /// <param name="branchComponents">All branch components.</param>
            private static void CompleteChildBranch(ushort branchIndex, ref DynamicBuffer<BranchComponent> branchComponents)
            {
                var childBranchComponent = branchComponents[branchIndex];
                if (childBranchComponent.NextIndex != ushort.MaxValue) {
                    childBranchComponent.NextIndex = ushort.MaxValue;
                    branchComponents[branchIndex] = childBranchComponent;
                }
            }

            /// <summary>
            /// Completes the selector evaluator.
            /// </summary>
            /// <param name="status">The completion status.</param>
            /// <param name="selectorEvaluatorComponent">The selector evaluator component.</param>
            /// <param name="taskComponent">The task component.</param>
            /// <param name="branchComponent">The branch component.</param>
            /// <param name="taskComponents">All task components.</param>
            /// <param name="branchComponents">All branch components.</param>
            /// <param name="interrupted">Was a running child interrupted?</param>
            private static void CompleteSelector(TaskStatus status, ref SelectorEvaluatorComponent selectorEvaluatorComponent, ref TaskComponent taskComponent, ref BranchComponent branchComponent, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<BranchComponent> branchComponents, ref bool interrupted)
            {
                taskComponent.Status = status;
                branchComponent.NextIndex = taskComponent.ParentIndex;

                var maxChildIndex = taskComponent.ChildUpperIndex > taskComponent.Index
                    ? taskComponent.ChildUpperIndex
                    : taskComponent.Index;
                interrupted |= AbortTaskRange((ushort)(taskComponent.Index + 1), maxChildIndex, TaskStatus.Failure, ref taskComponents, ref branchComponents);

                selectorEvaluatorComponent.ActiveChildIndex = ushort.MaxValue;
                selectorEvaluatorComponent.ReevaluateChildIndex = ushort.MaxValue;
            }

            /// <summary>
            /// Aborts any running or queued tasks within the specified child subtree.
            /// </summary>
            /// <param name="childIndex">The subtree root.</param>
            /// <param name="status">The status that should be assigned to the aborted tasks.</param>
            /// <param name="taskComponents">All task components.</param>
            /// <param name="branchComponents">All branch components.</param>
            /// <returns>True if a task was aborted.</returns>
            private static bool AbortChildSubtree(ushort childIndex, TaskStatus status, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<BranchComponent> branchComponents)
            {
                if (childIndex == ushort.MaxValue || childIndex >= taskComponents.Length) {
                    return false;
                }

                var childTaskComponent = taskComponents[childIndex];
                var maxChildIndex = childTaskComponent.ChildUpperIndex > childTaskComponent.Index
                    ? childTaskComponent.ChildUpperIndex
                    : childTaskComponent.Index;
                return AbortTaskRange(childIndex, maxChildIndex, status, ref taskComponents, ref branchComponents);
            }

            /// <summary>
            /// Aborts any running or queued tasks within the specified index range.
            /// </summary>
            /// <param name="startIndex">The first index to abort.</param>
            /// <param name="endIndex">The last index to abort.</param>
            /// <param name="status">The status that should be assigned to the aborted tasks.</param>
            /// <param name="taskComponents">All task components.</param>
            /// <param name="branchComponents">All branch components.</param>
            /// <returns>True if a task was aborted.</returns>
            private static bool AbortTaskRange(ushort startIndex, ushort endIndex, TaskStatus status, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<BranchComponent> branchComponents)
            {
                var interrupted = false;
                for (ushort i = startIndex; i <= endIndex; ++i) {
                    var taskComponent = taskComponents[i];
                    if (taskComponent.Status == TaskStatus.Queued || taskComponent.Status == TaskStatus.Running) {
                        taskComponent.Status = status;
                        taskComponents[i] = taskComponent;

                        var branchComponent = branchComponents[taskComponent.BranchIndex];
                        if (branchComponent.NextIndex != ushort.MaxValue) {
                            branchComponent.NextIndex = ushort.MaxValue;
                            branchComponents[taskComponent.BranchIndex] = branchComponent;
                        }
                        interrupted = true;
                    }
                }

                return interrupted;
            }
        }
    }
}
#endif