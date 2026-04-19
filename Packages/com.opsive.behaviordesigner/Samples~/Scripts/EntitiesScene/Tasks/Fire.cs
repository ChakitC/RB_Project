/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Samples
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.GraphDesigner.Runtime.Variables.ECS;
    using Unity.Entities;
    using UnityEngine;

    [Tooltip("Fires any entity that has the HealthComponent.")]
    [Shared.Utility.Category("Behavior Designer Samples/DOTS")]
    public class Fire : ECSActionTask<FireTaskSystem, FireComponent, FireFlag>
    {
        [Tooltip("The entity that should be targeted.")]
        [SerializeField] [RequireShared] SharedVariable<Entity> m_TargetEntity;

        private ECSSharedVariableIndex<Entity> m_TargetEntityIndex;

        /// <summary>
        /// Registers the target SharedVariable and adds the buffer element to the entity.
        /// </summary>
        /// <param name="world">The world that the entity exists in.</param>
        /// <param name="entity">The entity that the IBufferElementData should be assigned to.</param>
        /// <param name="registry">The ECS variable registry for registering SharedVariable fields.</param>
        /// <param name="gameObject">The GameObject that the entity is attached to.</param>
        /// <returns>The index of the element within the buffer.</returns>
        public override int AddBufferElement(World world, Entity entity, ECSVariableRegistry registry, GameObject gameObject)
        {
            m_TargetEntityIndex = new ECSSharedVariableIndex<Entity>(registry.Register(m_TargetEntity));
            return base.AddBufferElement(world, entity, registry, gameObject);
        }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override FireComponent GetBufferElement()
        {
            return new FireComponent()
            {
                Index = RuntimeIndex,
                TargetEntityVariableIndex = m_TargetEntityIndex.Index,
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
        [Tooltip("Buffer index into SharedVariableElement for the target entity.")]
        public int TargetEntityVariableIndex;
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
            foreach (var (branchComponents, taskComponents, fireComponents, sharedVariables) in
                SystemAPI.Query<DynamicBuffer<BranchComponent>, DynamicBuffer<TaskComponent>, DynamicBuffer<FireComponent>, DynamicBuffer<SharedVariableElement>>().WithAll<FireFlag, EvaluateFlag>()) {
                for (int i = 0; i < fireComponents.Length; ++i) {
                    var fireComponent = fireComponents[i];
                    var taskComponent = taskComponents[fireComponent.Index];
                    var branchComponent = branchComponents[taskComponent.BranchIndex];
                    if (!branchComponent.CanExecute) {
                        continue;
                    }

                    if (taskComponent.Status != TaskStatus.Queued) {
                        continue;
                    }

                    var targetEntity = sharedVariables.Get<Entity>(fireComponent.TargetEntityVariableIndex);
                    if (targetEntity != Entity.Null && state.EntityManager.Exists(targetEntity)) {
                        ecb.AddComponent<DestroyEntityTag>(targetEntity);
                    }

                    // The task will always return immediately.
                    taskComponent.Status = TaskStatus.Success;
                    var taskComponentBuffer = taskComponents;
                    taskComponentBuffer[fireComponent.Index] = taskComponent;

                    // The turret has fired - apply a recoil.
                    foreach (var (_, turretEntity) in SystemAPI.Query<RefRO<TurretRecoil>>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess()) {
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