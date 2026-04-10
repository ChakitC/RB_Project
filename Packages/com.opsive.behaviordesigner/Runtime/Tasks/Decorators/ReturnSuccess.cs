#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Decorators
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.GraphDesigner.Runtime;
    using Unity.Burst;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// A node representation of the return success task.
    /// </summary>
    [NodeIcon("66f47acff1d46f848bc8c22b221ee1d0", "3eb990b93a7fd6e479d6b032c7e6973f")]
    [Opsive.Shared.Utility.Description("The return success task will always return success except when the child task is running.")]
    public class ReturnSuccess : ECSDecoratorTask<ReturnSuccessTaskSystem, ReturnSuccessComponent, ReturnSuccessFlag>, IParentNode
    {
        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override ReturnSuccessComponent GetBufferElement()
        {
            return new ReturnSuccessComponent()
            {
                Index = RuntimeIndex,
            };
        }
    }

    /// <summary>
    /// The DOTS data structure for the ReturnSuccess class.
    /// </summary>
    public struct ReturnSuccessComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
    }

    /// <summary>
    /// A DOTS tag indicating when an ReturnSuccess node is active.
    /// </summary>
    public struct ReturnSuccessFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the ReturnSuccess logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct ReturnSuccessTaskSystem : ISystem
    {
        private EntityQuery m_Query;

        /// <summary>
        /// Builds the query.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnCreate(ref SystemState state)
        {
            m_Query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<ReturnSuccessComponent>().WithAll<ReturnSuccessFlag, EvaluateFlag>().Build();
        }

        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ReturnSuccessJob().ScheduleParallel(m_Query, state.Dependency);
        }

        /// <summary>
        /// Job which executes the task logic.
        /// </summary>
        [BurstCompile]
        private partial struct ReturnSuccessJob : IJobEntity
        {
            /// <summary>
            /// Executes the return success logic.
            /// </summary>
            /// <param name="branchComponents">An array of BranchComponents.</param>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="returnSuccessComponents">An array of ReturnSuccessComponents.</param>
            [BurstCompile]
            public void Execute(ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<ReturnSuccessComponent> returnSuccessComponents)
            {
                for (int i = 0; i < returnSuccessComponents.Length; ++i) {
                    var returnSuccessComponent = returnSuccessComponents[i];
                    var taskComponent = taskComponents[returnSuccessComponent.Index];
                    var taskStatus = taskComponent.Status;
                    if (taskStatus != TaskStatus.Queued && taskStatus != TaskStatus.Running) {
                        continue;
                    }

                    var branchComponent = branchComponents[taskComponent.BranchIndex];
                    if (!branchComponent.CanExecute || branchComponent.InterruptType != InterruptType.None) {
                        continue;
                    }

                    var childIndex = (ushort)(taskComponent.Index + 1);
                    TaskComponent childTaskComponent;
                    if (taskStatus == TaskStatus.Queued) {
                        taskComponent.Status = TaskStatus.Running;
                        taskComponents[taskComponent.Index] = taskComponent;

                        childTaskComponent = taskComponents[childIndex];
                        if (childTaskComponent.Status != TaskStatus.Queued) {
                            childTaskComponent.Status = TaskStatus.Queued;
                            taskComponents[childIndex] = childTaskComponent;
                        }

                        if (branchComponent.NextIndex != childIndex) {
                            branchComponent.NextIndex = childIndex;
                            branchComponents[taskComponent.BranchIndex] = branchComponent;
                        }
                        continue;
                    }

                    // The return success task is currently active. Check the first child.
                    childTaskComponent = taskComponents[childIndex];
                    if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running) {
                        // The child should keep running.
                        continue;
                    }

                    // The child has completed. Return success.
                    taskComponent.Status = TaskStatus.Success;
                    taskComponents[taskComponent.Index] = taskComponent;

                    if (branchComponent.NextIndex != taskComponent.ParentIndex) {
                        branchComponent.NextIndex = taskComponent.ParentIndex;
                        branchComponents[taskComponent.BranchIndex] = branchComponent;
                    }
                }
            }
        }
    }
}
#endif