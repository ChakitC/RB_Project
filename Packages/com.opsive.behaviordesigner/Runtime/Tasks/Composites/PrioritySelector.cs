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
    using Opsive.GraphDesigner.Runtime.Variables.ECS;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.Shared.Utility;
    using System;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// A node representation of the priority selector task.
    /// </summary>
    [NodeIcon("cea0f2b6cee06a742bb35dcc40202e8e", "744afc2640950e045961296f1d5800d7")]
    [Opsive.Shared.Utility.Description("Similar to the selector task, the priority selector task will return success as soon as a child task returns success. " +
                     "Instead of running the tasks sequentially from left to right within the tree, the priority selector will ask the task what its priority is to determine the order. " +
                     "The higher priority tasks have a higher chance at being run first.")]
    public class PrioritySelector : ECSCompositeTask<PrioritySelectorTaskSystem, PrioritySelectorComponent, PrioritySelectorFlag>, IParentNode, ISavableTask, ICloneable
    {
        private ushort m_ComponentIndex;

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override PrioritySelectorComponent GetBufferElement()
        {
            return new PrioritySelectorComponent()
            {
                Index = RuntimeIndex,
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
            var prioritySelectorComponents = world.EntityManager.GetBuffer<PrioritySelectorComponent>(entity);
            var prioritySelectorComponent = prioritySelectorComponents[m_ComponentIndex];

            // Save the active child index.
            return prioritySelectorComponent.ActiveRelativeChildIndex;
        }

        /// <summary>
        /// Loads the previous task state.
        /// </summary>
        /// <param name="saveData">The previous task state.</param>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        public void Load(object saveData, World world, Entity entity)
        {
            var prioritySelectorComponents = world.EntityManager.GetBuffer<PrioritySelectorComponent>(entity);
            var prioritySelectorComponent = prioritySelectorComponents[m_ComponentIndex];
            prioritySelectorComponent.ActiveRelativeChildIndex = (ushort)saveData;
            prioritySelectorComponents[m_ComponentIndex] = prioritySelectorComponent;
        }

        /// <summary>
        /// Creates a deep clone of the component.
        /// </summary>
        /// <returns>A deep clone of the component.</returns>
        public object Clone()
        {
            var clone = Activator.CreateInstance<PrioritySelector>();
            clone.Index = Index;
            clone.ParentIndex = ParentIndex;
            clone.SiblingIndex = SiblingIndex;
            clone.Enabled = Enabled;
            return clone;
        }
    }

    /// <summary>
    /// Immutable blob entry mapping a task index to its PriorityValueComponent index.
    /// </summary>
    public struct PriorityItemBlobEntry
    {
        [Tooltip("The index of the task.")]
        public ushort TaskIndex;
        [Tooltip("The index of the PriorityValueComponent. ushort.MaxValue indicates no corresponding component.")]
        public ushort PriorityValueIndex;
    }

    /// <summary>
    /// Blob asset storing the priority item entries (task index and priority value index per child).
    /// </summary>
    public struct PriorityItemsBlob
    {
        [Tooltip("The priority item entries.")]
        public BlobArray<PriorityItemBlobEntry> Items;
    }

    /// <summary>
    /// The DOTS data structure for the PrioritySelector class.
    /// </summary>
    public struct PrioritySelectorComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
        [Tooltip("The relative index of the child that is currently active.")]
        public ushort ActiveRelativeChildIndex;
        [Tooltip("The blob containing task-to-priority-value mapping per child.")]
        public BlobAssetReference<PriorityItemsBlob> PriorityItems;
        [Tooltip("Task indices in sorted order by priority (highest first).")]
        public UnsafeList<ushort> SortedOrder;
    }

    /// <summary>
    /// DOTS structure that contains the most recently priority of the task.
    /// </summary>
    public struct PriorityValueComponent : IBufferElementData
    {
        [Tooltip("The index of the task.")]
        public ushort Index;
        [Tooltip("The current priority value. The higher the value the more likely it will be selected.")]
        public float Value;
    }

    /// <summary>
    /// A DOTS tag indicating when a PrioritySelector node is active.
    /// </summary>
    public struct PrioritySelectorFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the PrioritySelector logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct PrioritySelectorTaskSystem : ISystem
    {
        /// <summary>
        /// Creates the jobs.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            var withValuesQuery = SystemAPI.QueryBuilder()
                .WithAllRW<BranchComponent>()
                .WithAllRW<TaskComponent>()
                .WithAllRW<PrioritySelectorComponent>()
                .WithAllRW<PriorityValueComponent>()
                .WithAll<PrioritySelectorFlag, EvaluateFlag>()
                .Build();
            state.Dependency = new PrioritySelectorWithValuesJob().ScheduleParallel(withValuesQuery, state.Dependency);

            // Special case where there is no PriorityValueComponent buffer.
            var withoutValuesQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PrioritySelectorComponent>()
                .WithAllRW<TaskComponent>()
                .WithAllRW<BranchComponent>()
                .WithAll<PrioritySelectorFlag, EvaluateFlag>()
                .WithNone<PriorityValueComponent>()
                .Build();
            state.Dependency = new PrioritySelectorWithoutValuesJob().ScheduleParallel(withoutValuesQuery, state.Dependency);
        }

        /// <summary>
        /// Job which executes the PrioritySelector logic when PriorityValueComponent exists.
        /// </summary>
        [BurstCompile]
        private partial struct PrioritySelectorWithValuesJob : IJobEntity
        {
            /// <summary>
            /// Executes the PrioritySelector logic.
            /// </summary>
            /// <param name="branchComponents">An array of BranchComponents.</param>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="prioritySelectorComponents">An array of PrioritySelectorComponents.</param>
            /// <param name="priorityValueComponents">An array of PriorityValueComponents.</param>
            [BurstCompile]
            public void Execute(ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents,
                ref DynamicBuffer<PrioritySelectorComponent> prioritySelectorComponents, ref DynamicBuffer<PriorityValueComponent> priorityValueComponents)
            {
                for (int i = 0; i < prioritySelectorComponents.Length; ++i) {
                    var prioritySelectorComponent = prioritySelectorComponents[i];
                    var taskComponent = taskComponents[prioritySelectorComponent.Index];
                    var taskStatus = taskComponent.Status;

                    // Skip inactive tasks before branch lookups.
                    if (taskStatus != TaskStatus.Queued && taskStatus != TaskStatus.Running) {
                        continue;
                    }

                    var branchComponent = branchComponents[taskComponent.BranchIndex];
                    // Do not continue if there will be an interrupt or the branch cannot execute.
                    if (branchComponent.InterruptType != InterruptType.None || !branchComponent.CanExecute) {
                        continue;
                    }

                    if (taskStatus == TaskStatus.Queued) {
                        taskComponent.Status = TaskStatus.Running;
                        taskComponents[taskComponent.Index] = taskComponent;

                        // Build the priority items blob when first run.
                        if (!prioritySelectorComponent.PriorityItems.IsCreated) {
                            var childCount = TraversalUtility.GetImmediateChildCount(ref taskComponent, ref taskComponents);
                            var builder = new BlobBuilder(Allocator.Temp);
                            ref var root = ref builder.ConstructRoot<PriorityItemsBlob>();
                            var itemsArray = builder.Allocate(ref root.Items, childCount);
                            var childIndex = (ushort)(taskComponent.Index + 1);
                            for (ushort j = 0; j < childCount; ++j) {
                                itemsArray[j] = new PriorityItemBlobEntry() { TaskIndex = childIndex, PriorityValueIndex = ushort.MaxValue };
                                for (ushort k = 0; k < priorityValueComponents.Length; ++k) {
                                    if (priorityValueComponents[k].Index == childIndex) {
                                        itemsArray[j].PriorityValueIndex = k;
                                        break;
                                    }
                                }
                                childIndex = taskComponents[childIndex].SiblingIndex;
                            }
                            prioritySelectorComponent.PriorityItems = builder.CreateBlobAssetReference<PriorityItemsBlob>(Allocator.Persistent);
                            builder.Dispose();
                        }

                        // Build sorted order by current priority values (reuse storage).
                        ref var priorityItems = ref prioritySelectorComponent.PriorityItems.Value.Items;
                        var orderCount = priorityItems.Length;
                        if (!prioritySelectorComponent.SortedOrder.IsCreated || prioritySelectorComponent.SortedOrder.Length != orderCount) {
                            if (prioritySelectorComponent.SortedOrder.IsCreated) {
                                prioritySelectorComponent.SortedOrder.Dispose();
                            }
                            prioritySelectorComponent.SortedOrder = new UnsafeList<ushort>(orderCount, Allocator.Persistent);
                            prioritySelectorComponent.SortedOrder.Resize(orderCount);
                        }

                        for (ushort j = 0; j < orderCount; ++j) {
                            prioritySelectorComponent.SortedOrder[j] = j;
                        }

                        for (int a = 0; a < orderCount; ++a) {
                            for (int b = a + 1; b < orderCount; ++b) {
                                ref var entryA = ref priorityItems[prioritySelectorComponent.SortedOrder[a]];
                                ref var entryB = ref priorityItems[prioritySelectorComponent.SortedOrder[b]];
                                var valueA = GetPriorityValue(ref entryA, ref priorityValueComponents);
                                var valueB = GetPriorityValue(ref entryB, ref priorityValueComponents);
                                if (valueB > valueA) {
                                    var t = prioritySelectorComponent.SortedOrder[a];
                                    prioritySelectorComponent.SortedOrder[a] = prioritySelectorComponent.SortedOrder[b];
                                    prioritySelectorComponent.SortedOrder[b] = t;
                                }
                            }
                        }

                        for (int j = 0; j < orderCount; ++j) {
                            var relIdx = prioritySelectorComponent.SortedOrder[j];
                            prioritySelectorComponent.SortedOrder[j] = priorityItems[relIdx].TaskIndex;
                        }

                        prioritySelectorComponent.ActiveRelativeChildIndex = 0;
                        prioritySelectorComponents[i] = prioritySelectorComponent;

                        branchComponent.NextIndex = prioritySelectorComponent.SortedOrder[prioritySelectorComponent.ActiveRelativeChildIndex];
                        branchComponents[taskComponent.BranchIndex] = branchComponent;

                        // Start the child.
                        var nextChildTaskComponent = taskComponents[branchComponent.NextIndex];
                        nextChildTaskComponent.Status = TaskStatus.Queued;
                        taskComponents[branchComponent.NextIndex] = nextChildTaskComponent;
                    }

                    // The prioritySelector task is currently active. Check the active child.
                    var childTaskComponent = taskComponents[prioritySelectorComponent.SortedOrder[prioritySelectorComponent.ActiveRelativeChildIndex]];
                    if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running) {
                        // The child should keep running.
                        continue;
                    }

                    // Switch to the next highest priority. If no more priority values exist the task should act as a normal selector.
                    if (prioritySelectorComponent.ActiveRelativeChildIndex == prioritySelectorComponent.SortedOrder.Length - 1 ||
                        childTaskComponent.Status == TaskStatus.Success) {
                        // There are no more children or the child succeeded. The selector task should end.
                        taskComponent.Status = childTaskComponent.Status;
                        prioritySelectorComponent.ActiveRelativeChildIndex = 0;
                        taskComponents[prioritySelectorComponent.Index] = taskComponent;

                        branchComponent.NextIndex = taskComponent.ParentIndex;
                        branchComponents[taskComponent.BranchIndex] = branchComponent;
                    } else {
                        // The child task returned failure. Move onto the next task.
                        prioritySelectorComponent.ActiveRelativeChildIndex++;
                        var nextIndex = prioritySelectorComponent.SortedOrder[prioritySelectorComponent.ActiveRelativeChildIndex];
                        var nextTaskComponent = taskComponents[nextIndex];
                        nextTaskComponent.Status = TaskStatus.Queued;
                        taskComponents[nextIndex] = nextTaskComponent;

                        branchComponent.NextIndex = nextIndex;
                        branchComponents[taskComponent.BranchIndex] = branchComponent;
                    }
                    prioritySelectorComponents[i] = prioritySelectorComponent;
                }
            }
        }

        /// <summary>
        /// Job which executes the special case where the PrioritySelector has no PriorityValueComponent buffer.
        /// </summary>
        [BurstCompile]
        private partial struct PrioritySelectorWithoutValuesJob : IJobEntity
        {
            /// <summary>
            /// Executes the no-priority-values fallback logic.
            /// </summary>
            /// <param name="prioritySelectorComponents">An array of PrioritySelectorComponents.</param>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="branchComponents">An array of BranchComponents.</param>
            [BurstCompile]
            public void Execute(ref DynamicBuffer<PrioritySelectorComponent> prioritySelectorComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<BranchComponent> branchComponents)
            {
                for (int i = 0; i < prioritySelectorComponents.Length; ++i) {
                    var prioritySelectorComponent = prioritySelectorComponents[i];
                    var taskComponent = taskComponents[prioritySelectorComponent.Index];

                    // If there are no values then the selector should return failure.
                    if (taskComponent.Status == TaskStatus.Queued && !prioritySelectorComponent.PriorityItems.IsCreated) {
                        taskComponent.Status = TaskStatus.Failure;
                        taskComponents[prioritySelectorComponent.Index] = taskComponent;

                        var branchComponent = branchComponents[taskComponent.BranchIndex];
                        branchComponent.NextIndex = taskComponent.ParentIndex;
                        branchComponents[taskComponent.BranchIndex] = branchComponent;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the priority value for a blob entry, or float.MinValue if no component.
        /// </summary>
        /// <param name="entry">The priority item blob entry.</param>
        /// <param name="priorityValueComponents">The priority value components buffer.</param>
        /// <returns>The priority value.</returns>
        [BurstCompile]
        private static float GetPriorityValue(ref PriorityItemBlobEntry entry, ref DynamicBuffer<PriorityValueComponent> priorityValueComponents)
        {
            if (entry.PriorityValueIndex == ushort.MaxValue) {
                return float.MinValue;
            }
            return priorityValueComponents[entry.PriorityValueIndex].Value;
        }

        /// <summary>
        /// Disposes blob assets when the system is destroyed.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnDestroy(ref SystemState state)
        {
            foreach (var prioritySelectorComponents in SystemAPI.Query<DynamicBuffer<PrioritySelectorComponent>>()) {
                for (int i = 0; i < prioritySelectorComponents.Length; ++i) {
                    var prioritySelectorComponent = prioritySelectorComponents[i];
                    if (prioritySelectorComponent.PriorityItems.IsCreated) {
                        prioritySelectorComponent.PriorityItems.Dispose();
                    }
                    if (prioritySelectorComponent.SortedOrder.IsCreated) {
                        prioritySelectorComponent.SortedOrder.Dispose();
                    }
                }
            }
        }
    }
}
#endif