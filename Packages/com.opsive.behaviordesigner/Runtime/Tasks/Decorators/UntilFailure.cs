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
    /// A node representation of the until failure task.
    /// </summary>
    [NodeIcon("60da350fd1f5b48428e466b79cb85cb2", "3d29cc3223984f44291c0e423a0aa6c6")]
    [Opsive.Shared.Utility.Description("The until failure task will keep executing its child task until the child task returns failure.")]
    public class UntilFailure : ECSDecoratorTask<UntilFailureTaskSystem, UntilFailureComponent, UntilFailureFlag>, IParentNode
    {
        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override UntilFailureComponent GetBufferElement()
        {
            return new UntilFailureComponent()
            {
                Index = RuntimeIndex,
            };
        }
    }

    /// <summary>
    /// The DOTS data structure for the UntilFailure class.
    /// </summary>
    public struct UntilFailureComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
    }

    /// <summary>
    /// A DOTS tag indicating when an UntilFailure node is active.
    /// </summary>
    public struct UntilFailureFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the UntilFailure logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct UntilFailureTaskSystem : ISystem
    {
        private EntityQuery m_Query;

        /// <summary>
        /// Builds the query.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnCreate(ref SystemState state)
        {
            m_Query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<UntilFailureComponent>().WithAll<UntilFailureFlag, EvaluateFlag>().Build();
        }

        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            state.Dependency = new UntilFailureJob().ScheduleParallel(m_Query, state.Dependency);
        }

        /// <summary>
        /// Job which executes the task logic.
        /// </summary>
        [BurstCompile]
        private partial struct UntilFailureJob : IJobEntity
        {
            /// <summary>
            /// Executes the until failure logic.
            /// </summary>
            /// <param name="branchComponents">An array of BranchComponents.</param>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="untilFailureComponents">An array of UntilFailureComponents.</param>
            [BurstCompile]
            public void Execute(ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<UntilFailureComponent> untilFailureComponents)
            {
                for (int i = 0; i < untilFailureComponents.Length; ++i) {
                    var untilFailureComponent = untilFailureComponents[i];
                    var taskComponent = taskComponents[untilFailureComponent.Index];
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

                    // The until failure task is currently active. Check the first child.
                    childTaskComponent = taskComponents[childIndex];
                    if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running) {
                        // The child should keep running.
                        continue;
                    }

                    // If the child returns success then it should be queued again.
                    if (childTaskComponent.Status == TaskStatus.Success) {
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

                    // The child has returned failure.
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