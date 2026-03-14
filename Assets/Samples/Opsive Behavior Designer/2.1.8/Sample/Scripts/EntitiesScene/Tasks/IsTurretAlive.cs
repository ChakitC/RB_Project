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

    [Opsive.Shared.Utility.Description("Uses DOTS to determine if the turret is still alive.")]
    [Shared.Utility.Category("Behavior Designer Samples/DOTS")]
    public class IsTurretAlive : ECSConditionalTask<IsTurretAliveTaskSystem, IsTurretAliveComponent>, IReevaluateResponder
    {
        public override ComponentType Flag { get => typeof(IsTurretAliveFlag); }
        public ComponentType ReevaluateFlag { get => typeof(IsTurretAliveReevaluateFlag); }
        public System.Type ReevaluateSystemType { get => typeof(IsTurretAliveReevaluateTaskSystem); }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override IsTurretAliveComponent GetBufferElement()
        {
            return new IsTurretAliveComponent()
            {
                Index = RuntimeIndex,
            };
        }
    }

    /// <summary>
    /// The DOTS data structure for the IsTurretAlive struct.
    /// </summary>
    public struct IsTurretAliveComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
    }

    /// <summary>
    /// A DOTS flag indicating when a IsTurretAlive node is active.
    /// </summary>
    public struct IsTurretAliveFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the IsTurretAlive logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct IsTurretAliveTaskSystem : ISystem
    {
        /// <summary>
        /// Updates the logic.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            // Find the turret. An entity will be found if the turret hasn't been destroyed.
            var isTurretAlive = false;
            foreach (var localTransform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<TurretTag>()) {
                isTurretAlive = true;
                break;
            }

            foreach (var (taskComponents, isTurretAliveComponents) in
                SystemAPI.Query<DynamicBuffer<TaskComponent>, DynamicBuffer<IsTurretAliveComponent>>().WithAll<IsTurretAliveFlag, EvaluateFlag>()) {
                for (int i = 0; i < isTurretAliveComponents.Length; ++i) {
                    var isTurretAliveComponent = isTurretAliveComponents[i];
                    var taskComponent = taskComponents[isTurretAliveComponent.Index];
                    if (taskComponent.Status != TaskStatus.Queued) {
                        continue;
                    }

                    taskComponent.Status = isTurretAlive ? TaskStatus.Success : TaskStatus.Failure;

                    var taskComponentBuffer = taskComponents;
                    taskComponentBuffer[isTurretAliveComponent.Index] = taskComponent;
                }
            }
        }
    }


    /// <summary>
    /// A DOTS tag indicating when an IsTurretAlive node needs to be reevaluated.
    /// </summary>
    public struct IsTurretAliveReevaluateFlag : IComponentData, IEnableableComponent
    {
    }

    /// <summary>
    /// Runs the IsTurretAlive reevaluation logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct IsTurretAliveReevaluateTaskSystem : ISystem
    {
        /// <summary>
        /// Updates the reevaluation logic.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            // Find the turret. An entity will be found if the turret hasn't been destroyed.
            var isTurretAlive = false;
            foreach (var localTransform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<TurretTag>()) {
                isTurretAlive = true;
                break;
            }

            foreach (var (taskComponents, isTurretAliveComponents) in
                SystemAPI.Query<DynamicBuffer<TaskComponent>, DynamicBuffer<IsTurretAliveComponent>>().WithAll<IsTurretAliveReevaluateFlag, EvaluateFlag>()) {
                for (int i = 0; i < isTurretAliveComponents.Length; ++i) {
                    var isTurretAliveComponent = isTurretAliveComponents[i];
                    var taskComponent = taskComponents[isTurretAliveComponent.Index];
                    if (!taskComponent.Reevaluate) {
                        continue;
                    }

                    var status = isTurretAlive ? TaskStatus.Success : TaskStatus.Failure;
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