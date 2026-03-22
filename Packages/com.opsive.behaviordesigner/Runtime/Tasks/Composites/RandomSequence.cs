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
    /// A node representation of the random sequence task.
    /// </summary>
    [NodeIcon("edb30349221143a408c76da55a6aa809", "cfb9039832ed52748b617bde070898dc")]
    [Opsive.Shared.Utility.Description("Similar to the sequence task, the random sequence task will return success as soon as every child task returns success.  " +
                     "The difference is that the random sequence class will run its children in a random order. The sequence task is deterministic " +
                     "in that it will always run the tasks from left to right within the tree. The random sequence task shuffles the child tasks up and then begins " +
                     "execution in a random order. Other than that the random sequence class is the same as the sequence class. It will stop running tasks " +
                     "as soon as a single task ends in failure. On a task failure it will stop executing all of the child tasks and return failure. " +
                     "If no child returns failure then it will return success.")]
    public class RandomSequence : ECSCompositeTask<RandomSequenceTaskSystem, RandomSequenceComponent>, IParentNode, IConditionalAbortParent, IInterruptResponder, ISavableTask, ICloneable
    {
        [Tooltip("Specifies how the child conditional tasks should be reevaluated.")]
        [SerializeField] ConditionalAbortType m_AbortType;
        [Tooltip("The seed of the random number generator. Set to 0 to use the entity index as the seed.")]
        [SerializeField] uint m_Seed;

        private ushort m_ComponentIndex;

        public ConditionalAbortType AbortType { get => m_AbortType; set => m_AbortType = value; }
        public uint Seed { get => m_Seed; set => m_Seed = value; }

        public override ComponentType Flag { get => typeof(RandomSequenceFlag); }
        public Type InterruptSystemType { get => typeof(RandomSequenceInterruptSystem); }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override RandomSequenceComponent GetBufferElement()
        {
            return new RandomSequenceComponent()
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
            var randomSequenceComponents = world.EntityManager.GetBuffer<RandomSequenceComponent>(entity);
            var randomSequenceComponent = randomSequenceComponents[m_ComponentIndex];

            // Save the active child and array order.
            var saveData = new object[2];
            saveData[0] = randomSequenceComponent.ActiveRelativeChildIndex;
            if (randomSequenceComponent.TaskOrder.IsCreated) {
                var taskOrder = randomSequenceComponent.TaskOrder.Value.Indicies.ToArray();
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
            var randomSequenceComponents = world.EntityManager.GetBuffer<RandomSequenceComponent>(entity);
            var randomSequenceComponent = randomSequenceComponents[m_ComponentIndex];

            // saveData is the active child and array order.
            var taskSaveData = (object[])saveData;
            randomSequenceComponent.ActiveRelativeChildIndex = (ushort)taskSaveData[0];
            if (taskSaveData[1] != null) {
                var taskOrder = (ushort[])taskSaveData[1];
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<IndiciesBlob>();
                var orderArray = builder.Allocate(ref root.Indicies, taskOrder.Length);
                for (int i = 0; i < taskOrder.Length; i++) {
                    orderArray[i] = taskOrder[i];
                }
                randomSequenceComponent.TaskOrder = builder.CreateBlobAssetReference<IndiciesBlob>(Allocator.Persistent);
                builder.Dispose();
            }
            randomSequenceComponents[m_ComponentIndex] = randomSequenceComponent;
        }

        /// <summary>
        /// Creates a deep clone of the component.
        /// </summary>
        /// <returns>A deep clone of the component.</returns>
        public object Clone()
        {
            var clone = Activator.CreateInstance<RandomSequence>();
            clone.Index = Index;
            clone.ParentIndex = ParentIndex;
            clone.SiblingIndex = SiblingIndex;
            clone.AbortType = AbortType;
            return clone;
        }
    }

    /// <summary>
    /// The DOTS data structure for the RandomSequence class.
    /// </summary>
    public struct RandomSequenceComponent : IBufferElementData
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
    /// A DOTS tag indicating when a RandomSequence node is active.
    /// </summary>
    public struct RandomSequenceFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the RandomSequence logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct RandomSequenceTaskSystem : ISystem
    {
        /// <summary>
        /// Updates the logic.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            foreach (var (branchComponents, taskComponents, randomSequenceComponents, entity) in
                SystemAPI.Query<DynamicBuffer<BranchComponent>, DynamicBuffer<TaskComponent>, DynamicBuffer<RandomSequenceComponent>>().WithAll<RandomSequenceFlag, EvaluateFlag>().WithEntityAccess()) {
                for (int i = 0; i < randomSequenceComponents.Length; ++i) {
                    var randomSequenceComponent = randomSequenceComponents[i];
                    var taskComponent = taskComponents[randomSequenceComponent.Index];
                    var branchComponent = branchComponents[taskComponent.BranchIndex];

                    // Do not continue if there will be an interrupt or the branch cannot execute.
                    if (branchComponent.InterruptType != InterruptType.None || !branchComponent.CanExecute) {
                        continue;
                    }

                    var randomSequenceComponentsBuffer = randomSequenceComponents;
                    var taskComponentsBuffer = taskComponents;
                    var branchComponentBuffer = branchComponents;
                    if (taskComponent.Status == TaskStatus.Queued) {
                        taskComponent.Status = TaskStatus.Running;
                        taskComponentsBuffer[taskComponent.Index] = taskComponent;

                        // Initialize the task order array.
                        if (!randomSequenceComponent.TaskOrder.IsCreated) {
                            var childCount = TraversalUtility.GetImmediateChildCount(ref taskComponent, ref taskComponentsBuffer);
                            var builder = new BlobBuilder(Allocator.Temp);
                            ref var root = ref builder.ConstructRoot<IndiciesBlob>();
                            var orderArray = builder.Allocate(ref root.Indicies, childCount);
                            var childIndex = taskComponent.Index + 1;
                            for (int j = 0; j < childCount; ++j) {
                                orderArray[j] = (ushort)childIndex;
                                childIndex = taskComponents[childIndex].SiblingIndex;
                            }
                            randomSequenceComponent.TaskOrder = builder.CreateBlobAssetReference<IndiciesBlob>(Allocator.Persistent);
                            builder.Dispose();
                        }

                        // Generate a new random number seed for each entity.
                        if (randomSequenceComponent.RandomNumberGenerator.state == 0) {
                            randomSequenceComponent.RandomNumberGenerator = Unity.Mathematics.Random.CreateFromIndex(randomSequenceComponent.Seed != 0 ? randomSequenceComponent.Seed : (uint)entity.Index);
                        }

                        // Use fisher-yates to shuffle the array in place.
                        ref var initialTaskOrder = ref randomSequenceComponent.TaskOrder.Value.Indicies;
                        var index = initialTaskOrder.Length;
                        while (index != 0) {
                            var randomUnitFloat = randomSequenceComponent.RandomNumberGenerator.NextFloat();
                            var randomIndex = (int)Unity.Mathematics.math.floor(randomUnitFloat * index);
                            index--;

                            var element = initialTaskOrder[randomIndex];
                            initialTaskOrder[randomIndex] = initialTaskOrder[index];
                            initialTaskOrder[index] = element;
                        }

                        randomSequenceComponent.ActiveRelativeChildIndex = 0;
                        randomSequenceComponentsBuffer[i] = randomSequenceComponent;

                        branchComponent.NextIndex = initialTaskOrder[randomSequenceComponent.ActiveRelativeChildIndex];
                        branchComponentBuffer[taskComponent.BranchIndex] = branchComponent;

                        // Start the child.
                        var nextChildTaskComponent = taskComponents[branchComponent.NextIndex];
                        nextChildTaskComponent.Status = TaskStatus.Queued;
                        taskComponentsBuffer[branchComponent.NextIndex] = nextChildTaskComponent;
                    } else if (taskComponent.Status != TaskStatus.Running) {
                        continue;
                    }

                    // The randomSequence task is currently active. Check the first child.
                    ref var taskOrder = ref randomSequenceComponent.TaskOrder.Value.Indicies;
                    var childTaskComponent = taskComponents[taskOrder[randomSequenceComponent.ActiveRelativeChildIndex]];
                    if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running) {
                        // The child should keep running.
                        continue;
                    }

                    if (randomSequenceComponent.ActiveRelativeChildIndex == taskOrder.Length - 1 || childTaskComponent.Status == TaskStatus.Failure) {
                        // There are no more children or the child failed. The random sequence task should end. A task status of inactive indicates the last task was disabled. Return success.
                        taskComponent.Status = childTaskComponent.Status != TaskStatus.Inactive ? childTaskComponent.Status : TaskStatus.Success;
                        randomSequenceComponent.ActiveRelativeChildIndex = 0;
                        taskComponentsBuffer[randomSequenceComponent.Index] = taskComponent;

                        branchComponent.NextIndex = taskComponent.ParentIndex;
                        branchComponentBuffer[taskComponent.BranchIndex] = branchComponent;
                    } else {
                        // The child task returned success. Move onto the next task. 
                        randomSequenceComponent.ActiveRelativeChildIndex++;
                        var nextIndex = taskOrder[randomSequenceComponent.ActiveRelativeChildIndex];
                        var nextTaskComponent = taskComponents[nextIndex];
                        nextTaskComponent.Status = TaskStatus.Queued;
                        taskComponentsBuffer[nextIndex] = nextTaskComponent;

                        branchComponent.NextIndex = nextIndex;
                        branchComponentBuffer[taskComponent.BranchIndex] = branchComponent;
                    }
                    randomSequenceComponentsBuffer[i] = randomSequenceComponent;
                }
            }
        }

        /// <summary>
        /// The task has been destroyed.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnDestroy(ref SystemState state)
        {
            foreach (var randomSequenceComponents in SystemAPI.Query<DynamicBuffer<RandomSequenceComponent>>()) {
                for (int i = 0; i < randomSequenceComponents.Length; ++i) {
                    var randomSequenceComponent = randomSequenceComponents[i];
                    if (randomSequenceComponent.TaskOrder.IsCreated) {
                        randomSequenceComponent.TaskOrder.Dispose();
                    }
                }
            }
        }
    }

    /// <summary>
    /// An interrupt has occurred. Ensure the task state is correct after the interruption.
    /// </summary>
    [DisableAutoCreation]
    public partial struct RandomSequenceInterruptSystem : ISystem
    {
        /// <summary>
        /// Runs the logic after an interruption.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            foreach (var (taskComponents, randomSequenceComponents) in
                SystemAPI.Query<DynamicBuffer<TaskComponent>, DynamicBuffer<RandomSequenceComponent>>().WithAll<InterruptFlag>()) {
                for (int i = 0; i < randomSequenceComponents.Length; ++i) {
                    var randomSequenceComponent = randomSequenceComponents[i];
                    // The active child will have a non-running status if it has been interrupted.
                    var taskComponent = taskComponents[randomSequenceComponent.Index];
                    if (taskComponent.Status == TaskStatus.Running && taskComponents[randomSequenceComponent.TaskOrder.Value.Indicies[randomSequenceComponent.ActiveRelativeChildIndex]].Status != TaskStatus.Running) {
                        ushort relativeChildIndex = 0;
                        // Find the currently active task.
                        while (taskComponents[randomSequenceComponent.TaskOrder.Value.Indicies[relativeChildIndex]].Status != TaskStatus.Running) {
                            relativeChildIndex++;
                        }
                        randomSequenceComponent.ActiveRelativeChildIndex = relativeChildIndex;
                        var randomSequenceBuffer = randomSequenceComponents;
                        randomSequenceBuffer[i] = randomSequenceComponent;
                    }
                }
            }
        }
    }
}
#endif