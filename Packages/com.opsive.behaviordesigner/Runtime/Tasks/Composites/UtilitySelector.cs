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
    using Opsive.GraphDesigner.Runtime.Variables.ECS;
    using Opsive.Shared.Utility;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Burst;
    using UnityEngine;
    using System;

    /// <summary>
    /// A node representation of the utility selector task.
    /// </summary>
    [NodeIcon("9d36cd363c3e08246a6e9eaf5ad99d69", "db3d0b77c7f9e0b4f9157aa03178836a")]
    [Opsive.Shared.Utility.Description("The utility selector task evaluates the child tasks using Utility Theory AI. The child task can return the utility value " +
                     "at that particular time. The task with the highest utility value will be selected and the existing running task will be aborted. The utility selector " +
                     "task reevaluates its children every tick.")]
    public class UtilitySelector : ECSCompositeTask<UtilitySelectorTaskSystem, UtilitySelectorComponent, UtilitySelectorFlag>, IParentNode, ISavableTask, ICloneable
    {
        private ushort m_ComponentIndex;

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override UtilitySelectorComponent GetBufferElement()
        {
            return new UtilitySelectorComponent()
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
            var utilitySelectorComponents = world.EntityManager.GetBuffer<UtilitySelectorComponent>(entity);
            var utilitySelectorComponent = utilitySelectorComponents[m_ComponentIndex];

            // Save the active child.
            return utilitySelectorComponent.ActiveChildIndex;
        }

        /// <summary>
        /// Loads the previous task state.
        /// </summary>
        /// <param name="saveData">The previous task state.</param>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        public void Load(object saveData, World world, Entity entity)
        {
            var utilitySelectorComponents = world.EntityManager.GetBuffer<UtilitySelectorComponent>(entity);
            var utilitySelectorComponent = utilitySelectorComponents[m_ComponentIndex];

            // saveData is the active child index.
            utilitySelectorComponent.ActiveChildIndex = (ushort)saveData;
            utilitySelectorComponents[m_ComponentIndex] = utilitySelectorComponent;
        }

        /// <summary>
        /// Creates a deep clone of the component.
        /// </summary>
        /// <returns>A deep clone of the component.</returns>
        public object Clone()
        {
            var clone = Activator.CreateInstance<UtilitySelector>();
            clone.Index = Index;
            clone.ParentIndex = ParentIndex;
            clone.SiblingIndex = SiblingIndex;
            clone.Enabled = Enabled;
            return clone;
        }
    }

    /// <summary>
    /// Immutable blob entry mapping a task index to its UtilityValueComponent index.
    /// </summary>
    public struct UtilityItemBlobEntry
    {
        [Tooltip("The index of the task.")]
        public ushort TaskIndex;
        [Tooltip("The index of the UtilityValueComponent.")]
        public ushort UtilityValueIndex;
    }

    /// <summary>
    /// Blob asset storing the utility item entries (task index and utility value index per child). Mutable state (CanExecute) is stored in the component mask.
    /// </summary>
    public struct UtilityItemsBlob
    {
        [Tooltip("The utility item entries.")]
        public BlobArray<UtilityItemBlobEntry> Items;
    }

    /// <summary>
    /// The DOTS data structure for the UtilitySelector class.
    /// </summary>
    public struct UtilitySelectorComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
        [Tooltip("The index of the child that is currently active.")]
        public ushort ActiveChildIndex;
        [Tooltip("The blob containing task-to-utility-value mapping per child.")]
        public BlobAssetReference<UtilityItemsBlob> UtilityItems;
        [Tooltip("Bit mask of which children can execute (bit i = child i). Max 64 children.")]
        public ulong CanExecuteMask;
    }

    /// <summary>
    /// DOTS structure that contains the most recently utility of the task.
    /// </summary>
    public struct UtilityValueComponent : IBufferElementData
    {
        [Tooltip("The index of the task.")]
        public ushort Index;
        [Tooltip("The current utility value. The higher the value the more likely it will be selected.")]
        public float Value;
    }

    /// <summary>
    /// A DOTS tag indicating when a UtilitySelector node is active.
    /// </summary>
    public struct UtilitySelectorFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the UtilitySelector logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct UtilitySelectorTaskSystem : ISystem
    {
        /// <summary>
        /// Updates the logic.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            var hasUtilityValueComponent = false;
            foreach (var (utilitySelectorComponents, utilityValueComponents, taskComponents, branchComponents, entity) in
                SystemAPI.Query<DynamicBuffer<UtilitySelectorComponent>, DynamicBuffer<UtilityValueComponent>, DynamicBuffer<TaskComponent>, DynamicBuffer<BranchComponent>>().WithAll<EvaluateFlag>().WithEntityAccess()) {

                hasUtilityValueComponent = true;
                for (int i = 0; i < utilitySelectorComponents.Length; ++i) {
                    var utilitySelectorComponent = utilitySelectorComponents[i];
                    var taskComponent = taskComponents[utilitySelectorComponent.Index];
                    var branchComponent = branchComponents[taskComponent.BranchIndex];

                    // Do not continue if there will be an interrupt or the branch cannot execute.
                    if (branchComponent.InterruptType != InterruptType.None || !branchComponent.CanExecute) {
                        continue;
                    }

                    var utilitySelectorComponentsBuffer = utilitySelectorComponents;
                    var utilityValueComponentBuffer = utilityValueComponents;
                    var taskComponentsBuffer = taskComponents;
                    var branchComponentBuffer = branchComponents;
                    if (taskComponent.Status == TaskStatus.Queued) {
                        // Initialize the utility items blob and can-execute mask when first run.
                        ushort childIndex;
                        if (!utilitySelectorComponent.UtilityItems.IsCreated) {
                            var childCount = TraversalUtility.GetImmediateChildCount(ref taskComponent, ref taskComponentsBuffer);
                            var builder = new BlobBuilder(Allocator.Temp);
                            ref var root = ref builder.ConstructRoot<UtilityItemsBlob>();
                            var itemsArray = builder.Allocate(ref root.Items, childCount);
                            childIndex = (ushort)(taskComponent.Index + 1);
                            for (ushort j = 0; j < childCount; ++j) {
                                itemsArray[j] = new UtilityItemBlobEntry() { TaskIndex = childIndex, UtilityValueIndex = ushort.MaxValue };
                                for (ushort k = 0; k < utilityValueComponents.Length; ++k) {
                                    if (utilityValueComponents[k].Index == childIndex) {
                                        itemsArray[j].UtilityValueIndex = k;
                                        break;
                                    }
                                }
                                childIndex = taskComponents[childIndex].SiblingIndex;
                            }
                            utilitySelectorComponent.UtilityItems = builder.CreateBlobAssetReference<UtilityItemsBlob>(Allocator.Persistent);
                            builder.Dispose();
                        }

                        // Do not include child nodes that are disabled.
                        ref var items = ref utilitySelectorComponent.UtilityItems.Value.Items;
                        childIndex = (ushort)(taskComponent.Index + 1);
                        ulong mask = 0;
                        for (int j = 0; j < items.Length; ++j) {
                            if (taskComponents[childIndex].Enabled) {
                                mask |= 1UL << j;
                            }
                            childIndex = taskComponents[childIndex].SiblingIndex;
                        }
                        utilitySelectorComponent.CanExecuteMask = mask;

                        utilitySelectorComponent.ActiveChildIndex = GetHighestUtility(utilitySelectorComponent.UtilityItems, utilitySelectorComponent.CanExecuteMask, ref utilityValueComponentBuffer);
                        utilitySelectorComponentsBuffer[i] = utilitySelectorComponent;

                        if (utilitySelectorComponent.ActiveChildIndex == ushort.MaxValue) {
                            taskComponent.Status = TaskStatus.Failure;
                            branchComponent.NextIndex = taskComponent.ParentIndex;
                        } else {
                            taskComponent.Status = TaskStatus.Running;
                            items = ref utilitySelectorComponent.UtilityItems.Value.Items;
                            branchComponent.NextIndex = items[utilitySelectorComponent.ActiveChildIndex].TaskIndex;

                            // Start the child.
                            var nextChildTaskComponent = taskComponents[branchComponent.NextIndex];
                            nextChildTaskComponent.Status = TaskStatus.Queued;
                            taskComponentsBuffer[branchComponent.NextIndex] = nextChildTaskComponent;
                        }
                        taskComponentsBuffer[taskComponent.Index] = taskComponent;
                        branchComponentBuffer[taskComponent.BranchIndex] = branchComponent;

                        continue;
                    } else if (taskComponent.Status != TaskStatus.Running) {
                        continue;
                    }

                    ref var itemsRef = ref utilitySelectorComponent.UtilityItems.Value.Items;
                    var childTaskComponent = taskComponents[itemsRef[utilitySelectorComponent.ActiveChildIndex].TaskIndex];
                    if (childTaskComponent.Status == TaskStatus.Success) {
                        // The child has returned success. Stop the selector.
                        taskComponent.Status = childTaskComponent.Status;
                        taskComponentsBuffer[utilitySelectorComponent.Index] = taskComponent;
                        branchComponent.NextIndex = taskComponent.ParentIndex;
                        branchComponentBuffer[taskComponent.BranchIndex] = branchComponent;
                        continue;
                    } else if (childTaskComponent.Status == TaskStatus.Failure) {
                        utilitySelectorComponent.CanExecuteMask &= ~(1UL << utilitySelectorComponent.ActiveChildIndex);
                        utilitySelectorComponentsBuffer[i] = utilitySelectorComponent;
                    }

                    // The active task returned failure or is currently running. Determine if the active task needs to change.
                    var highestIndex = GetHighestUtility(utilitySelectorComponent.UtilityItems, utilitySelectorComponent.CanExecuteMask, ref utilityValueComponentBuffer);
                    if (highestIndex == utilitySelectorComponent.ActiveChildIndex) {
                        // No changes are necessary.
                        continue;
                    }

                    var activeTaskIndex = itemsRef[utilitySelectorComponent.ActiveChildIndex].TaskIndex;
                    utilitySelectorComponent.ActiveChildIndex = highestIndex;
                    utilitySelectorComponentsBuffer[i] = utilitySelectorComponent;

                    // A new branch has been selected. Trigger an interrupt on the currently active branch.
                    if (taskComponents[activeTaskIndex].Status == TaskStatus.Running){
                        branchComponent.InterruptType = InterruptType.Branch;
                        branchComponent.InterruptIndex = itemsRef[highestIndex].TaskIndex;
                        state.EntityManager.SetComponentEnabled<InterruptFlag>(entity, true);
                        branchComponentBuffer[taskComponent.BranchIndex] = branchComponent;
                        continue;
                    }

                    // No more tasks may need to run.
                    if (highestIndex == ushort.MaxValue) {
                        taskComponent.Status = TaskStatus.Failure;
                        taskComponentsBuffer[utilitySelectorComponent.Index] = taskComponent;

                        branchComponent.NextIndex = taskComponent.ParentIndex;
                    } else {
                        // Run the task with the next highest utility value.
                        var nextTaskIndex = itemsRef[utilitySelectorComponent.ActiveChildIndex].TaskIndex;
                        var nextTaskComponent = taskComponents[nextTaskIndex];
                        nextTaskComponent.Status = TaskStatus.Queued;
                        taskComponentsBuffer[nextTaskIndex] = nextTaskComponent;

                        branchComponent.NextIndex = nextTaskIndex;
                    }
                    branchComponentBuffer[taskComponent.BranchIndex] = branchComponent;
                }
            }

            // Special case where the UtilitySelectorComponent has no UtilityValueComponent children.
            if (!hasUtilityValueComponent) {
                foreach (var (utilitySelectorComponents, taskComponents, branchComponents) in
                    SystemAPI.Query<DynamicBuffer<UtilitySelectorComponent>, DynamicBuffer<TaskComponent>, DynamicBuffer<BranchComponent>>().WithAll<UtilitySelectorFlag, EvaluateFlag>()) {

                    for (int i = 0; i < utilitySelectorComponents.Length; ++i) {
                        var utilitySelectorComponent = utilitySelectorComponents[i];
                        var taskComponent = taskComponents[utilitySelectorComponent.Index];

                        // If there are no values then the selector should return failure.
                        if (taskComponent.Status == TaskStatus.Queued && !utilitySelectorComponent.UtilityItems.IsCreated) {
                            taskComponent.Status = TaskStatus.Failure;
                            var taskComponentsBuffer = taskComponents;
                            taskComponentsBuffer[utilitySelectorComponent.Index] = taskComponent;

                            var branchComponent = branchComponents[taskComponent.BranchIndex];
                            branchComponent.NextIndex = taskComponent.ParentIndex;
                            var branchComponentBuffer = branchComponents;
                            branchComponentBuffer[taskComponent.BranchIndex] = branchComponent;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the task index with the highest utility value.
        /// </summary>
        /// <param name="utilityItems">The blob containing utility item data.</param>
        /// <param name="canExecuteMask">Bit mask of which children can execute.</param>
        /// <param name="utilityValueComponents">The utility values of the tasks.</param>
        /// <returns>The index with the highest utility value, or ushort.MaxValue if none.</returns>
        [BurstCompile]
        private ushort GetHighestUtility(BlobAssetReference<UtilityItemsBlob> utilityItems, ulong canExecuteMask, ref DynamicBuffer<UtilityValueComponent> utilityValueComponents)
        {
            var utilityIndex = ushort.MaxValue;
            var highestUtility = float.MinValue;
            ref var items = ref utilityItems.Value.Items;
            for (ushort i = 0; i < items.Length; ++i) {
                if ((canExecuteMask & (1UL << i)) == 0 || items[i].UtilityValueIndex == ushort.MaxValue) {
                    continue;
                }

                var utilityValue = utilityValueComponents[items[i].UtilityValueIndex].Value;
                if (utilityValue > highestUtility) {
                    highestUtility = utilityValue;
                    utilityIndex = i;
                }
            }

            return utilityIndex;
        }

        /// <summary>
        /// Disposes blob assets when the system is destroyed.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnDestroy(ref SystemState state)
        {
            foreach (var utilitySelectorComponents in SystemAPI.Query<DynamicBuffer<UtilitySelectorComponent>>()) {
                for (int i = 0; i < utilitySelectorComponents.Length; ++i) {
                    var utilitySelectorComponent = utilitySelectorComponents[i];
                    if (utilitySelectorComponent.UtilityItems.IsCreated) {
                        utilitySelectorComponent.UtilityItems.Dispose();
                    }
                }
            }
        }
    }
}
#endif