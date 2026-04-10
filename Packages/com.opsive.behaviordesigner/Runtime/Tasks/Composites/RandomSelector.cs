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
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
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
    public class RandomSelector : ECSCompositeTask<RandomSelectorTaskSystem, RandomSelectorComponent, RandomSelectorFlag>, IParentNode, IConditionalAbortParent, IInterruptResponder, ISavableTask, ICloneable
    {
        [Tooltip("Specifies how the child conditional tasks should be reevaluated.")]
        [SerializeField] ConditionalAbortType m_AbortType;
        [Tooltip("The seed of the random number generator. Set to 0 to use the entity index as the seed.")]
        [SerializeField] uint m_Seed;

        private ushort m_ComponentIndex;

        public ConditionalAbortType AbortType { get => m_AbortType; set => m_AbortType = value; }
        public uint Seed { get => m_Seed; set => m_Seed = value; }

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
            var randomSelectorComponents = world.EntityManager.GetBuffer<RandomSelectorComponent>(entity);
            var randomSelectorComponent = randomSelectorComponents[m_ComponentIndex];

            // Save the active child and array order.
            var saveData = new object[2];
            saveData[0] = randomSelectorComponent.ActiveRelativeChildIndex;
            if (randomSelectorComponent.TaskOrder.IsCreated) {
                var taskOrder = new ushort[randomSelectorComponent.TaskOrder.Length];
                for (int i = 0; i < randomSelectorComponent.TaskOrder.Length; ++i) {
                    taskOrder[i] = randomSelectorComponent.TaskOrder[i];
                }
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
                if (randomSelectorComponent.TaskOrder.IsCreated) {
                    randomSelectorComponent.TaskOrder.Dispose();
                }
                randomSelectorComponent.TaskOrder = new UnsafeList<ushort>(taskOrder.Length, Allocator.Persistent);
                randomSelectorComponent.TaskOrder.Resize(taskOrder.Length);
                for (int i = 0; i < taskOrder.Length; i++) {
                    randomSelectorComponent.TaskOrder[i] = taskOrder[i];
                }
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
            clone.Enabled = Enabled;
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
        public UnsafeList<ushort> TaskOrder;
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
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<RandomSelectorComponent>().WithAll<RandomSelectorFlag, EvaluateFlag>().Build();
            state.Dependency = new RandomSelectorJob().ScheduleParallel(query, state.Dependency);
        }

        /// <summary>
        /// Job which executes the task logic.
        /// </summary>
        [BurstCompile]
        private partial struct RandomSelectorJob : IJobEntity
        {
            /// <summary>
            /// Executes the random selector logic.
            /// </summary>
            /// <param name="branchComponents">An array of BranchComponents.</param>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="randomSelectorComponents">An array of RandomSelectorComponents.</param>
            /// <param name="entity">The entity being executed.</param>
            [BurstCompile]
            public void Execute(ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<RandomSelectorComponent> randomSelectorComponents, Entity entity)
            {
                for (int i = 0; i < randomSelectorComponents.Length; ++i) {
                    var randomSelectorComponent = randomSelectorComponents[i];
                    var taskComponent = taskComponents[randomSelectorComponent.Index];
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
                        if (!randomSelectorComponent.TaskOrder.IsCreated) {
                            var childCount = TraversalUtility.GetImmediateChildCount(ref taskComponent, ref taskComponents);
                            randomSelectorComponent.TaskOrder = new UnsafeList<ushort>(childCount, Allocator.Persistent);
                            randomSelectorComponent.TaskOrder.Resize(childCount);
                            var childIndex = taskComponent.Index + 1;
                            for (int j = 0; j < childCount; ++j) {
                                randomSelectorComponent.TaskOrder[j] = (ushort)childIndex;
                                childIndex = taskComponents[childIndex].SiblingIndex;
                            }
                        }

                        // Generate a new random number seed for each entity.
                        if (randomSelectorComponent.RandomNumberGenerator.state == 0) {
                            randomSelectorComponent.RandomNumberGenerator = Unity.Mathematics.Random.CreateFromIndex(randomSelectorComponent.Seed != 0 ? randomSelectorComponent.Seed : (uint)entity.Index);
                        }

                        randomSelectorComponent.ActiveRelativeChildIndex = 0;
                        if (randomSelectorComponent.TaskOrder.Length > 1) {
                            // Lazy Fisher-Yates: only place the first entry now.
                            var randomIndex = randomSelectorComponent.RandomNumberGenerator.NextInt(randomSelectorComponent.TaskOrder.Length);
                            var element = randomSelectorComponent.TaskOrder[randomIndex];
                            randomSelectorComponent.TaskOrder[randomIndex] = randomSelectorComponent.TaskOrder[0];
                            randomSelectorComponent.TaskOrder[0] = element;
                        }

                        randomSelectorComponents[i] = randomSelectorComponent;

                        branchComponent.NextIndex = randomSelectorComponent.TaskOrder[randomSelectorComponent.ActiveRelativeChildIndex];
                        branchComponents[taskComponent.BranchIndex] = branchComponent;

                        // The child may have already ran and have a non-inactive status.
                        var nextChildTaskComponent = taskComponents[branchComponent.NextIndex];
                        nextChildTaskComponent.Status = TaskStatus.Queued;
                        taskComponents[branchComponent.NextIndex] = nextChildTaskComponent;
                    }

                    // The randomSelector task is currently active. Check the active child.
                    var childTaskComponent = taskComponents[randomSelectorComponent.TaskOrder[randomSelectorComponent.ActiveRelativeChildIndex]];
                    if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running) {
                        // The child should keep running.
                        continue;
                    }

                    if (randomSelectorComponent.ActiveRelativeChildIndex == randomSelectorComponent.TaskOrder.Length - 1 || childTaskComponent.Status == TaskStatus.Success) {
                        // There are no more children or the child succeeded. The random selector task should end. A task status of inactive indicates the last task was disabled. Return failure.
                        taskComponent.Status = childTaskComponent.Status != TaskStatus.Inactive ? childTaskComponent.Status : TaskStatus.Failure;
                        randomSelectorComponent.ActiveRelativeChildIndex = 0;
                        taskComponents[randomSelectorComponent.Index] = taskComponent;

                        branchComponent.NextIndex = taskComponent.ParentIndex;
                        branchComponents[taskComponent.BranchIndex] = branchComponent;
                    } else {
                        // The child task returned failure. Move onto the next random task.
                        randomSelectorComponent.ActiveRelativeChildIndex++;
                        var nextRelativeChildIndex = randomSelectorComponent.ActiveRelativeChildIndex;
                        var remainingCount = randomSelectorComponent.TaskOrder.Length - nextRelativeChildIndex;
                        if (remainingCount > 1) {
                            // Lazy Fisher-Yates: place only the next slot.
                            var randomIndex = nextRelativeChildIndex + randomSelectorComponent.RandomNumberGenerator.NextInt(remainingCount);
                            var element = randomSelectorComponent.TaskOrder[randomIndex];
                            randomSelectorComponent.TaskOrder[randomIndex] = randomSelectorComponent.TaskOrder[nextRelativeChildIndex];
                            randomSelectorComponent.TaskOrder[nextRelativeChildIndex] = element;
                        }

                        var nextIndex = randomSelectorComponent.TaskOrder[nextRelativeChildIndex];
                        var nextTaskComponent = taskComponents[nextIndex];
                        nextTaskComponent.Status = TaskStatus.Queued;
                        taskComponents[nextIndex] = nextTaskComponent;

                        branchComponent.NextIndex = nextIndex;
                        branchComponents[taskComponent.BranchIndex] = branchComponent;
                    }
                    randomSelectorComponents[i] = randomSelectorComponent;
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
                    if (!randomSelectorComponent.TaskOrder.IsCreated) {
                        continue;
                    }

                    var taskOrder = randomSelectorComponent.TaskOrder;
                    if (taskOrder.Length == 0 || randomSelectorComponent.ActiveRelativeChildIndex >= taskOrder.Length) {
                        continue;
                    }

                    if (taskComponent.Status == TaskStatus.Running && taskComponents[taskOrder[randomSelectorComponent.ActiveRelativeChildIndex]].Status != TaskStatus.Running) {
                        ushort relativeChildIndex = 0;
                        // Find the currently active task.
                        while (relativeChildIndex < taskOrder.Length && taskComponents[taskOrder[relativeChildIndex]].Status != TaskStatus.Running) {
                            relativeChildIndex++;
                        }
                        if (relativeChildIndex < taskOrder.Length) {
                            randomSelectorComponent.ActiveRelativeChildIndex = relativeChildIndex;
                            var randomSelectorBuffer = randomSelectorComponents;
                            randomSelectorBuffer[i] = randomSelectorComponent;
                        }
                    }
                }
            }
        }
    }
}
#endif