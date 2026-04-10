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
    /// A node representation of the until success task.
    /// </summary>
    [NodeIcon("f2e750025a5812640919385b75319d6f", "4e9ac4f2dd8bfe741a5f889efb1ade67")]
    [Opsive.Shared.Utility.Description("The until success task will keep executing its child task until the child task returns success.")]
    public class UntilSuccess : ECSDecoratorTask<UntilSuccessTaskSystem, UntilSuccessComponent, UntilSuccessFlag>, IParentNode
    {
        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override UntilSuccessComponent GetBufferElement()
        {
            return new UntilSuccessComponent()
            {
                Index = RuntimeIndex,
            };
        }
    }

    /// <summary>
    /// The DOTS data structure for the UntilSuccess class.
    /// </summary>
    public struct UntilSuccessComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
    }

    /// <summary>
    /// A DOTS tag indicating when an UntilSuccess node is active.
    /// </summary>
    public struct UntilSuccessFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the UntilSuccess logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct UntilSuccessTaskSystem : ISystem
    {
        private EntityQuery m_Query;

        /// <summary>
        /// Builds the query.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnCreate(ref SystemState state)
        {
            m_Query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<UntilSuccessComponent>().WithAll<UntilSuccessFlag, EvaluateFlag>().Build();
        }

        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            state.Dependency = new UntilSuccessJob().ScheduleParallel(m_Query, state.Dependency);
        }

        /// <summary>
        /// Job which executes the task logic.
        /// </summary>
        [BurstCompile]
        private partial struct UntilSuccessJob : IJobEntity
        {
            /// <summary>
            /// Executes the until success logic.
            /// </summary>
            /// <param name="branchComponents">An array of BranchComponents.</param>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="untilSuccessComponents">An array of UntilSuccessComponents.</param>
            [BurstCompile]
            public void Execute(ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<UntilSuccessComponent> untilSuccessComponents)
            {
                for (int i = 0; i < untilSuccessComponents.Length; ++i) {
                    var untilSuccessComponent = untilSuccessComponents[i];
                    var taskComponent = taskComponents[untilSuccessComponent.Index];
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

                    // The until success task is currently active. Check the first child.
                    childTaskComponent = taskComponents[childIndex];
                    if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running ) {
                        // The child should keep running.
                        continue;
                    }

                    // If the child returns failure then it should be queued again.
                    if (childTaskComponent.Status == TaskStatus.Failure) {
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

                    // The child has returned success. The task can end.
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