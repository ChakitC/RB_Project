#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Decorators
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.Shared.Utility;
    using Unity.Burst;
    using Unity.Entities;
    using UnityEngine;
    using UnityEngine.Scripting.APIUpdating;

    /// <summary>
    /// A node representation of the repeater task.
    /// </summary>
    [NodeIcon("ceb6f3e7f67cde640b28b2a15ec13ffe", "bb415ca6de87c3d49ab9a94fe8a6fca8")]
    [Opsive.Shared.Utility.Description(@"The repeater task will repeat execution of its child task until the child task has been run a specified number of times. " +
                       "It has the option of continuing to execute the child task even if the child task returns a failure.")]
    [MovedFrom(false, "Opsive.BehaviorDesigner.Runtime.Tasks.Decorators", "Opsive.BehaviorDesigner.Runtime", "SharedRepeater")]
    public class Repeater : ECSDecoratorTask<RepeaterTaskSystem, RepeaterComponent, RepeaterFlag>, IParentNode, ISavableTask
    {
        [Tooltip("Should the task be repeated forever?")]
        [FormerlySerializedType(typeof(bool))]
        [SerializeField] SharedVariable<bool> m_RepeatForever = true;
        [Tooltip("The number of times the task should repeat.")]
        [FormerlySerializedType(typeof(ushort))]
        [SerializeField] SharedVariable<int> m_RepeatCount = 0;
        [Tooltip("Should the repeater end if the child task fails?")]
        [FormerlySerializedType(typeof(bool))]
        [SerializeField] SharedVariable<bool> m_EndOnFailure = false;

        private ushort m_ComponentIndex;
        private ECSSharedVariableIndex<bool> m_RepeatForeverVariableIndex;
        private ECSSharedVariableIndex<int> m_RepeatCountVariableIndex;
        private ECSSharedVariableIndex<bool> m_EndOnFailureVariableIndex;

        public SharedVariable<bool> RepeatForever { get => m_RepeatForever; set => m_RepeatForever = value; }
        public SharedVariable<int> RepeatCount { get => m_RepeatCount; set => m_RepeatCount = value; }
        public SharedVariable<bool> EndOnFailure { get => m_EndOnFailure; set => m_EndOnFailure = value; }

        /// <summary>
        /// Resets the task to its default values.
        /// </summary>
        public override void Reset()
        {
            m_RepeatForever = true;
            m_RepeatCount = 0;
            m_EndOnFailure = false;
        }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override RepeaterComponent GetBufferElement()
        {
            return new RepeaterComponent() {
                Index = RuntimeIndex,
                RepeatForeverVariableIndex = m_RepeatForeverVariableIndex.Index,
                RepeatCountVariableIndex = m_RepeatCountVariableIndex.Index,
                EndOnFailureVariableIndex = m_EndOnFailureVariableIndex.Index,
            };
        }

        /// <summary>
        /// Adds the IBufferElementData to the entity.
        /// </summary>
        /// <param name="world">The world that the entity exists in.</param>
        /// <param name="entity">The entity that the IBufferElementData should be assigned to.</param>
        /// <param name="registry">The ECS variable registry for registering SharedVariable fields.</param>
        /// <param name="gameObject">The GameObject that the entity is attached to.</param>
        /// <returns>The index of the element within the buffer.</returns>
        public override int AddBufferElement(World world, Entity entity, ECSVariableRegistry registry, GameObject gameObject)
        {
            m_RepeatForeverVariableIndex = new ECSSharedVariableIndex<bool>(registry.Register(m_RepeatForever));
            m_RepeatCountVariableIndex = new ECSSharedVariableIndex<int>(registry.Register(m_RepeatCount));
            m_EndOnFailureVariableIndex = new ECSSharedVariableIndex<bool>(registry.Register(m_EndOnFailure));
            m_ComponentIndex = (ushort)base.AddBufferElement(world, entity, registry, gameObject);
            return m_ComponentIndex;
        }

        /// <summary>
        /// Specifies the type of reflection that should be used to save the task.
        /// </summary>
        /// <param name="index">The index of the sub-task. This is used for the task set allowing each contained task to have their own save type.</param>
        public MemberVisibility GetSaveReflectionType(int index) { return MemberVisibility.None; }

        /// <summary>
        /// Returns the current task state.
        /// </summary>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        /// <returns>The current task state.</returns>
        public object Save(World world, Entity entity)
        {
            var repeaterComponents = world.EntityManager.GetBuffer<RepeaterComponent>(entity);
            var repeaterComponent = repeaterComponents[m_ComponentIndex];

            // Save the current count.
            return repeaterComponent.CurrentCount;
        }

        /// <summary>
        /// Loads the previous task state.
        /// </summary>
        /// <param name="saveData">The previous task state.</param>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        public void Load(object saveData, World world, Entity entity)
        {
            var repeaterComponents = world.EntityManager.GetBuffer<RepeaterComponent>(entity);
            var repeaterComponent = repeaterComponents[m_ComponentIndex];

            // saveData is the current count.
            repeaterComponent.CurrentCount = (uint)saveData;
            repeaterComponents[m_ComponentIndex] = repeaterComponent;
        }
    }

    /// <summary>
    /// The DOTS data structure for the Repeater class.
    /// </summary>
    public struct RepeaterComponent : IBufferElementData
    {
        [Tooltip("The index of the node.")]
        public ushort Index;
        [Tooltip("Buffer index into SharedVariableElement for the repeat-forever flag.")]
        public int RepeatForeverVariableIndex;
        [Tooltip("Buffer index into SharedVariableElement for the repeat count.")]
        public int RepeatCountVariableIndex;
        [Tooltip("The number of times the child task has been repeated.")]
        public uint CurrentCount;
        [Tooltip("Buffer index into SharedVariableElement for the end-on-failure flag.")]
        public int EndOnFailureVariableIndex;
    }

    /// <summary>
    /// A DOTS tag indicating when a Repeater node is active.
    /// </summary>
    public struct RepeaterFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Runs the Repeater logic.
    /// </summary>
    [DisableAutoCreation]
    public partial struct RepeaterTaskSystem : ISystem
    {
        private EntityQuery m_Query;

        /// <summary>
        /// Builds the query.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        private void OnCreate(ref SystemState state)
        {
            m_Query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<RepeaterComponent>().WithAll<RepeaterFlag, EvaluateFlag, SharedVariableElement>().Build();
        }

        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            state.Dependency = new RepeaterJob().ScheduleParallel(m_Query, state.Dependency);
        }

        /// <summary>
        /// Job which executes the task logic.
        /// </summary>
        [BurstCompile]
        private partial struct RepeaterJob : IJobEntity
        {
            /// <summary>
            /// Executes the repeater logic.
            /// </summary>
            /// <param name="branchComponents">An array of BranchComponents.</param>
            /// <param name="taskComponents">An array of TaskComponents.</param>
            /// <param name="repeaterComponents">An array of RepeaterComponents.</param>
            /// <param name="sharedVariables">The shared variable buffer for this entity.</param>
            [BurstCompile]
            public void Execute(ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<RepeaterComponent> repeaterComponents,
                DynamicBuffer<SharedVariableElement> sharedVariables)
            {
                for (int i = 0; i < repeaterComponents.Length; ++i) {
                    var repeaterComponent = repeaterComponents[i];
                    var repeatForever = sharedVariables.Get<bool>(repeaterComponent.RepeatForeverVariableIndex);
                    var repeatCount = sharedVariables.Get<int>(repeaterComponent.RepeatCountVariableIndex);
                    var endOnFailure = sharedVariables.Get<bool>(repeaterComponent.EndOnFailureVariableIndex);
                    var taskComponent = taskComponents[repeaterComponent.Index];
                    var taskStatus = taskComponent.Status;
                    if (taskStatus != TaskStatus.Queued && taskStatus != TaskStatus.Running) {
                        continue;
                    }

                    var branchComponent = branchComponents[taskComponent.BranchIndex];
                    if (!branchComponent.CanExecute || branchComponent.InterruptType != InterruptType.None) {
                        continue;
                    }

                    var childIndex = (ushort)(taskComponent.Index + 1);
                    if (taskStatus == TaskStatus.Queued) {
                        taskComponent.Status = TaskStatus.Running;
                        taskComponents[taskComponent.Index] = taskComponent;

                        if (repeaterComponent.CurrentCount != 1) {
                            repeaterComponent.CurrentCount = 1;
                            var repeaterBuffer = repeaterComponents;
                            repeaterBuffer[i] = repeaterComponent;
                        }

                        if (branchComponent.NextIndex != childIndex) {
                            branchComponent.NextIndex = childIndex;
                            branchComponents[taskComponent.BranchIndex] = branchComponent;
                        }

                        // Start the child.
                        var nextChildTaskComponent = taskComponents[childIndex];
                        if (nextChildTaskComponent.Status != TaskStatus.Queued) {
                            nextChildTaskComponent.Status = TaskStatus.Queued;
                            taskComponents[childIndex] = nextChildTaskComponent;
                        }
                        continue;
                    }

                    // The repeater task is currently active. Check the first child.
                    var childTaskComponent = taskComponents[childIndex];
                    if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running) {
                        // The child should keep running.
                        continue;
                    }

                    branchComponent = branchComponents[childTaskComponent.BranchIndex];
                    if ((repeatForever || repeaterComponent.CurrentCount <= repeatCount) &&
                        (childTaskComponent.Status == TaskStatus.Success || (!endOnFailure && childTaskComponent.Status == TaskStatus.Failure))) {
                        // Restart the child if the branch should repeat again.
                        if (childTaskComponent.Status != TaskStatus.Queued) {
                            childTaskComponent.Status = TaskStatus.Queued;
                            taskComponents[childTaskComponent.Index] = childTaskComponent;
                        }

                        repeaterComponent.CurrentCount++;
                        var repeaterBuffer = repeaterComponents;
                        repeaterBuffer[i] = repeaterComponent;

                        if (branchComponent.NextIndex != childTaskComponent.Index) {
                            branchComponent.NextIndex = childTaskComponent.Index;
                            branchComponents[childTaskComponent.BranchIndex] = branchComponent;
                        }
                    } else {
                        // End with the child status if there should not be any more repeats. An inactive status will be returned if the child is disabled.
                        var status = childTaskComponent.Status == TaskStatus.Inactive ? TaskStatus.Success : childTaskComponent.Status;
                        if (taskComponent.Status != status) {
                            taskComponent.Status = status;
                            taskComponents[taskComponent.Index] = taskComponent;
                        }

                        if (branchComponent.NextIndex != taskComponent.ParentIndex) {
                            branchComponent.NextIndex = taskComponent.ParentIndex;
                            branchComponents[childTaskComponent.BranchIndex] = branchComponent;
                        }
                    }
                }
            }
        }
    }
}
#endif