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
    using System;

    /// <summary>
    /// A node representation of the return status task.
    /// </summary>
    [Opsive.Shared.Utility.Description("The return status task will immediately return sucess or failure.")]
    public class ReturnStatus : ECSActionTask<ReturnStatusTaskSystem, ReturnStatusComponent>, ICloneable
    {
        [Tooltip("Should a success status be returned? If false then failure will be returned.")]
        [SerializeField] bool m_Success;

        public bool Success { get => m_Success; set => m_Success = value; }

        /// <summary>
        /// The type of tag that should be enabled when the task is running.
        /// </summary>
        public override ComponentType Flag { get => typeof(ReturnStatusFlag); }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override ReturnStatusComponent GetBufferElement()
        {
            return new ReturnStatusComponent() {
                Index = RuntimeIndex,
                Success = m_Success
            };
        }

        /// <summary>
        /// Creates a deep clone of the component.
        /// </summary>
        /// <returns>A deep clone of the component.</returns>
        public object Clone()
        {
            var clone = Activator.CreateInstance<ReturnStatus>();
            clone.Index = Index;
            clone.ParentIndex = ParentIndex;
            clone.SiblingIndex = SiblingIndex;
            clone.Success = Success;
            return clone;
        }
    }

    /// <summary>
    /// The DOTS data structure for the ReturnStatus class.
    /// </summary>
    public struct ReturnStatusComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        [SerializeField] ushort m_Index;
        [Tooltip("Should a success status be returned? If false then failure will be returned.")]
        [SerializeField] bool m_Success;
        public ushort Index { get => m_Index; set => m_Index = value; }
        public bool Success { get => m_Success; set => m_Success = value; }
    }

    /// <summary>
    /// A DOTS tag indicating when a ReturnStatus node is active.
    /// </summary>
    public struct ReturnStatusFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the ReturnStatus logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct ReturnStatusTaskSystem : ISystem
    {
        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder().WithAllRW<TaskComponent>().WithAllRW<ReturnStatusComponent>().WithAll<ReturnStatusFlag, EvaluateFlag>().Build();
            state.Dependency = new ReturnStatusJob().ScheduleParallel(query, state.Dependency);
        }

        /// <summary>
        /// Job which executes the task logic.
        /// </summary>
        [BurstCompile]
        private partial struct ReturnStatusJob : IJobEntity
        {
            /// <summary>
            /// Executes the return status logic.
            /// </summary>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="returnStatusComponents">An array of ReturnStatusComponents.</param>
            [BurstCompile]
            public void Execute(ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<ReturnStatusComponent> returnStatusComponents)
            {
                for (int i = 0; i < returnStatusComponents.Length; ++i) {
                    var returnStatusComponent = returnStatusComponents[i];
                    var taskComponent = taskComponents[returnStatusComponent.Index];
                    if (taskComponent.Status != TaskStatus.Queued) {
                        continue;
                    }
                    taskComponent.Status = returnStatusComponent.Success ? TaskStatus.Success : TaskStatus.Failure;
                    taskComponents[returnStatusComponent.Index] = taskComponent;
                }
            }
        }
    }
}
#endif