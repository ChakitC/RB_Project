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
    using System;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Uses DOTS to determine if the entity has a target.")]
    [Shared.Utility.Category("Behavior Designer Samples/DOTS")]
    public class FindTarget : ECSActionTask<FindTargetTaskSystem, FindTargetComponent, FindTargetFlag>
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
        public override FindTargetComponent GetBufferElement()
        {
            return new FindTargetComponent()
            {
                Index = RuntimeIndex,
                TargetEntityVariableIndex = m_TargetEntityIndex.Index,
            };
        }
    }

    /// <summary>
    /// The DOTS data structure for the FindTarget struct.
    /// </summary>
    public struct FindTargetComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
        [Tooltip("Buffer index into SharedVariableElement for the target entity.")]
        public int TargetEntityVariableIndex;
    }

    /// <summary>
    /// A DOTS flag indicating when a FindTarget node is active.
    /// </summary>
    public struct FindTargetFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the FindTarget logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct FindTargetTaskSystem : ISystem
    {
        Unity.Mathematics.Random randomGenerator;

        /// <summary>
        /// The system has been created.
        /// </summary>
        /// <param name="state">The current SystemState.</param>
        private void OnCreate(ref SystemState state)
        {
            randomGenerator = new Unity.Mathematics.Random((uint)DateTime.Now.Ticks);
        }

        /// <summary>
        /// Updates the logic.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            foreach (var (branchComponents, taskComponents, findTargetComponents, sharedVariables) in
                SystemAPI.Query<DynamicBuffer<BranchComponent>, DynamicBuffer<TaskComponent>, DynamicBuffer<FindTargetComponent>, DynamicBuffer<SharedVariableElement>>().WithAll<FindTargetFlag, EvaluateFlag>()) {
                for (int i = 0; i < findTargetComponents.Length; ++i) {
                    var fndTargetComponent = findTargetComponents[i];
                    var taskComponent = taskComponents[fndTargetComponent.Index];
                    var branchComponent = branchComponents[taskComponent.BranchIndex];
                    if (!branchComponent.CanExecute) {
                        continue;
                    }

                    if (taskComponent.Status != TaskStatus.Queued) {
                        continue;
                    }

                    var index = -1;
                    var count = 0;
                    var targetEntity = Entity.Null;
                    var entities = state.EntityManager.GetAllEntities(Allocator.Temp);
                    var foundAgent = false;
                    if (entities.Length > 0) {
                        do {
                            index = randomGenerator.NextInt(entities.Length);
                            count++;
                        } while (count < entities.Length * 2 && !(foundAgent = state.EntityManager.HasComponent<AgentTag>(entities[index])));
                    }

                    // Store the found target in the shared variable buffer.
                    if (foundAgent) {
                        targetEntity = entities[index];
                    }
                    sharedVariables.Set(fndTargetComponent.TargetEntityVariableIndex, targetEntity);
                    entities.Dispose();

                    // The task is complete, return to the parent.
                    taskComponent.Status = foundAgent ? TaskStatus.Success : TaskStatus.Failure;
                    var taskComponentBuffer = taskComponents;
                    taskComponentBuffer[fndTargetComponent.Index] = taskComponent;
                }
            }
        }
    }
}