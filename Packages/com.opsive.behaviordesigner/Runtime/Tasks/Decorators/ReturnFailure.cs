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
    /// A node representation of the return failure task.
    /// </summary>
    [NodeIcon("667a475ceee05824188a36b24ec8d392", "7d32c9b05505df24a94069606f3b823d")]
    [Opsive.Shared.Utility.Description("The return failure task will always return failure except when the child task is running.")]
    public class ReturnFailure : ECSDecoratorTask<ReturnFailureTaskSystem, ReturnFailureComponent, ReturnFailureFlag>, IParentNode
    {
        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override ReturnFailureComponent GetBufferElement()
        {
            return new ReturnFailureComponent()
            {
                Index = RuntimeIndex,
            };
        }
    }

    /// <summary>
    /// The DOTS data structure for the ReturnFailure class.
    /// </summary>
    public struct ReturnFailureComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
    }

    /// <summary>
    /// A DOTS tag indicating when an ReturnFailure node is active.
    /// </summary>
    public struct ReturnFailureFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the ReturnFailure logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct ReturnFailureTaskSystem : ISystem
    {
        private EntityQuery m_Query;

        /// <summary>
        /// Builds the query.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnCreate(ref SystemState state)
        {
            m_Query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<ReturnFailureComponent>().WithAll<ReturnFailureFlag, EvaluateFlag>().Build();
        }

        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ReturnFailureJob().ScheduleParallel(m_Query, state.Dependency);
        }

        /// <summary>
        /// Job which executes the task logic.
        /// </summary>
        [BurstCompile]
        private partial struct ReturnFailureJob : IJobEntity
        {
            /// <summary>
            /// Executes the return failure logic.
            /// </summary>
            /// <param name="branchComponents">An array of BranchComponents.</param>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="returnFailureComponents">An array of ReturnFailureComponents.</param>
            [BurstCompile]
            public void Execute(ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<ReturnFailureComponent> returnFailureComponents)
            {
                for (int i = 0; i < returnFailureComponents.Length; ++i) {
                    var returnFailureComponent = returnFailureComponents[i];
                    var taskComponent = taskComponents[returnFailureComponent.Index];
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

                    // The return failure task is currently active. Check the first child.
                    childTaskComponent = taskComponents[childIndex];
                    if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running) {
                        // The child should keep running.
                        continue;
                    }

                    // The child has completed. Return failure.
                    taskComponent.Status = TaskStatus.Failure;
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