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
    /// A node representation of the inverter task.
    /// </summary>
    [NodeIcon("53fe4de81c20e924095bdb5f3447acdc", "8d991ea2b725c214c85580d5647c578c")]
    [Opsive.Shared.Utility.Description("The inverter task will invert the return value of the child task after it has finished executing. " +
                     "If the child returns success, the inverter task will return failure. If the child returns failure, the inverter task will return success.")]
    public class Inverter : ECSDecoratorTask<InverterTaskSystem, InverterComponent, InverterFlag>, IParentNode
    {
        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override InverterComponent GetBufferElement()
        {
            return new InverterComponent()
            {
                Index = RuntimeIndex,
            };
        }
    }

    /// <summary>
    /// The DOTS data structure for the Inverter class.
    /// </summary>
    public struct InverterComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
    }

    /// <summary>
    /// A DOTS tag indicating when an Inverter node is active.
    /// </summary>
    public struct InverterFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the Inverter logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct InverterTaskSystem : ISystem
    {
        private EntityQuery m_Query;

        /// <summary>
        /// Builds the query.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnCreate(ref SystemState state)
        {
            m_Query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<InverterComponent>().WithAll<InverterFlag, EvaluateFlag>().Build();
        }

        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            state.Dependency = new InverterJob().ScheduleParallel(m_Query, state.Dependency);
        }

        /// <summary>
        /// Job which executes the task logic.
        /// </summary>
        [BurstCompile]
        private partial struct InverterJob : IJobEntity
        {
            /// <summary>
            /// Executes the inverter logic.
            /// </summary>
            /// <param name="branchComponents">An array of BranchComponents.</param>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="inverterComponents">An array of InverterComponents.</param>
            [BurstCompile]
            public void Execute(ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<InverterComponent> inverterComponents)
            {
                for (int i = 0; i < inverterComponents.Length; ++i) {
                    var inverterComponent = inverterComponents[i];
                    var taskComponent = taskComponents[inverterComponent.Index];
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

                    // The inverter task is currently active. Check the first child.
                    childTaskComponent = taskComponents[childIndex];
                    if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running) {
                        // The child should keep running.
                        continue;
                    }

                    // The child has completed. Invert the status.
                    var status = childTaskComponent.Status == TaskStatus.Success ? TaskStatus.Failure : TaskStatus.Success;
                    if (taskComponent.Status != status) {
                        taskComponent.Status = status;
                        taskComponents[taskComponent.Index] = taskComponent;
                    }

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