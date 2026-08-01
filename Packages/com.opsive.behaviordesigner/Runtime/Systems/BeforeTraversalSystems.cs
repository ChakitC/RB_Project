#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Systems
{
    using Opsive.BehaviorDesigner.Runtime;
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.BehaviorDesigner.Runtime.Groups;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// System which checks for any tasks that should be reevaluated with conditional aborts. This system only marks the tasks, it does not do
    /// the actual reevaluation or interruption.
    /// </summary>
    [UpdateInGroup(typeof(BeforeTraversalSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(ReevaluateTaskSystemGroup))]
    [DisableAutoCreation]
    public partial struct ReevaluateSystem : ISystem
    {
        private EntityQuery m_Query;

        /// <summary>
        /// Builds the query.
        /// </summary>
        /// <param name="state">THe current SystemState.</param>
        private void OnCreate(ref SystemState state)
        {
            m_Query = SystemAPI.QueryBuilder().WithAllRW<TaskComponent>().WithAllRW<ReevaluateTaskComponent>().WithAll<BranchComponent>().WithAbsent<BakedBehaviorTree>().Build();
        }

        /// <summary>
        /// Marks tasks that should be reevaluated with conditional aborts.
        /// </summary>
        /// <param name="state">The current SystemState.</param>
        private void OnUpdate(ref SystemState state)
        {
            if (m_Query.IsEmptyIgnoreFilter) {
                return;
            }

            foreach (var (branchComponents, taskComponents, reevaluateTaskComponents, entity) in
                SystemAPI.Query<DynamicBuffer<BranchComponent>, DynamicBuffer<TaskComponent>, DynamicBuffer<ReevaluateTaskComponent>>().WithAbsent<BakedBehaviorTree>().WithEntityAccess()) {

                var branchComponentsBuffer = branchComponents;
                var taskComponentsBuffer = taskComponents;
                var reevaluateTaskComponentsBuffer = reevaluateTaskComponents;
                BeforeTraversalCore.Reevaluate(state.EntityManager, entity, ref branchComponentsBuffer, ref taskComponentsBuffer, ref reevaluateTaskComponentsBuffer);
            }
        }
    }

    /// <summary>
    /// The tasks have been reevaluated. Compare the status to determine if an interrupt should occur.
    /// </summary>
    [UpdateInGroup(typeof(InterruptSystemGroup))]
    [UpdateBefore(typeof(InterruptSystem))]
    public partial struct ConditionalAbortsInvokerSystem : ISystem
    {
        private EntityQuery m_Query;

        /// <summary>
        /// Builds the query.
        /// </summary>
        /// <param name="state">THe current SystemState.</param>
        private void OnCreate(ref SystemState state)
        {
            m_Query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<ReevaluateTaskComponent>().WithAbsent<BakedBehaviorTree>().Build();
        }

        /// <summary>
        /// Creates the jobs necessary for conditional aborts.
        /// </summary>
        /// <param name="state">The current SystemState.</param>
        #if !UNITY_EDITOR
        [BurstCompile]
        #endif
        private void OnUpdate(ref SystemState state)
        {
            if (m_Query.IsEmptyIgnoreFilter) {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            state.Dependency = new ConditionalAbortsJob()
            {
                EntityCommandBuffer = ecb.AsParallelWriter()
            }.ScheduleParallel(m_Query, state.Dependency);

            // The jobs must be run immediately for the next systems.
            state.Dependency.Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// Job which checks for any tasks that should be reevaluated with conditional aborts. This job only flags the tasks, it does not do
        /// the actual reevaluation or interruption.
        /// </summary>
        #if !UNITY_EDITOR
        [BurstCompile]
        #endif
        private partial struct ConditionalAbortsJob : IJobEntity
        {
            [Tooltip("CommandBuffer which sets the component data.")]
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            /// <summary>
            /// Executes the job.
            /// </summary>
            /// <param name="entity">The entity that is being acted upon.</param>
            /// <param name="entityIndex">The index of the entity.</param>
            /// <param name="branchComponents">An array of branch components.</param>
            /// <param name="taskComponents">An array of task components.</param>
            /// <param name="reevaluateTaskComponents">An array of reevaluate task components.</param>
            #if !UNITY_EDITOR
            [BurstCompile]
            #endif
            public void Execute(Entity entity, [EntityIndexInQuery] int entityIndex, ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<ReevaluateTaskComponent> reevaluateTaskComponents)
            {
                BeforeTraversalCore.InvokeConditionalAborts(entity, entityIndex, ref branchComponents, ref taskComponents, ref reevaluateTaskComponents, EntityCommandBuffer);
            }
        }
    }

    /// <summary>
    /// Processes any interrupts.
    /// </summary>
    [UpdateInGroup(typeof(InterruptSystemGroup))]
    [UpdateAfter(typeof(ConditionalAbortsInvokerSystem))]
    [UpdateBefore(typeof(InterruptTaskSystemGroup))]
    public partial struct InterruptSystem : ISystem
    {
        private EntityQuery m_Query;

        /// <summary>
        /// Builds the query.
        /// </summary>
        /// <param name="state">THe current SystemState.</param>
        private void OnCreate(ref SystemState state)
        {
            m_Query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAll<InterruptFlag>().Build();
        }

        /// <summary>
        /// Creates the InterruptJob.
        /// </summary>
        /// <param name="state">The current SystemState.</param>
        #if !UNITY_EDITOR
        [BurstCompile]
        #endif
        private void OnUpdate(ref SystemState state)
        {
            // IsEmpty instead of IsEmptyIgnoreFilter so the enableable InterruptFlag is respected. Interrupts are rare
            // so this prevents the job and sync point on the frames without an interrupt.
            if (m_Query.IsEmpty) {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            state.Dependency = new InterruptJob()
            {
                EntityCommandBuffer = ecb.AsParallelWriter(),
            }.ScheduleParallel(m_Query, state.Dependency);

            // The job must run immediately for the next systems.
            state.Dependency.Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// Triggers the interrupts.
        /// </summary>
        #if !UNITY_EDITOR
        [BurstCompile]
        #endif
        private partial struct InterruptJob : IJobEntity
        {
            [Tooltip("CommandBuffer which sets the component data.")]
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            /// <summary>
            /// Executes the job.
            /// </summary>
            /// <param name="entity">The entity that is being acted upon.</param>
            /// <param name="entityIndex">The index of the entity.</param>
            /// <param name="branchComponents">An array of branch components.</param>
            /// <param name="taskComponents">An array of task components.</param>
            #if !UNITY_EDITOR
            [BurstCompile]
            #endif
            public void Execute(Entity entity, [EntityIndexInQuery] int entityIndex, DynamicBuffer<BranchComponent> branchComponents, DynamicBuffer<TaskComponent> taskComponents)
            {
                BeforeTraversalCore.Interrupt(entity, entityIndex, branchComponents, taskComponents, EntityCommandBuffer);
            }
        }
    }

    /// <summary>
    /// Cleanup the interrupts after they have run.
    /// </summary>
    [UpdateInGroup(typeof(InterruptSystemGroup), OrderLast = true)]
    public partial struct InterruptCleanupSystem : ISystem
    {
        /// <summary>
        /// Executes the system.
        /// </summary>
        /// <param name="state">The current SystemState.</param>
        #if !UNITY_EDITOR
        [BurstCompile]
        #endif
        private void OnUpdate(ref SystemState state)
        {
            foreach (var (branchComponents, entity) in
                SystemAPI.Query<DynamicBuffer<BranchComponent>>().WithAll<InterruptFlag>().WithEntityAccess()) {
                var branchComponentBuffer = branchComponents;
                BeforeTraversalCore.CleanupInterrupts(state.EntityManager, entity, ref branchComponentBuffer);
            }
        }
    }
}
#endif