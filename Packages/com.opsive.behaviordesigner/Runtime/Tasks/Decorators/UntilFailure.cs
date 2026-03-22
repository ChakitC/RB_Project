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
    public class UntilFailure : ECSDecoratorTask<UntilFailureTaskSystem, UntilFailureComponent>, IParentNode
    {
        public override ComponentType Flag { get => typeof(UntilFailureFlag); }

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
        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<UntilFailureComponent>().WithAll<UntilFailureFlag, EvaluateFlag>().Build();
            state.Dependency = new UntilFailureJob().ScheduleParallel(query, state.Dependency);
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
                    var branchComponent = branchComponents[taskComponent.BranchIndex];
                    if (!branchComponent.CanExecute) {
                        continue;
                    }

                    TaskComponent childTaskComponent;
                    if (taskComponent.Status == TaskStatus.Queued) {
                        taskComponent.Status = TaskStatus.Running;
                        taskComponents[taskComponent.Index] = taskComponent;

                        childTaskComponent = taskComponents[taskComponent.Index + 1];
                        childTaskComponent.Status = TaskStatus.Queued;
                        taskComponents[taskComponent.Index + 1] = childTaskComponent;

                        branchComponent.NextIndex = (ushort)(taskComponent.Index + 1);
                        branchComponents[taskComponent.BranchIndex] = branchComponent;
                        continue;
                    } else if (taskComponent.Status != TaskStatus.Running) {
                        continue;
                    }

                    // The until failure task is currently active. Check the first child.
                    childTaskComponent = taskComponents[taskComponent.Index + 1];
                    if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running) {
                        // The child should keep running.
                        continue;
                    }

                    // If the child returns success then it should be queued again.
                    if (childTaskComponent.Status == TaskStatus.Success) {
                        childTaskComponent.Status = TaskStatus.Queued;
                        taskComponents[taskComponent.Index + 1] = childTaskComponent;

                        branchComponent.NextIndex = (ushort)(taskComponent.Index + 1);
                        branchComponents[taskComponent.BranchIndex] = branchComponent;
                        continue;
                    }

                    // The child has returned failure.
                    taskComponent.Status = TaskStatus.Failure;
                    taskComponents[taskComponent.Index] = taskComponent;

                    branchComponent.NextIndex = taskComponent.ParentIndex;
                    branchComponents[taskComponent.BranchIndex] = branchComponent;
                }
            }
        }
    }
}
#endif