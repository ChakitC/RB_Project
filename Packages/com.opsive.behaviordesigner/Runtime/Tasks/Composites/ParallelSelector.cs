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
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// A node representation of the parallel selector task.
    /// </summary>
    [NodeIcon("d47aff1a00bcc6d4da8ca0df32ed8415", "108591b5d7a6bd94383d16a62cb3b4a7")]
    [Opsive.Shared.Utility.Description("Similar to the selector task, the parallel selector task will return success as soon as a child task returns success. " +
                     "The parallel task will run all of its children tasks simultaneously versus running each task one at a time. " +
                     "If one tasks returns success the parallel selector task will end all of the child tasks and return success. " +
                     "If every child task returns failure then the parallel selector task will return failure.")]
    public class ParallelSelector : ECSCompositeTask<ParallelSelectorTaskSystem, ParallelSelectorComponent>, IParentNode, IParallelNode
    {
        /// <summary>
        /// The type of tag that should be enabled when the task is running.
        /// </summary>
        public override ComponentType Flag { get => typeof(ParallelSelectorFlag); }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override ParallelSelectorComponent GetBufferElement()
        {
            return new ParallelSelectorComponent() {
                Index = RuntimeIndex,
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
            var index = base.AddBufferElement(world, entity, gameObject);
            ComponentUtility.AddInterruptComponents(world.EntityManager, entity);
            return index;
        }
    }

    /// <summary>
    /// The DOTS data structure for the ParallelSelector class.
    /// </summary>
    public struct ParallelSelectorComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        [SerializeField] ushort m_Index;

        public ushort Index { get => m_Index; set => m_Index = value; }
    }

    /// <summary>
    /// A DOTS tag indicating when a ParallelSelector node is active.
    /// </summary>
    public struct ParallelSelectorFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the ParallelSelector logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct ParallelSelectorTaskSystem : ISystem
    {
        private EntityQuery m_Query;
        private EntityCommandBuffer m_EntityCommandBuffer;
        private JobHandle m_Dependency;

        /// <summary>
        /// Builds the query.
        /// </summary>
        /// <param name="state">THe current SystemState.</param>
        private void OnCreate(ref SystemState state)
        {
            m_Query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<ParallelSelectorComponent>().WithAll<ParallelSelectorFlag, EvaluateFlag>().Build();
        }

        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            m_Dependency.Complete();
            if (m_EntityCommandBuffer.IsCreated) {
                m_EntityCommandBuffer.Playback(state.EntityManager);
                m_EntityCommandBuffer.Dispose();
            }

            m_EntityCommandBuffer = new EntityCommandBuffer(Allocator.TempJob);
            state.Dependency = new ParallelSelectorJob()
            {
                EntityCommandBuffer = m_EntityCommandBuffer.AsParallelWriter()
            }.ScheduleParallel(m_Query, state.Dependency);
            m_Dependency = state.Dependency;
        }

        /// <summary>
        /// The system has been destroyed.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        public void OnDestroy(ref SystemState state)
        {
            if (m_EntityCommandBuffer.IsCreated) {
                m_EntityCommandBuffer.Playback(state.EntityManager);
                m_EntityCommandBuffer.Dispose();
            }
        }

        /// <summary>
        /// Job which executes the task logic.
        /// </summary>
        [BurstCompile]
        private partial struct ParallelSelectorJob : IJobEntity
        {
            [Tooltip("CommandBuffer which sets the component data.")]
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            /// <summary>
            /// Executes the parallel selector logic.
            /// </summary>
            /// <param name="entity">The entity that is being acted upon.</param>
            /// <param name="entityIndex">The index of the entity.</param>
            /// <param name="branchComponents">An array of BranchComponents.</param>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="parallelSelectorComponents">An array of ParallelSelectorComponents.</param>
            [BurstCompile]
            public void Execute(Entity entity, [EntityIndexInQuery] int entityIndex, ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<ParallelSelectorComponent> parallelSelectorComponents)
            {
                for (int i = 0; i < parallelSelectorComponents.Length; ++i) {
                    var parallelSelectorComponent = parallelSelectorComponents[i];
                    var taskComponent = taskComponents[parallelSelectorComponent.Index];
                    var branchComponent = branchComponents[taskComponent.BranchIndex];

                    // Do not continue if there will be an interrupt.
                    if (branchComponent.InterruptType != InterruptType.None) {
                        continue;
                    }

                    ushort childIndex;
                    TaskComponent childTaskComponent;
                    if (taskComponent.Status == TaskStatus.Queued) {
                        taskComponent.Status = TaskStatus.Running;
                        taskComponents[taskComponent.Index] = taskComponent;

                        childIndex = (ushort)(parallelSelectorComponent.Index + 1);
                        while (childIndex != ushort.MaxValue) {
                            childTaskComponent = taskComponents[childIndex];
                            childTaskComponent.Status = TaskStatus.Queued;
                            taskComponents[childIndex] = childTaskComponent;

                            var childBranchComponent = branchComponents[childTaskComponent.BranchIndex];
                            childBranchComponent.NextIndex = childTaskComponent.Index;
                            branchComponents[childTaskComponent.BranchIndex] = childBranchComponent;

                            childIndex = taskComponents[childIndex].SiblingIndex;
                        }
                    } else if (taskComponent.Status != TaskStatus.Running) {
                        continue;
                    }

                    var childSuccess = false;
                    var childrenRunning = false;
                    childIndex = (ushort)(parallelSelectorComponent.Index + 1);
                    while (childIndex != ushort.MaxValue) {
                        childTaskComponent = taskComponents[childIndex];
                        if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running) {
                            childrenRunning = true;
                        } else if (childTaskComponent.Status == TaskStatus.Failure) {
                            var childBranchComponent = branchComponents[childTaskComponent.BranchIndex];
                            if (childBranchComponent.ActiveIndex != ushort.MaxValue) {
                                childBranchComponent.NextIndex = ushort.MaxValue;
                                branchComponents[childTaskComponent.BranchIndex] = childBranchComponent;
                            }
                        } else if (childTaskComponent.Status == TaskStatus.Success) {
                            childSuccess = true;

                            var childBranchComponent = branchComponents[childTaskComponent.BranchIndex];
                            childBranchComponent.NextIndex = ushort.MaxValue;
                            branchComponents[childTaskComponent.BranchIndex] = childBranchComponent;
                            break;
                        }
                        childIndex = taskComponents[childIndex].SiblingIndex;
                    }

                    // If a single child succeeds then all tasks should be stopped.
                    if (childSuccess) {
                        var maxChildIndex = taskComponent.Index + TraversalUtility.GetChildCount(taskComponent.Index, ref taskComponents);
                        for (ushort j = (ushort)(taskComponent.Index + 1); j <= maxChildIndex; ++j) {
                            childTaskComponent = taskComponents[j];
                            if (childTaskComponent.Status == TaskStatus.Running || childTaskComponent.Status == TaskStatus.Queued) {
                                childTaskComponent.Status = TaskStatus.Failure;
                                taskComponents[j] = childTaskComponent;

                                branchComponent = branchComponents[childTaskComponent.BranchIndex];
                                EntityCommandBuffer.SetComponentEnabled<InterruptedFlag>(entityIndex, entity, true);
                                if (branchComponent.ActiveIndex == childTaskComponent.Index) {
                                    branchComponent.NextIndex = ushort.MaxValue;
                                    branchComponents[childTaskComponent.BranchIndex] = branchComponent;
                                }
                            }
                        }
                    } else if (childrenRunning) {
                        continue;
                    }

                    // No more children are running. Resume the parent task.
                    taskComponent.Status = TaskStatus.Success;
                    taskComponents[taskComponent.Index] = taskComponent;

                    branchComponent.NextIndex = taskComponent.ParentIndex;
                    branchComponents[taskComponent.BranchIndex] = branchComponent;
                }
            }
        }
    }
}
#endif