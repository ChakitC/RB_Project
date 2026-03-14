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
    using Unity.Mathematics;
    using Unity.Transforms;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Uses DOTS to rotate around the center. This task will always return a status of running.")]
    [Shared.Utility.Category("Behavior Designer Samples/DOTS")]
    public class RotateTowardsEntity : ECSActionTask<RotateTowardsEntityTaskSystem, RotateTowardsEntityComponent>
    {
        [Tooltip("The angular speed of the agent.")]
        [SerializeField] float m_AngularSpeed;

        /// <summary>
        /// The type of flag that should be enabled when the task is running.
        /// </summary>
        public override ComponentType Flag { get => typeof(RotateTowardsEntityFlag); }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override RotateTowardsEntityComponent GetBufferElement()
        {
            return new RotateTowardsEntityComponent()
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
    /// The DOTS data structure for the RotateTowardsEntity struct.
    /// </summary>
    public struct RotateTowardsEntityComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
        [Tooltip("The angular speed of the agent.")]
        public float AngularSpeed;
    }

    /// <summary>
    /// A DOTS flag indicating when a RotateTowardsEntity node is active.
    /// </summary>
    public struct RotateTowardsEntityFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the RotateTowardsEntity logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct RotateTowardsEntityTaskSystem : ISystem
    {
        /// <summary>
        /// Updates the logic.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (localTransform, taskComponents, rotateTorwardsTargetComponents) in
                SystemAPI.Query<RefRW<LocalTransform>, DynamicBuffer<TaskComponent>, DynamicBuffer<RotateTowardsEntityComponent>>().WithAll<RotateTowardsEntityFlag, EvaluateFlag>()) {
                for (int i = 0; i < rotateTorwardsTargetComponents.Length; ++i) {
                    var rotateTowardsEntityComponent = rotateTorwardsTargetComponents[i];
                    var taskComponent = taskComponents[rotateTowardsEntityComponent.Index];
                    if (taskComponent.Status == TaskStatus.Queued) {
                        taskComponent.Status = TaskStatus.Running;

                        var taskComponentBuffer = taskComponents;
                        taskComponentBuffer[rotateTowardsEntityComponent.Index] = taskComponent;
                    }

                    if (taskComponent.Status != TaskStatus.Running) {
                        continue;
                    }

                    // Find the target. There will only be one entity with the TargetEntityTag.
                    foreach (var (targetTransform, targetEntity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<TargetEntityTag>().WithEntityAccess()) {
                        var target = quaternion.EulerXYZ(0, -GetAngle(targetTransform.ValueRO.Position), 0);
                        localTransform.ValueRW.Rotation = RotateTowards(localTransform.ValueRO.Rotation, target, rotateTowardsEntityComponent.AngularSpeed * deltaTime);
                        break;
                    }
                }
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
            var n = 180 - (math.atan2(-target.x, -target.z)) * 180 / math.PI;
            return (n % 360) * math.TORADIANS;
        }

        /// <summary>
        /// Rotates the specified target.
        /// </summary>
        /// <param name="from">The original rotation.</param>
        /// <param name="to">The target rotation.</param>
        /// <param name="maxDelta">The maximum delta amount.</param>
        /// <returns>The computed rotation.</returns>
        private quaternion RotateTowards(quaternion from, quaternion to, float maxDelta)
        {
            var angle = math.angle(from, to);
            if (angle <= 0) {
                return to;
            }
            return math.slerp(from, to, math.clamp(maxDelta / angle, 0, 1));
        }
    }
}