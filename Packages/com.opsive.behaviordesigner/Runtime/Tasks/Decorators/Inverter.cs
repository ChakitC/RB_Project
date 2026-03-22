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
    public class Inverter : ECSDecoratorTask<InverterTaskSystem, InverterComponent>, IParentNode
    {
        public override ComponentType Flag { get => typeof(InverterFlag); }

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
        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<InverterComponent>().WithAll<InverterFlag, EvaluateFlag>().Build();
            state.Dependency = new InverterJob().ScheduleParallel(query, state.Dependency);
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

                    // The inverter task is currently active. Check the first child.
                    childTaskComponent = taskComponents[taskComponent.Index + 1];
                    if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running) {
                        // The child should keep running.
                        continue;
                    }

                    // The child has completed. Invert the status.
                    taskComponent.Status = childTaskComponent.Status == TaskStatus.Success ? TaskStatus.Failure : TaskStatus.Success;
                    taskComponents[taskComponent.Index] = taskComponent;

                    branchComponent.NextIndex = taskComponent.ParentIndex;
                    branchComponents[taskComponent.BranchIndex] = branchComponent;
                }
            }
        }
    }
}
#endif