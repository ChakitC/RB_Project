#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.GraphDesigner.Runtime;
    using Unity.Entities;
    using Unity.Burst;
    using UnityEngine;

    /// <summary>
    /// A node representation of the idle task.
    /// </summary>
    [NodeIcon("fc4d1b83384913b4abfbd8455db6df5b", "79a6985a753bb244fb5b32dc0f26addb")]
    [Opsive.Shared.Utility.Description("Returns a TaskStatus of running. The task will only stop when interrupted or a conditional abort is triggered.")]
    public class Idle : ECSActionTask<IdleTaskSystem, IdleComponent>
    {
        /// <summary>
        /// The type of tag that should be enabled when the task is running.
        /// </summary>
        public override ComponentType Flag { get => typeof(IdleFlag); }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override IdleComponent GetBufferElement()
        {
            return new IdleComponent() {
                Index = RuntimeIndex
            };
        }
    }

    /// <summary>
    /// The DOTS data structure for the Idle class.
    /// </summary>
    public struct IdleComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
    }

    /// <summary>
    /// A DOTS tag indicating when a Idle node is active.
    /// </summary>
    public struct IdleFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the Idle logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct IdleTaskSystem : ISystem
    {
        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder().WithAllRW<TaskComponent>().WithAll<IdleComponent, IdleFlag, EvaluateFlag>().Build();
            state.Dependency = new IdleJob().ScheduleParallel(query, state.Dependency);
        }

        /// <summary>
        /// Job which executes the task logic.
        /// </summary>
        [BurstCompile]
        private partial struct IdleJob : IJobEntity
        {
            /// <summary>
            /// Executes the idle logic.
            /// </summary>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="idleComponents">An array of IdleComponents.</param>
            [BurstCompile]
            public void Execute(ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<IdleComponent> idleComponents)
            {
                for (int i = 0; i < idleComponents.Length; ++i) {
                    var idleComponent = idleComponents[i];
                    var taskComponent = taskComponents[idleComponent.Index];
                    if (taskComponent.Status == TaskStatus.Queued) {
                        taskComponent.Status = TaskStatus.Running;
                        taskComponents[idleComponent.Index] = taskComponent;
                    }
                }
            }
        }
    }
}
#endif