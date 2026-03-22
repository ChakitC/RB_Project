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
    using Opsive.Shared.Utility;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Burst;
    using UnityEngine;
    using System;

    /// <summary>
    /// A node representation of the random selector task.
    /// </summary>
    [NodeIcon("d7c1e0f5830316e449df8a35561df859", "7638e4bc5a1f4cd488801902387ec5ea")]
    [Opsive.Shared.Utility.Description("Similar to the selector task, the random selector task will return success as soon as a child task returns success.  " +
                     "The difference is that the random selector class will run its children in a random order. The selector task is deterministic " +
                     "in that it will always run the tasks from left to right within the tree. The random selector task shuffles the child tasks up and then begins " +
                     "execution in a random order. Other than that the random selector class is the same as the selector class. It will continue running tasks " +
                     "until a task completes successfully. If no child tasks return success then it will return failure.")]
    public class RandomSelector : ECSCompositeTask<RandomSelectorTaskSystem, RandomSelectorComponent>, IParentNode, IConditionalAbortParent, IInterruptResponder, ISavableTask, ICloneable
    {
        [Tooltip("Specifies how the child conditional tasks should be reevaluated.")]
        [SerializeField] ConditionalAbortType m_AbortType;
        [Tooltip("The seed of the random number generator. Set to 0 to use the entity index as the seed.")]
        [SerializeField] uint m_Seed;

        private ushort m_ComponentIndex;

        public ConditionalAbortType AbortType { get => m_AbortType; set => m_AbortType = value; }
        public uint Seed { get => m_Seed; set => m_Seed = value; }

        public override ComponentType Flag { get => typeof(RandomSelectorFlag); }
        public Type InterruptSystemType { get => typeof(RandomSelectorInterruptSystem); }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override RandomSelectorComponent GetBufferElement()
        {
            return new RandomSelectorComponent()
            {
                Index = RuntimeIndex,
                Seed = m_Seed,
            };
        }

        /// <summary>
        /// Adds the IBufferElementData to the entity.
        /// </summary>
        /// <param name="world">The world that the entity exists in.</param>
        /// <param name="entity">The entity that the IBufferElementData should be assigned to.</param>
        /// <param name="gameObject">The GameObject that the entity is attached to.</param>
        /// <returns>The index of the element within the buffer.</returns>
        public override int AddBufferElement(World world, Entity entity, GameObject gameObject)
        {
            m_ComponentIndex = (ushort)base.AddBufferElement(world, entity, gameObject);
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
            var randomSelectorComponents = world.EntityManager.GetBuffer<RandomSelectorComponent>(entity);
            var randomSelectorComponent = randomSelectorComponents[m_ComponentIndex];

            // Save the active child and array order.
            var saveData = new object[2];
            saveData[0] = randomSelectorComponent.ActiveRelativeChildIndex;
            if (randomSelectorComponent.TaskOrder.IsCreated) {
                var taskOrder = randomSelectorComponent.TaskOrder.Value.Indicies.ToArray();
                saveData[1] = taskOrder;
            }
            return saveData;
        }

        /// <summary>
        /// Loads the previous task state.
        /// </summary>
        /// <param name="saveData">The previous task state.</param>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        public void Load(object saveData, World world, Entity entity)
        {
            var randomSelectorComponents = world.EntityManager.GetBuffer<RandomSelectorComponent>(entity);
            var randomSelectorComponent = randomSelectorComponents[m_ComponentIndex];

            // saveData is the active child and array order.
            var taskSaveData = (object[])saveData;
            randomSelectorComponent.ActiveRelativeChildIndex = (ushort)taskSaveData[0];
            if (taskSaveData[1] != null) {
                var taskOrder = (ushort[])taskSaveData[1];
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<IndiciesBlob>();
                var orderArray = builder.Allocate(ref root.Indicies, taskOrder.Length);
                for (int i = 0; i < taskOrder.Length; i++) {
                    orderArray[i] = taskOrder[i];
                }
                randomSelectorComponent.TaskOrder = builder.CreateBlobAssetReference<IndiciesBlob>(Allocator.Persistent);
                builder.Dispose();
            }
            randomSelectorComponents[m_ComponentIndex] = randomSelectorComponent;
        }

        /// <summary>
        /// Creates a deep clone of the component.
        /// </summary>
        /// <returns>A deep clone of the component.</returns>
        public object Clone()
        {
            var clone = Activator.CreateInstance<RandomSelector>();
            clone.Index = Index;
            clone.ParentIndex = ParentIndex;
            clone.SiblingIndex = SiblingIndex;
            clone.AbortType = AbortType;
            return clone;
        }
    }

    /// <summary>
    /// The DOTS data structure for the RandomSelector class.
    /// </summary>
    public struct RandomSelectorComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
        [Tooltip("The relative index of the child that is currently active.")]
        public ushort ActiveRelativeChildIndex;
        [Tooltip("The seed of the random number generator.")]
        public uint Seed;
        [Tooltip("The random number generator for the task.")]
        public Unity.Mathematics.Random RandomNumberGenerator;
        [Tooltip("The indicies of the child task execution order.")]
        public BlobAssetReference<IndiciesBlob> TaskOrder;
    }

    /// <summary>
    /// A DOTS tag indicating when a RandomSelector node is active.
    /// </summary>
    public struct RandomSelectorFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the RandomSelector logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct RandomSelectorTaskSystem : ISystem
    {
        /// <summary>
        /// Updates the logic.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            foreach (var (branchComponents, taskComponents, randomSelectorComponents, entity) in
                SystemAPI.Query<DynamicBuffer<BranchComponent>, DynamicBuffer<TaskComponent>, DynamicBuffer<RandomSelectorComponent>>().WithAll<RandomSelectorFlag, EvaluateFlag>().WithEntityAccess()) {
                for (int i = 0; i < randomSelectorComponents.Length; ++i) {
                    var randomSelectorComponent = randomSelectorComponents[i];
                    var taskComponent = taskComponents[randomSelectorComponent.Index];
                    var branchComponent = branchComponents[taskComponent.BranchIndex];

                    // Do not continue if there will be an interrupt or the branch cannot execute.
                    if (branchComponent.InterruptType != InterruptType.None || !branchComponent.CanExecute) {
                        continue;
                    }

                    var randomSelectorComponentsBuffer = randomSelectorComponents;
                    var taskComponentsBuffer = taskComponents;
                    var branchComponentBuffer = branchComponents;
                    if (taskComponent.Status == TaskStatus.Queued) {
                        taskComponent.Status = TaskStatus.Running;
                        taskComponentsBuffer[taskComponent.Index] = taskComponent;

                        // Initialize the task order array.
                        if (!randomSelectorComponent.TaskOrder.IsCreated) {
                            var childCount = TraversalUtility.GetImmediateChildCount(ref taskComponent, ref taskComponentsBuffer);
                            var builder = new BlobBuilder(Allocator.Temp);
                            ref var root = ref builder.ConstructRoot<IndiciesBlob>();
                            var orderArray = builder.Allocate(ref root.Indicies, childCount);
                            var childIndex = taskComponent.Index + 1;
                            for (int j = 0; j < childCount; ++j) {
                                orderArray[j] = (ushort)childIndex;
                                childIndex = taskComponents[childIndex].SiblingIndex;
                            }
                            randomSelectorComponent.TaskOrder = builder.CreateBlobAssetReference<IndiciesBlob>(Allocator.Persistent);
                            builder.Dispose();
                        }

                        // Generate a new random number seed for each entity.
                        if (randomSelectorComponent.RandomNumberGenerator.state == 0) {
                            randomSelectorComponent.RandomNumberGenerator = Unity.Mathematics.Random.CreateFromIndex(randomSelectorComponent.Seed != 0 ? randomSelectorComponent.Seed : (uint)entity.Index);
                        }

                        // Use fisher-yates to shuffle the array in place.
                        ref var initialTaskOrder = ref randomSelectorComponent.TaskOrder.Value.Indicies;
                        var index = initialTaskOrder.Length;
                        while (index != 0) {
                            var randomUnitFloat = randomSelectorComponent.RandomNumberGenerator.NextFloat();
                            var randomIndex = (int)Unity.Mathematics.math.floor(randomUnitFloat * index);
                            index--;

                            var element = initialTaskOrder[randomIndex];
                            initialTaskOrder[randomIndex] = initialTaskOrder[index];
                            initialTaskOrder[index] = element;
                        }

                        randomSelectorComponent.ActiveRelativeChildIndex = 0;
                        randomSelectorComponentsBuffer[i] = randomSelectorComponent;

                        branchComponent.NextIndex = initialTaskOrder[randomSelectorComponent.ActiveRelativeChildIndex];
                        branchComponentBuffer[taskComponent.BranchIndex] = branchComponent;

                        // The child may have already ran and have a non-inactive status.
                        var nextChildTaskComponent = taskComponents[branchComponent.NextIndex];
                        nextChildTaskComponent.Status = TaskStatus.Queued;
                        taskComponentsBuffer[branchComponent.NextIndex] = nextChildTaskComponent;
                    } else if (taskComponent.Status != TaskStatus.Running) {
                        continue;
                    }

                    // The randomSelector task is currently active. Check the first child.
                    ref var taskOrder = ref randomSelectorComponent.TaskOrder.Value.Indicies;
                    var childTaskComponent = taskComponents[taskOrder[randomSelectorComponent.ActiveRelativeChildIndex]];
                    if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running) {
                        // The child should keep running.
                        continue;
                    }

                    if (randomSelectorComponent.ActiveRelativeChildIndex == taskOrder.Length - 1 || childTaskComponent.Status == TaskStatus.Success) {
                        // There are no more children or the child succeeded. The random selector task should end. A task status of inactive indicates the last task was disabled. Return failure.
                        taskComponent.Status = childTaskComponent.Status != TaskStatus.Inactive ? childTaskComponent.Status : TaskStatus.Failure;
                        randomSelectorComponent.ActiveRelativeChildIndex = 0;
                        taskComponentsBuffer[randomSelectorComponent.Index] = taskComponent;

                        branchComponent.NextIndex = taskComponent.ParentIndex;
                        branchComponentBuffer[taskComponent.BranchIndex] = branchComponent;
                    } else {
                        // The child task returned failure. Move onto the next task. 
                        randomSelectorComponent.ActiveRelativeChildIndex++;
                        var nextIndex = taskOrder[randomSelectorComponent.ActiveRelativeChildIndex];
                        var nextTaskComponent = taskComponents[nextIndex];
                        nextTaskComponent.Status = TaskStatus.Queued;
                        taskComponentsBuffer[nextIndex] = nextTaskComponent;

                        branchComponent.NextIndex = nextIndex;
                        branchComponentBuffer[taskComponent.BranchIndex] = branchComponent;
                    }
                    randomSelectorComponentsBuffer[i] = randomSelectorComponent;
                }
            }
        }

        /// <summary>
        /// The task has been destroyed.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnDestroy(ref SystemState state)
        {
            foreach (var randomSelectorComponents in SystemAPI.Query<DynamicBuffer<RandomSelectorComponent>>()) {
                for (int i = 0; i < randomSelectorComponents.Length; ++i) {
                    var randomSelectorComponent = randomSelectorComponents[i];
                    if (randomSelectorComponent.TaskOrder.IsCreated) {
                        randomSelectorComponent.TaskOrder.Dispose();
                    }
                }
            }
        }
    }

    /// <summary>
    /// An interrupt has occurred. Ensure the task state is correct after the interruption.
    /// </summary>
    [DisableAutoCreation]

    public partial struct RandomSelectorInterruptSystem : ISystem
    {
        /// <summary>
        /// Runs the logic after an interruption.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            foreach (var (taskComponents, randomSelectorComponents) in
                SystemAPI.Query<DynamicBuffer<TaskComponent>, DynamicBuffer<RandomSelectorComponent>>().WithAll<InterruptFlag>()) {
                for (int i = 0; i < randomSelectorComponents.Length; ++i) {
                    var randomSelectorComponent = randomSelectorComponents[i];
                    // The active child will have a non-running status if it has been interrupted.
                    var taskComponent = taskComponents[randomSelectorComponent.Index];
                    if (taskComponent.Status == TaskStatus.Running && taskComponents[randomSelectorComponent.TaskOrder.Value.Indicies[randomSelectorComponent.ActiveRelativeChildIndex]].Status != TaskStatus.Running) {
                        ushort relativeChildIndex = 0;
                        // Find the currently active task.
                        while (taskComponents[randomSelectorComponent.TaskOrder.Value.Indicies[relativeChildIndex]].Status != TaskStatus.Running) {
                            relativeChildIndex++;
                        }
                        randomSelectorComponent.ActiveRelativeChildIndex = relativeChildIndex;
                        var randomSelectorBuffer = randomSelectorComponents;
                        randomSelectorBuffer[i] = randomSelectorComponent;
                    }
                }
            }
        }
    }
}
#endif