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
    using Unity.Entities;
    using Unity.Transforms;
    using UnityEngine;
    using System;

    [Tooltip("Fires any entity that has the HealthComponent.")]
    [Shared.Utility.Category("Behavior Designer Samples/DOTS")]
    public class Fire : ECSActionTask<FireTaskSystem, FireComponent>
    {
        /// <summary>
        /// The type of flag that should be enabled when the task is running.
        /// </summary>
        public override ComponentType Flag { get => typeof(FireFlag); }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override FireComponent GetBufferElement()
        {
            return new FireComponent()
            {
                Index = RuntimeIndex,
            };
        }
    }

    /// <summary>
    /// The DOTS data structure for the Fire struct.
    /// </summary>
    public struct FireComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
    }

    /// <summary>
    /// A DOTS flag indicating when a Fire node is active.
    /// </summary>
    public struct FireFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the Fire logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct FireTaskSystem : ISystem
    {
        /// <summary>
        /// Updates the logic.
        /// </summary>
        /// <param name="state">The current SystemState.</param>
        private void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            foreach (var (taskComponents, fireComponents, entity) in
                SystemAPI.Query<DynamicBuffer<TaskComponent>, DynamicBuffer<FireComponent>>().WithAll<FireFlag, EvaluateFlag>().WithEntityAccess()) {
                for (int i = 0; i < fireComponents.Length; ++i) {
                    var fireComponent = fireComponents[i];
                    var taskComponent = taskComponents[fireComponent.Index];
                    if (taskComponent.Status != TaskStatus.Queued) {
                        continue;
                    }

                    // Find the target. There will only be one entity with the TargetEntityTag.
                    foreach (var (target, localTransform, targetEntity) in SystemAPI.Query<RefRO<TargetEntityTag>, RefRO<LocalTransform>>().WithEntityAccess()) {
                        ecb.AddComponent<DestroyEntityTag>(targetEntity);
                        break;
                    }

                    // The task will always return immediately.
                    taskComponent.Status = TaskStatus.Success;
                    var taskComponentBuffer = taskComponents;
                    taskComponentBuffer[fireComponent.Index] = taskComponent;

                    // The turret has fired - apply a recoil.
                    foreach (var (target, turretEntity) in SystemAPI.Query<RefRO<TurretRecoil>>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess()) {
                        ecb.SetComponentEnabled<TurretRecoil>(turretEntity, true);
                        break;
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}