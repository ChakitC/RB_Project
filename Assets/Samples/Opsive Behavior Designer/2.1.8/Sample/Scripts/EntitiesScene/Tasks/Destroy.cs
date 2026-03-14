/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Samples
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.GraphDesigner.Runtime;
    using Unity.Burst;
    using Unity.Entities;
    using Unity.Collections;
    using Unity.Transforms;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Destroys the Entity.")]
    [Shared.Utility.Category("Behavior Designer Samples/DOTS")]
    public class Destroy : ECSActionTask<DestroyTaskSystem, DestroyComponent>
    {
        /// <summary>
        /// The type of flag that should be enabled when the task is running.
        /// </summary>
        public override ComponentType Flag { get => typeof(DestroyFlag); }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override DestroyComponent GetBufferElement()
        {
            return new DestroyComponent()
            {
                Index = RuntimeIndex,
            };
        }
    }

    /// <summary>
    /// The DOTS data structure for the Destroy struct.
    /// </summary>
    public struct DestroyComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
    }

    /// <summary>
    /// A DOTS flag indicating when a Destroy node is active.
    /// </summary>
    public struct DestroyFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the Destroy logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct DestroyTaskSystem : ISystem
    {
        private EntityQuery m_DestroyQuery;

        /// <summary>
        /// Creates the required objects for use within the job system.
        /// </summary>
        /// <param name="state">The current SystemState.</param>
        [BurstCompile]
        private void OnCreate(ref SystemState state)
        {
            m_DestroyQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<TaskComponent>().WithAllRW<LocalTransform>()
                .WithAll<DestroyComponent, EvaluateFlag>()
                .Build(ref state);
        }

        /// <summary>
        /// Updates the logic.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            foreach (var (destroyComponents, taskComponents, entity) in
                SystemAPI.Query<DynamicBuffer<DestroyComponent>, DynamicBuffer<TaskComponent>>().WithAll<DestroyFlag, EvaluateFlag>().WithEntityAccess()) {
                for (int i = 0; i < destroyComponents.Length; ++i) {
                    var destroyComponent = destroyComponents[i];
                    var taskComponent = taskComponents[destroyComponent.Index];
                    if (taskComponent.Status != TaskStatus.Queued) {
                        continue;
                    }

                    // The DestroyEntityTag will destroy the entity.
                    ecb.AddComponent<DestroyEntityTag>(entity);

                    // The task always returns success.
                    taskComponent.Status = TaskStatus.Success;
                    var taskComponentBuffer = taskComponents;
                    taskComponentBuffer[destroyComponent.Index] = taskComponent;
                }
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}