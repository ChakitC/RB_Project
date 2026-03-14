/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Samples
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.BehaviorDesigner.Runtime.Groups;
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.GraphDesigner.Runtime;
    using System;
    using Unity.Burst;
    using Unity.Entities;
    using Unity.Transforms;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Uses DOTS to determine if the entity has a target.")]
    [Shared.Utility.Category("Behavior Designer Samples/DOTS")]
    public class FindTarget : ECSActionTask<FindTargetTaskSystem, FindTargetComponent>
    {
        /// <summary>
        /// The type of flag that should be enabled when the task is running.
        /// </summary>
        public override ComponentType Flag { get => typeof(FindTargetFlag); }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override FindTargetComponent GetBufferElement()
        {
            return new FindTargetComponent()
            {
                Index = RuntimeIndex,
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
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            foreach (var (transform, taskComponents, findTargetComponents) in
                SystemAPI.Query<RefRW<LocalTransform>, DynamicBuffer<TaskComponent>, DynamicBuffer<FindTargetComponent>>().WithAll<FindTargetFlag, EvaluateFlag>()) {
                for (int i = 0; i < findTargetComponents.Length; ++i) {
                    var fndTargetComponent = findTargetComponents[i];
                    var taskComponent = taskComponents[fndTargetComponent.Index];
                    if (taskComponent.Status != TaskStatus.Queued) {
                        continue;
                    }

                    var index = -1;
                    var count = 0;
                    var entities = state.EntityManager.GetAllEntities(Unity.Collections.Allocator.Temp);
                    var foundAgent = false;
                    if (entities.Length > 0) {
                        do {
                            index = randomGenerator.NextInt(entities.Length);
                            count++;
                        } while (count < entities.Length * 2 && !(foundAgent = state.EntityManager.HasComponent<AgentTag>(entities[index])));
                    }

                    // A new target has been found. Add the TargetEntityTag.
                    if (foundAgent) {
                        ecb.AddComponent<TargetEntityTag>(entities[index]);
                    }
                    entities.Dispose();

                    // The task is complete, return to the parent.
                    taskComponent.Status = foundAgent ? TaskStatus.Success : TaskStatus.Failure;
                    var taskComponentBuffer = taskComponents;
                    taskComponentBuffer[fndTargetComponent.Index] = taskComponent;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}