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
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.Shared.Utility;
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
    public class RandomSequence : ECSCompositeTask<RandomSequenceTaskSystem, RandomSequenceComponent, RandomSequenceFlag>, IParentNode, IConditionalAbortParent, IInterruptResponder, ISavableTask, ICloneable
    {
        [Tooltip("Specifies how the child conditional tasks should be reevaluated.")]
        [SerializeField] ConditionalAbortType m_AbortType;
        [Tooltip("The seed of the random number generator. Set to 0 to use the entity index as the seed.")]
        [SerializeField] uint m_Seed;

        private ushort m_ComponentIndex;

        public ConditionalAbortType AbortType { get => m_AbortType; set => m_AbortType = value; }
        public uint Seed { get => m_Seed; set => m_Seed = value; }

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
                TaskOrderStartIndex = -1,
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
            if (!world.EntityManager.HasBuffer<RandomSequenceTaskOrderComponent>(entity)) {
                world.EntityManager.AddBuffer<RandomSequenceTaskOrderComponent>(entity);
            }
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
            if (randomSequenceComponent.TaskOrderStartIndex >= 0 && randomSequenceComponent.TaskOrderLength > 0 &&
                world.EntityManager.HasBuffer<RandomSequenceTaskOrderComponent>(entity)) {
                var taskOrderComponents = world.EntityManager.GetBuffer<RandomSequenceTaskOrderComponent>(entity);
                if (randomSequenceComponent.TaskOrderStartIndex + randomSequenceComponent.TaskOrderLength <= taskOrderComponents.Length) {
                    var taskOrder = new ushort[randomSequenceComponent.TaskOrderLength];
                    for (int i = 0; i < randomSequenceComponent.TaskOrderLength; ++i) {
                        taskOrder[i] = taskOrderComponents[randomSequenceComponent.TaskOrderStartIndex + i].TaskIndex;
                    }
                    saveData[1] = taskOrder;
                }
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
            DynamicBuffer<RandomSequenceTaskOrderComponent> taskOrderComponents;
            if (world.EntityManager.HasBuffer<RandomSequenceTaskOrderComponent>(entity)) {
                taskOrderComponents = world.EntityManager.GetBuffer<RandomSequenceTaskOrderComponent>(entity);
            } else {
                taskOrderComponents = world.EntityManager.AddBuffer<RandomSequenceTaskOrderComponent>(entity);
            }

            // saveData is the active child and array order.
            var taskSaveData = (object[])saveData;
            randomSequenceComponent.ActiveRelativeChildIndex = (ushort)taskSaveData[0];
            if (taskSaveData[1] != null) {
                var taskOrder = (ushort[])taskSaveData[1];
                if (taskOrder.Length == 0) {
                    randomSequenceComponent.TaskOrderStartIndex = -1;
                    randomSequenceComponent.TaskOrderLength = 0;
                } else {
                    if (randomSequenceComponent.TaskOrderStartIndex < 0 || randomSequenceComponent.TaskOrderLength != taskOrder.Length ||
                        randomSequenceComponent.TaskOrderStartIndex + randomSequenceComponent.TaskOrderLength > taskOrderComponents.Length) {
                        randomSequenceComponent.TaskOrderStartIndex = taskOrderComponents.Length;
                        randomSequenceComponent.TaskOrderLength = (ushort)taskOrder.Length;
                        for (int i = 0; i < taskOrder.Length; i++) {
                            taskOrderComponents.Add(new RandomSequenceTaskOrderComponent() { TaskIndex = taskOrder[i] });
                        }
                    } else {
                        randomSequenceComponent.TaskOrderLength = (ushort)taskOrder.Length;
                        for (int i = 0; i < taskOrder.Length; i++) {
                            taskOrderComponents[randomSequenceComponent.TaskOrderStartIndex + i] = new RandomSequenceTaskOrderComponent() { TaskIndex = taskOrder[i] };
                        }
                    }
                }
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
            clone.Enabled = Enabled;
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
        [Tooltip("The start index within the RandomSequenceTaskOrderComponent buffer.")]
        public int TaskOrderStartIndex;
        [Tooltip("The number of task order entries used by this random sequence.")]
        public ushort TaskOrderLength;
    }

    /// <summary>
    /// Stores the mutable child execution order for RandomSequence components.
    /// </summary>
    public struct RandomSequenceTaskOrderComponent : IBufferElementData
    {
        [Tooltip("The index of the child task.")]
        public ushort TaskIndex;
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
        private EntityQuery m_Query;

        /// <summary>
        /// Builds the query.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnCreate(ref SystemState state)
        {
            m_Query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<RandomSequenceComponent>().WithAllRW<RandomSequenceTaskOrderComponent>().WithAll<RandomSequenceFlag, EvaluateFlag>().Build();
        }

        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            state.Dependency = new RandomSequenceJob().ScheduleParallel(m_Query, state.Dependency);
        }

        /// <summary>
        /// Job which executes the task logic.
        /// </summary>
        [BurstCompile]
        private partial struct RandomSequenceJob : IJobEntity
        {
            /// <summary>
            /// Executes the random sequence logic.
            /// </summary>
            /// <param name="branchComponents">An array of BranchComponents.</param>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="randomSequenceComponents">An array of RandomSequenceComponents.</param>
            /// <param name="entity">The entity being executed.</param>
            [BurstCompile]
            public void Execute(ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents,
                ref DynamicBuffer<RandomSequenceComponent> randomSequenceComponents, ref DynamicBuffer<RandomSequenceTaskOrderComponent> taskOrderComponents, Entity entity)
            {
                for (int i = 0; i < randomSequenceComponents.Length; ++i) {
                    var randomSequenceComponent = randomSequenceComponents[i];
                    var taskComponent = taskComponents[randomSequenceComponent.Index];
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
                        taskComponent.Status = TaskStatus.Running;
                        taskComponents[taskComponent.Index] = taskComponent;

                        // Initialize the task order array.
                        if (randomSequenceComponent.TaskOrderStartIndex < 0 || randomSequenceComponent.TaskOrderLength == 0) {
                            var childCount = TraversalUtility.GetImmediateChildCount(ref taskComponent, ref taskComponents);
                            if (childCount == 0) {
                                taskComponent.Status = TaskStatus.Success;
                                taskComponents[randomSequenceComponent.Index] = taskComponent;

                                branchComponent.NextIndex = taskComponent.ParentIndex;
                                branchComponents[taskComponent.BranchIndex] = branchComponent;
                                randomSequenceComponents[i] = randomSequenceComponent;
                                continue;
                            }

                            randomSequenceComponent.TaskOrderStartIndex = taskOrderComponents.Length;
                            randomSequenceComponent.TaskOrderLength = (ushort)childCount;
                            var childIndex = taskComponent.Index + 1;
                            for (int j = 0; j < childCount; ++j) {
                                taskOrderComponents.Add(new RandomSequenceTaskOrderComponent() { TaskIndex = (ushort)childIndex });
                                childIndex = taskComponents[childIndex].SiblingIndex;
                            }
                        }

                        // Generate a new random number seed for each entity.
                        if (randomSequenceComponent.RandomNumberGenerator.state == 0) {
                            randomSequenceComponent.RandomNumberGenerator = Unity.Mathematics.Random.CreateFromIndex(randomSequenceComponent.Seed != 0 ? randomSequenceComponent.Seed : (uint)entity.Index);
                        }

                        randomSequenceComponent.ActiveRelativeChildIndex = 0;
                        if (randomSequenceComponent.TaskOrderLength > 1) {
                            // Lazy Fisher-Yates: only place the first entry now.
                            var randomIndex = randomSequenceComponent.RandomNumberGenerator.NextInt(randomSequenceComponent.TaskOrderLength);
                            var randomTaskOrderIndex = randomSequenceComponent.TaskOrderStartIndex + randomIndex;
                            var firstTaskOrderIndex = randomSequenceComponent.TaskOrderStartIndex;
                            var element = taskOrderComponents[randomTaskOrderIndex];
                            taskOrderComponents[randomTaskOrderIndex] = taskOrderComponents[firstTaskOrderIndex];
                            taskOrderComponents[firstTaskOrderIndex] = element;
                        }

                        randomSequenceComponents[i] = randomSequenceComponent;

                        branchComponent.NextIndex = taskOrderComponents[randomSequenceComponent.TaskOrderStartIndex + randomSequenceComponent.ActiveRelativeChildIndex].TaskIndex;
                        branchComponents[taskComponent.BranchIndex] = branchComponent;

                        // Start the child.
                        var nextChildTaskComponent = taskComponents[branchComponent.NextIndex];
                        nextChildTaskComponent.Status = TaskStatus.Queued;
                        taskComponents[branchComponent.NextIndex] = nextChildTaskComponent;
                    }

                    // The randomSequence task is currently active. Check the active child.
                    var activeTaskOrderIndex = randomSequenceComponent.TaskOrderStartIndex + randomSequenceComponent.ActiveRelativeChildIndex;
                    var childTaskComponent = taskComponents[taskOrderComponents[activeTaskOrderIndex].TaskIndex];
                    if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running) {
                        // The child should keep running.
                        continue;
                    }

                    if (randomSequenceComponent.ActiveRelativeChildIndex == randomSequenceComponent.TaskOrderLength - 1 || childTaskComponent.Status == TaskStatus.Failure) {
                        // There are no more children or the child failed. The random sequence task should end. A task status of inactive indicates the last task was disabled. Return success.
                        taskComponent.Status = childTaskComponent.Status != TaskStatus.Inactive ? childTaskComponent.Status : TaskStatus.Success;
                        randomSequenceComponent.ActiveRelativeChildIndex = 0;
                        taskComponents[randomSequenceComponent.Index] = taskComponent;

                        branchComponent.NextIndex = taskComponent.ParentIndex;
                        branchComponents[taskComponent.BranchIndex] = branchComponent;
                    } else {
                        // The child task returned success. Move onto the next random task.
                        randomSequenceComponent.ActiveRelativeChildIndex++;
                        var nextRelativeChildIndex = randomSequenceComponent.ActiveRelativeChildIndex;
                        var remainingCount = randomSequenceComponent.TaskOrderLength - nextRelativeChildIndex;
                        if (remainingCount > 1) {
                            // Lazy Fisher-Yates: place only the next slot.
                            var randomIndex = nextRelativeChildIndex + randomSequenceComponent.RandomNumberGenerator.NextInt(remainingCount);
                            var randomTaskOrderIndex = randomSequenceComponent.TaskOrderStartIndex + randomIndex;
                            var nextTaskOrderIndex = randomSequenceComponent.TaskOrderStartIndex + nextRelativeChildIndex;
                            var element = taskOrderComponents[randomTaskOrderIndex];
                            taskOrderComponents[randomTaskOrderIndex] = taskOrderComponents[nextTaskOrderIndex];
                            taskOrderComponents[nextTaskOrderIndex] = element;
                        }

                        var nextIndex = taskOrderComponents[randomSequenceComponent.TaskOrderStartIndex + nextRelativeChildIndex].TaskIndex;
                        var nextTaskComponent = taskComponents[nextIndex];
                        nextTaskComponent.Status = TaskStatus.Queued;
                        taskComponents[nextIndex] = nextTaskComponent;

                        branchComponent.NextIndex = nextIndex;
                        branchComponents[taskComponent.BranchIndex] = branchComponent;
                    }
                    randomSequenceComponents[i] = randomSequenceComponent;
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
            foreach (var (taskComponents, randomSequenceComponents, taskOrderComponents) in
                SystemAPI.Query<DynamicBuffer<TaskComponent>, DynamicBuffer<RandomSequenceComponent>, DynamicBuffer<RandomSequenceTaskOrderComponent>>().WithAll<InterruptFlag>()) {
                for (int i = 0; i < randomSequenceComponents.Length; ++i) {
                    var randomSequenceComponent = randomSequenceComponents[i];
                    // The active child will have a non-running status if it has been interrupted.
                    var taskComponent = taskComponents[randomSequenceComponent.Index];
                    var taskOrderEndIndex = randomSequenceComponent.TaskOrderStartIndex + randomSequenceComponent.TaskOrderLength;
                    if (randomSequenceComponent.TaskOrderStartIndex < 0 || randomSequenceComponent.TaskOrderLength == 0 ||
                        taskOrderEndIndex > taskOrderComponents.Length || randomSequenceComponent.ActiveRelativeChildIndex >= randomSequenceComponent.TaskOrderLength) {
                        continue;
                    }

                    if (taskComponent.Status == TaskStatus.Running && taskComponents[taskOrderComponents[randomSequenceComponent.TaskOrderStartIndex + randomSequenceComponent.ActiveRelativeChildIndex].TaskIndex].Status != TaskStatus.Running) {
                        ushort relativeChildIndex = 0;
                        // Find the currently active task.
                        while (relativeChildIndex < randomSequenceComponent.TaskOrderLength &&
                            taskComponents[taskOrderComponents[randomSequenceComponent.TaskOrderStartIndex + relativeChildIndex].TaskIndex].Status != TaskStatus.Running) {
                            relativeChildIndex++;
                        }
                        if (relativeChildIndex < randomSequenceComponent.TaskOrderLength) {
                            randomSequenceComponent.ActiveRelativeChildIndex = relativeChildIndex;
                            var randomSequenceBuffer = randomSequenceComponents;
                            randomSequenceBuffer[i] = randomSequenceComponent;
                        }
                    }
                }
            }
        }
    }
}
#endif