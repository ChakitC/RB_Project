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
    using Unity.Mathematics;
    using Unity.Transforms;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Uses DOTS to rotate around the center. This task will always return a status of running.")]
    [Shared.Utility.Category("Behavior Designer Samples/DOTS")]
    public class Swarm : ECSActionTask<SwarmTaskSystem, SwarmComponent>
    {
        [Tooltip("The angular speed of the agent.")]
        [SerializeField] float m_AngularSpeed;

        /// <summary>
        /// The type of flag that should be enabled when the task is running.
        /// </summary>
        public override ComponentType Flag { get => typeof(SwarmFlag); }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override SwarmComponent GetBufferElement()
        {
            return new SwarmComponent()
            {
                Index = RuntimeIndex,
                AngularSpeed = m_AngularSpeed,
            };
        }

        /// <summary>
        /// Resets the task to its default values.
        /// </summary>
        public override void Reset() { m_AngularSpeed = 2; }
    }

    /// <summary>
    /// The DOTS data structure for the Swarm struct.
    /// </summary>
    public struct SwarmComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
        [Tooltip("The angular speed of the agent.")]
        public float AngularSpeed;
    }

    /// <summary>
    /// A DOTS flag indicating when a Swarm node is active.
    /// </summary>
    public struct SwarmFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the Swarm logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct SwarmTaskSystem : ISystem
    {
        private EntityQuery m_SwarmQuery;

        /// <summary>
        /// Creates the required objects for use within the job system.
        /// </summary>
        /// <param name="state">The current SystemState.</param>
        [BurstCompile]
        private void OnCreate(ref SystemState state)
        {
            m_SwarmQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<TaskComponent>().WithAllRW<LocalTransform>()
                .WithAll<SwarmComponent, SwarmFlag, EvaluateFlag>()
                .Build(ref state);
        }

        /// <summary>
        /// Updates the logic.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            state.Dependency = new SwarmJob()
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(m_SwarmQuery, state.Dependency);
        }

        /// <summary>
        /// Rotates around the center.
        /// </summary>
        [BurstCompile]
        private partial struct SwarmJob : IJobEntity
        {
            [Tooltip("The current frame's DeltaTime.")]
            public float DeltaTime;

            /// <summary>
            /// Updates the logic.
            /// </summary>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="swarmComponents">An array of SwarmComponents.</param>
            /// <param name="transform">The entity's transform.</param>
            [BurstCompile]
            public void Execute(ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<SwarmComponent> swarmComponents, ref LocalTransform transform)
            {
                for (int i = 0; i < swarmComponents.Length; ++i) {
                    var swarmComponent = swarmComponents[i];
                    var taskComponent = taskComponents[swarmComponent.Index];
                    if (taskComponent.Status == TaskStatus.Queued) {
                        taskComponent.Status = TaskStatus.Running;

                        taskComponents[swarmComponent.Index] = taskComponent;
                    }

                    // Always swarm.
                    if (taskComponent.Status != TaskStatus.Running) {
                        continue;
                    }

                    var dist = math.length(transform.Position);
                    var radians = GetAngle(transform.Position);

                    radians += swarmComponent.AngularSpeed * DeltaTime;

                    transform.Position.x = math.cos(radians) * dist;
                    transform.Position.z = math.sin(radians) * dist;
                }
            }

            /// <summary>
            /// Returns the angle between the target point and the center.
            /// </summary>
            /// <param name="target">The target point.</param>
            /// <returns>The angle between the target point and the center. This will be in the range of 0 - 2PI (radians).</returns>
            [BurstCompile]
            private float GetAngle(float3 target)
            {
                var n = 270 - (math.atan2(-target.x, -target.z)) * 180 / math.PI;
                return (n % 360) * math.TORADIANS;
            }
        }
    }
}