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
    using UnityEngine;
    using System;

    [Tooltip("Damages any entity that has the HealthComponent.")]
    [Shared.Utility.Category("Behavior Designer Samples/DOTS")]
    public class Damage : ECSActionTask<DamageTaskSystem, DamageComponent>
    {
        [Tooltip("The amount of damage to apply.")]
        [SerializeField] float m_DamageAmount;

        /// <summary>
        /// The type of flag that should be enabled when the task is running.
        /// </summary>
        public override ComponentType Flag { get => typeof(DamageFlag); }

        /// <summary>
        /// Resets the task to its default values.
        /// </summary>
        public override void Reset() { m_DamageAmount = 1; }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override DamageComponent GetBufferElement()
        {
            return new DamageComponent()
            {
                Index = RuntimeIndex,
                DamageAmount = m_DamageAmount,
            };
        }
    }

    /// <summary>
    /// The DOTS data structure for the Damage struct.
    /// </summary>
    public struct DamageComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
        [Tooltip("The amount of damage to apply.")]
        public float DamageAmount;
    }

    /// <summary>
    /// A DOTS flag indicating when a Fire node is active.
    /// </summary>
    public struct DamageFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the Damage logic.
    /// </summary>
    [DisableAutoCreation]
    public partial class DamageTaskSystem : SystemBase
    {
        public Action<float> OnDamage;

        /// <summary>
        /// Updates the logic.
        /// </summary>
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(WorldUpdateAllocator);
            foreach (var (taskComponents, damageComponents) in
                SystemAPI.Query<DynamicBuffer<TaskComponent>, DynamicBuffer<DamageComponent>>().WithAll<DamageFlag, EvaluateFlag>()) {
                for (int i = 0; i < damageComponents.Length; ++i) {
                    var damageComponent = damageComponents[i];
                    var taskComponent = taskComponents[damageComponent.Index];
                    if (taskComponent.Status != TaskStatus.Queued) {
                        continue;
                    }

                    // Apply the damage.
                    foreach (var (healthComponent, entity) in SystemAPI.Query<RefRW<HealthComponent>>().WithEntityAccess()) {
                        healthComponent.ValueRW.Value -= damageComponent.DamageAmount;

                        if (healthComponent.ValueRO.Value <= 0) {
                            ecb.AddComponent<DestroyEntityTag>(entity);
                        }
                        OnDamage?.Invoke(healthComponent.ValueRO.Value);
                    }

                    // The task will always return immediately.
                    taskComponent.Status = TaskStatus.Success;
                    var taskComponentBuffer = taskComponents;
                    taskComponentBuffer[damageComponent.Index] = taskComponent;
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}