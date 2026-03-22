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
    public class ReturnSuccess : ECSDecoratorTask<ReturnSuccessTaskSystem, ReturnSuccessComponent>, IParentNode
    {
        public override ComponentType Flag { get => typeof(ReturnSuccessFlag); }

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
        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<ReturnSuccessComponent>().WithAll<ReturnSuccessFlag, EvaluateFlag>().Build();
            state.Dependency = new ReturnSuccessJob().ScheduleParallel(query, state.Dependency);
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

                    // The return success task is currently active. Check the first child.
                    childTaskComponent = taskComponents[taskComponent.Index + 1];
                    if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running) {
                        // The child should keep running.
                        continue;
                    }

                    // The child has completed. Return success.
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