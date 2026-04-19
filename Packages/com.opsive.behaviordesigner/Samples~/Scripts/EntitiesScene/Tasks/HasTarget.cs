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
    using Unity.Burst;
    using Unity.Entities;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Uses DOTS to determine if the entity has a target.")]
    [Shared.Utility.Category("Behavior Designer Samples/DOTS")]
    public class HasTarget : ECSConditionalTask<HasTargetTaskSystem, HasTargetComponent, HasTargetFlag>, IReevaluateResponder
    {
        [Tooltip("The entity that should be targeted.")]
        [SerializeField] [RequireShared] SharedVariable<Entity> m_TargetEntity;

        private ECSSharedVariableIndex<Entity> m_TargetEntityIndex;

        public ComponentType ReevaluateFlag { get => typeof(HasTargetReevaluateFlag); }
        public System.Type ReevaluateSystemType { get => typeof(HasTargetReevaluateTaskSystem); }

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
        public override HasTargetComponent GetBufferElement()
        {
            return new HasTargetComponent()
            {
                Index = RuntimeIndex,
                TargetEntityVariableIndex = m_TargetEntityIndex.Index,
            };
        }
    }

    /// <summary>
    /// The DOTS data structure for the HasTarget struct.
    /// </summary>
    public struct HasTargetComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
        [Tooltip("Buffer index into SharedVariableElement for the target entity.")]
        public int TargetEntityVariableIndex;
    }

    /// <summary>
    /// A DOTS flag indicating when a HasTarget node is active.
    /// </summary>
    public struct HasTargetFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the HasTarget logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct HasTargetTaskSystem : ISystem
    {
        /// <summary>
        /// Updates the logic.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            foreach (var (branchComponents, taskComponents, hasTargetComponents, sharedVariables) in
                SystemAPI.Query<DynamicBuffer<BranchComponent>, DynamicBuffer<TaskComponent>, DynamicBuffer<HasTargetComponent>, DynamicBuffer<SharedVariableElement>>().WithAll<HasTargetFlag, EvaluateFlag>()) {
                for (int i = 0; i < hasTargetComponents.Length; ++i) {
                    var hasTargetComponent = hasTargetComponents[i];
                    var taskComponent = taskComponents[hasTargetComponent.Index];
                    var branchComponent = branchComponents[taskComponent.BranchIndex];
                    if (!branchComponent.CanExecute) {
                        continue;
                    }

                    if (taskComponent.Status != TaskStatus.Queued) {
                        continue;
                    }

                    var targetEntity = sharedVariables.Get<Entity>(hasTargetComponent.TargetEntityVariableIndex);
                    var hasTarget = targetEntity != Entity.Null && state.EntityManager.Exists(targetEntity);

                    taskComponent.Status = hasTarget ? TaskStatus.Success : TaskStatus.Failure;

                    var taskComponentBuffer = taskComponents;
                    taskComponentBuffer[hasTargetComponent.Index] = taskComponent;
                }
            }
        }
    }

    /// <summary>
    /// A DOTS tag indicating when an HasTarget node needs to be reevaluated.
    /// </summary>
    public struct HasTargetReevaluateFlag : IComponentData, IEnableableComponent
    {
    }

    /// <summary>
    /// Runs the HasTarget reevaluation logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct HasTargetReevaluateTaskSystem : ISystem
    {
        /// <summary>
        /// Updates the reevaluation logic.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            foreach (var (branchComponents, taskComponents, hasTargetComponents, sharedVariables) in
                SystemAPI.Query<DynamicBuffer<BranchComponent>, DynamicBuffer<TaskComponent>, DynamicBuffer<HasTargetComponent>, DynamicBuffer<SharedVariableElement>>().WithAll<HasTargetReevaluateFlag, EvaluateFlag>()) {
                for (int i = 0; i < hasTargetComponents.Length; ++i) {
                    var hasTargetComponent = hasTargetComponents[i];
                    var taskComponent = taskComponents[hasTargetComponent.Index];
                    var branchComponent = branchComponents[taskComponent.BranchIndex];
                    if (!branchComponent.CanExecute) {
                        continue;
                    }

                    if (!taskComponent.Reevaluate) {
                        continue;
                    }

                    var targetEntity = sharedVariables.Get<Entity>(hasTargetComponent.TargetEntityVariableIndex);
                    var hasTarget = targetEntity != Entity.Null && state.EntityManager.Exists(targetEntity);

                    var status = hasTarget ? TaskStatus.Success : TaskStatus.Failure;
                    if (status != taskComponent.Status) {
                        taskComponent.Status = status;
                        var buffer = taskComponents;
                        buffer[taskComponent.Index] = taskComponent;
                    }
                }
            }
        }
    }
}