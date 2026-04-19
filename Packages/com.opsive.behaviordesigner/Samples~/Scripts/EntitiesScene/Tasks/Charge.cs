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
    using Unity.Collections;
    using Unity.Mathematics;
    using Unity.Transforms;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Uses DOTS to move towards the center point, returns success when the agent is less than the arrive distance.")]
    [Shared.Utility.Category("Behavior Designer Samples/DOTS")]
    public class Charge : ECSActionTask<ChargeTaskSystem, ChargeComponent, ChargeFlag>
    {
        [Tooltip("The speed of the agent.")]
        [SerializeField] SharedVariable<float> m_Speed;
        [Tooltip("The distance away from the target when the agent has arrived at the target.")]
        [SerializeField] float m_ArriveDistance;

        private ECSSharedVariableIndex<float> m_SpeedIndex;

        /// <summary>
        /// Resets the task to its default values.
        /// </summary>
        public override void Reset()
        {
            m_Speed = new SharedVariable<float> { Value = 10f };
            m_ArriveDistance = 0.1f;
        }

        /// <summary>
        /// Registers the speed SharedVariable and adds the buffer element to the entity.
        /// </summary>
        /// <param name="world">The world that the entity exists in.</param>
        /// <param name="entity">The entity that the IBufferElementData should be assigned to.</param>
        /// <param name="registry">The ECS variable registry for registering SharedVariable fields.</param>
        /// <param name="gameObject">The GameObject that the entity is attached to.</param>
        /// <returns>The index of the element within the buffer.</returns>
        public override int AddBufferElement(World world, Entity entity, ECSVariableRegistry registry, GameObject gameObject)
        {
            m_SpeedIndex = new ECSSharedVariableIndex<float>(registry.Register(m_Speed));
            return base.AddBufferElement(world, entity, registry, gameObject);
        }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override ChargeComponent GetBufferElement()
        {
            return new ChargeComponent()
            {
                Index = RuntimeIndex,
                SpeedVariableIndex = m_SpeedIndex.Index,
                ArriveDistance = m_ArriveDistance,
            };
        }
    }

    /// <summary>
    /// The DOTS data structure for the Charge struct.
    /// </summary>
    public struct ChargeComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
        [Tooltip("Buffer index into SharedVariableElement for the agent's speed.")]
        public int SpeedVariableIndex;
        [Tooltip("The distance away from the target when the agent has arrived at the target.")]
        public float ArriveDistance;
    }

    /// <summary>
    /// A DOTS flag indicating when a Charge node is active.
    /// </summary>
    public struct ChargeFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the Charge logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct ChargeTaskSystem : ISystem
    {
        private EntityQuery m_ChargeQuery;

        /// <summary>
        /// Creates the required objects for use within the job system.
        /// </summary>
        /// <param name="state">The current SystemState.</param>
        [BurstCompile]
        private void OnCreate(ref SystemState state)
        {
            m_ChargeQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<LocalTransform>()
                .WithAll<ChargeComponent, SharedVariableElement, EvaluateFlag>()
                .Build(ref state);
        }

        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ChargeJob()
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(m_ChargeQuery, state.Dependency);
        }

        /// <summary>
        /// Charges towards the center.
        /// </summary>
        [BurstCompile]
        private partial struct ChargeJob : IJobEntity
        {
            [Tooltip("The current frame's DeltaTime.")]
            public float DeltaTime;

            /// <summary>
            /// Updates the logic.
            /// </summary>
            /// <param name="branchComponents">An array of BranchComponents.</param>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="chargeComponents">An array of ChargeComponents.</param>
            /// <param name="sharedVariables">The shared variable buffer for this entity.</param>
            /// <param name="transform">The entity's transform.</param>
            [BurstCompile]
            public void Execute(ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<ChargeComponent> chargeComponents, DynamicBuffer<SharedVariableElement> sharedVariables, ref LocalTransform transform)
            {
                for (int i = 0; i < chargeComponents.Length; ++i) {
                    var chargeComponent = chargeComponents[i];
                    var taskComponent = taskComponents[chargeComponent.Index];
                    var branchComponent = branchComponents[taskComponent.BranchIndex];
                    if (!branchComponent.CanExecute) {
                        continue;
                    }

                    if (taskComponent.Status == TaskStatus.Queued) {
                        taskComponent.Status = TaskStatus.Running;
                        taskComponents[chargeComponent.Index] = taskComponent;
                    }

                    if (taskComponent.Status != TaskStatus.Running) {
                        continue;
                    }

                    // The task should return success as soon as the agent has arrived.
                    var direction = -transform.Position;
                    if (math.length(direction) < chargeComponent.ArriveDistance) {
                        taskComponent.Status = TaskStatus.Success;
                        taskComponents[chargeComponent.Index] = taskComponent;

                        continue;
                    }

                    // Read the speed from the shared variable buffer and move toward the center.
                    var speed = sharedVariables.Get<float>(chargeComponent.SpeedVariableIndex);
                    transform.Position += (direction * speed * DeltaTime);
                }
            }
        }
    }
}