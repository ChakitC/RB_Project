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
    using Unity.Transforms;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Uses DOTS to determine if the entity has a target.")]
    [Shared.Utility.Category("Behavior Designer Samples/DOTS")]
    public class HasTarget : ECSConditionalTask<HasTargetTaskSystem, HasTargetComponent>, IReevaluateResponder
    {
        public override ComponentType Flag { get => typeof(HasTargetFlag); }
        public ComponentType ReevaluateFlag { get => typeof(HasTargetReevaluateFlag); }
        public System.Type ReevaluateSystemType { get => typeof(HasTargetReevaluateTaskSystem); }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override HasTargetComponent GetBufferElement()
        {
            return new HasTargetComponent()
            {
                Index = RuntimeIndex,
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
            foreach (var (taskComponents, hasTargetComponents) in
                SystemAPI.Query<DynamicBuffer<TaskComponent>, DynamicBuffer<HasTargetComponent>>().WithAll<HasTargetFlag, EvaluateFlag>()) {
                for (int i = 0; i < hasTargetComponents.Length; ++i) {
                    var hasTargetComponent = hasTargetComponents[i];
                    var taskComponent = taskComponents[hasTargetComponent.Index];
                    if (taskComponent.Status != TaskStatus.Queued) {
                        continue;
                    }

                    // Find the target. There will only be one entity with the TargetEntityTag.
                    var hasTarget = false;
                    foreach (var localTransform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<TargetEntityTag>()) {
                        hasTarget = true;
                        break;
                    }

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
            foreach (var (taskComponents, hasTargetComponents) in
                SystemAPI.Query<DynamicBuffer<TaskComponent>, DynamicBuffer<HasTargetComponent>>().WithAll<HasTargetReevaluateFlag, EvaluateFlag>()) {
                for (int i = 0; i < hasTargetComponents.Length; ++i) {
                    var hasTargetComponent = hasTargetComponents[i];
                    var taskComponent = taskComponents[hasTargetComponent.Index];
                    if (!taskComponent.Reevaluate) {
                        continue;
                    }

                    // Find the target. There will only be one entity with the TargetEntityTag.
                    var hasTarget = false;
                    foreach (var localTransform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<TargetEntityTag>()) {
                        hasTarget = true;
                        break;
                    }

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