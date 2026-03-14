#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.BehaviorDesigner.Runtime.Utility;
    using Opsive.GraphDesigner.Runtime;
    using Unity.Entities;
    using Unity.Burst;
    using UnityEngine;
    using Unity.Collections;

    [NodeIcon("7c0aba0d8377aac48966d8e3f817a2a8", "90105f40f82a30e45b08d150c1928950")]
    [Opsive.Shared.Utility.Description("Performs the actual interruption. This will immediately stop the specified tasks from running and will return success or failure depending on the value of interrupt success.")]
    public class PerformInterruption : ECSActionTask<PerformInterruptionTaskSystem, PerformInterruptionComponent>
    {
        [Tooltip("The task that should be interrupted.")]
        [SerializeField] ILogicNode[] m_InterruptTasks;
        [Tooltip("Should the interrupted task return success?")]
        [SerializeField] bool m_InterruptSuccess;

        /// <summary>
        /// The type of tag that should be enabled when the task is running.
        /// </summary>
        public override ComponentType Flag { get => typeof(PerformInterruptionFlag); }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override PerformInterruptionComponent GetBufferElement()
        {
            if (m_InterruptTasks == null || m_InterruptTasks.Length == 0) {
                UnityEngine.Debug.LogError("Error: At least one interrupt task must be specified.");
                return new PerformInterruptionComponent();
            }

            var indicies = new ushort[m_InterruptTasks.Length];
            var nullTaskCount = 0;
            for (int i = 0; i < m_InterruptTasks.Length; ++i) {
                if (m_InterruptTasks[i] == null) {
                    nullTaskCount++;
                    continue;
                }
                indicies[i - nullTaskCount] = m_InterruptTasks[i].Index;
            }
            if (nullTaskCount > 0) {
                System.Array.Resize(ref indicies, indicies.Length - nullTaskCount);
            }

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<IndiciesBlob>();
            var indicesArray = builder.Allocate(ref root.Indicies, indicies.Length);
            for (int i = 0; i < indicies.Length; i++) {
                indicesArray[i] = indicies[i];
            }
            var blobAsset = builder.CreateBlobAssetReference<IndiciesBlob>(Allocator.Persistent);
            builder.Dispose();

            return new PerformInterruptionComponent() {
                Index = RuntimeIndex,
                InterruptIndicies = blobAsset,
                InterruptSuccess = m_InterruptSuccess
            };
        }

        /// <summary>
        /// Adds the IBufferElementData to the entity.
        /// </summary>
        /// <param name="world">The world that the entity exists in.</param>
        /// <param name="entity">The entity that the IBufferElementData should be assigned to.</param>
        /// <param name="gameObject">The GameObject that the entity is attached to.</param>
        /// <returns>The index of the element within the buffer.</returns>
        public override int AddBufferElement(World world, Entity entity, GameObject gameObject)
        {
            var index = base.AddBufferElement(world, entity, gameObject);
            ComponentUtility.AddInterruptComponents(world.EntityManager, entity);
            return index;
        }
    }

    /// <summary>
    /// The DOTS data structure for the PerformInterruption class.
    /// </summary>
    public struct PerformInterruptionComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        [SerializeField] public ushort Index;
        [Tooltip("The indicies of the tasks that should be interrupted.")]
        [SerializeField] public BlobAssetReference<IndiciesBlob> InterruptIndicies;
        [Tooltip("Should the interrupted tasks return success?")]
        [SerializeField] public bool InterruptSuccess;
    }
    
    /// <summary>
    /// A DOTS flag indicating when a PerformInterruption node is active.
    /// </summary>
    public struct PerformInterruptionFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the PerformInterruption logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct PerformInterruptionTaskSystem : ISystem
    {
        /// <summary>
        /// Updates the logic.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            foreach (var (branchComponents, taskComponents, performInterruptionComponents, entity) in
                SystemAPI.Query<DynamicBuffer<BranchComponent>, DynamicBuffer<TaskComponent>, DynamicBuffer<PerformInterruptionComponent>>().WithAll<PerformInterruptionFlag, EvaluateFlag>().WithEntityAccess()) {
                for (int i = 0; i < performInterruptionComponents.Length; ++i) {
                    var performInterruptionComponent = performInterruptionComponents[i];
                    var taskComponent = taskComponents[performInterruptionComponent.Index];

                    if (taskComponent.Status == TaskStatus.Queued) {
                        taskComponent.Status = TaskStatus.Success;
                        var taskComponentsBuffer = taskComponents;
                        taskComponentsBuffer[taskComponent.Index] = taskComponent;

                        var branchComponentsBuffer = branchComponents;
                        for (int j = 0; j < performInterruptionComponent.InterruptIndicies.Value.Indicies.Length; ++j) {
                            var interruptTaskComponent = taskComponents[performInterruptionComponent.InterruptIndicies.Value.Indicies[j]];
                            var interruptBranchComponent = branchComponents[interruptTaskComponent.BranchIndex];
                            interruptBranchComponent.InterruptType = performInterruptionComponent.InterruptSuccess ? InterruptType.ImmediateSuccess : InterruptType.ImmediateFailure;
                            interruptBranchComponent.InterruptIndex = interruptTaskComponent.Index;
                            branchComponentsBuffer[interruptTaskComponent.BranchIndex] = interruptBranchComponent;
                        }

                        state.EntityManager.SetComponentEnabled<InterruptFlag>(entity, true);
                    }
                }
            }
        }

        /// <summary>
        /// The task has been destroyed.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnDestroy(ref SystemState state)
        {
            foreach (var performInterruptionComponents in SystemAPI.Query<DynamicBuffer<PerformInterruptionComponent>>()) {
                for (int i = 0; i < performInterruptionComponents.Length; ++i) {
                    var performInterruptionComponent = performInterruptionComponents[i];
                    if (performInterruptionComponent.InterruptIndicies.IsCreated) {
                        performInterruptionComponent.InterruptIndicies.Dispose();
                    }
                }
            }
        }
    }
}
#endif