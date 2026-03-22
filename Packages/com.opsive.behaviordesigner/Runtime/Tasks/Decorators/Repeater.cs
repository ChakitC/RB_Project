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

    /// <summary>
    /// A node representation of the repeater task.
    /// </summary>
    [NodeIcon("ceb6f3e7f67cde640b28b2a15ec13ffe", "bb415ca6de87c3d49ab9a94fe8a6fca8")]
    [Opsive.Shared.Utility.Description(@"The repeater task will repeat execution of its child task until the child task has been run a specified number of times. " +
                      "It has the option of continuing to execute the child task even if the child task returns a failure.")]
    public class Repeater : ECSDecoratorTask<RepeaterTaskSystem, RepeaterComponent>, IParentNode, ISavableTask
    {
        [Tooltip("Should the task be repeated forever?")]
        [SerializeField] bool m_RepeatForever;
        [Tooltip("The number of times the task should repeat.")]
        [SerializeField] ushort m_RepeatCount;
        [Tooltip("Should the repeater end if the child task fails?")]
        [SerializeField] bool m_EndOnFailure;

        private ushort m_ComponentIndex;

        public bool RepeatForever { get => m_RepeatForever; set => m_RepeatForever = value; }
        public ushort RepeatCount { get => m_RepeatCount; set => m_RepeatCount = value; }
        public bool EndOnFailure { get => m_EndOnFailure; set => m_EndOnFailure = value; }

        public override ComponentType Flag { get => typeof(RepeaterFlag); }

        /// <summary>
        /// Resets the task to its default values.
        /// </summary>
        public override void Reset() { m_RepeatForever = true; }

        /// <summary>
        /// Returns a new TBufferElement for use by the system.
        /// </summary>
        /// <returns>A new TBufferElement for use by the system.</returns>
        public override RepeaterComponent GetBufferElement()
        {
            return new RepeaterComponent() {
                Index = RuntimeIndex,
                RepeatCount = m_RepeatForever ? -1 : m_RepeatCount,
                EndOnFailure = m_EndOnFailure,
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
            m_ComponentIndex = (ushort)base.AddBufferElement(world, entity, gameObject);
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
        [Tooltip("The number of times the child task can repeat.")]
        public int RepeatCount;
        [Tooltip("The number of times the child task has been repeated.")]
        public uint CurrentCount;
        [Tooltip("Should the task end when the child returns failure?")]
        public bool EndOnFailure;
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
        /// <summary>
        /// Creates the job.
        /// </summary>
        /// <param name="state">The current state of the system.</param>
        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder().WithAllRW<BranchComponent>().WithAllRW<TaskComponent>().WithAllRW<RepeaterComponent>().WithAll<RepeaterFlag, EvaluateFlag>().Build();
            state.Dependency = new RepeaterJob().ScheduleParallel(query, state.Dependency);
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
            [BurstCompile]
            public void Execute(ref DynamicBuffer<BranchComponent> branchComponents, ref DynamicBuffer<TaskComponent> taskComponents, ref DynamicBuffer<RepeaterComponent> repeaterComponents)
            {
                for (int i = 0; i < repeaterComponents.Length; ++i) {
                    var repeaterComponent = repeaterComponents[i];
                    var taskComponent = taskComponents[repeaterComponent.Index];
                    var branchComponent = branchComponents[taskComponent.BranchIndex];
                    if (!branchComponent.CanExecute) {
                        continue;
                    }

                    if (taskComponent.Status == TaskStatus.Queued) {
                        taskComponent.Status = TaskStatus.Running;
                        taskComponents[taskComponent.Index] = taskComponent;

                        repeaterComponent.CurrentCount = 1;
                        var repeaterBuffer = repeaterComponents;
                        repeaterBuffer[i] = repeaterComponent;

                        branchComponent.NextIndex = (ushort)(taskComponent.Index + 1);
                        branchComponents[taskComponent.BranchIndex] = branchComponent;

                        // Start the child.
                        var nextChildTaskComponent = taskComponents[branchComponent.NextIndex];
                        nextChildTaskComponent.Status = TaskStatus.Queued;
                        taskComponents[branchComponent.NextIndex] = nextChildTaskComponent;
                    } else if (taskComponent.Status != TaskStatus.Running) {
                        continue;
                    }

                    // The repeater task is currently active. Check the first child.
                    var childTaskComponent = taskComponents[taskComponent.Index + 1];
                    if (childTaskComponent.Status == TaskStatus.Queued || childTaskComponent.Status == TaskStatus.Running) {
                        // The child should keep running.
                        continue;
                    }

                    branchComponent = branchComponents[childTaskComponent.BranchIndex];
                    if ((repeaterComponent.RepeatCount == -1 || repeaterComponent.CurrentCount <= repeaterComponent.RepeatCount) &&
                        (childTaskComponent.Status == TaskStatus.Success || (!repeaterComponent.EndOnFailure && childTaskComponent.Status == TaskStatus.Failure))) {
                        // Restart the child if the branch should repeat again.
                        childTaskComponent.Status = TaskStatus.Queued;
                        taskComponents[childTaskComponent.Index] = childTaskComponent;

                        repeaterComponent.CurrentCount++;
                        var repeaterBuffer = repeaterComponents;
                        repeaterBuffer[i] = repeaterComponent;

                        branchComponent.NextIndex = childTaskComponent.Index;
                        branchComponents[childTaskComponent.BranchIndex] = branchComponent;
                    } else {
                        // End with the child status if there should not be any more repeats. An inactive status will be returned if the child is disabled.
                        taskComponent.Status = childTaskComponent.Status == TaskStatus.Inactive ? TaskStatus.Success : childTaskComponent.Status;
                        taskComponents[taskComponent.Index] = taskComponent;

                        branchComponent.NextIndex = taskComponent.ParentIndex;
                        branchComponents[childTaskComponent.BranchIndex] = branchComponent;
                    }
                }
            }
        }
    }
    /// <summary>
    /// A node representation of the repeater task.
    /// </summary>
    [NodeIcon("ceb6f3e7f67cde640b28b2a15ec13ffe", "bb415ca6de87c3d49ab9a94fe8a6fca8")]
    [Opsive.Shared.Utility.Description(@"The repeater task will repeat execution of its child task until the child task has been run a specified number of times. " +
                      "It has the option of continuing to execute the child task even if the child task returns a failure. Uses the GameObject workflow.")]
    public class SharedRepeater : DecoratorNode
    {
        [Tooltip("Should the task be repeated forever?")]
        [SerializeField] SharedVariable<bool> m_RepeatForever = true;
        [Tooltip("The number of times the task should repeat.")]
        [SerializeField] SharedVariable<int> m_RepeatCount;
        [Tooltip("Should the repeater end if the child task fails?")]
        [SerializeField] SharedVariable<bool> m_EndOnFailure;

        public SharedVariable<bool> RepeatForever { get => m_RepeatForever; set => m_RepeatForever = value; }
        public SharedVariable<int> RepeatCount { get => m_RepeatCount; set => m_RepeatCount = value; }
        public SharedVariable<bool> EndOnFailure { get => m_EndOnFailure; set => m_EndOnFailure = value; }

        private uint m_CurrentCount;

        /// <summary>
        /// Callback when the task is started.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_CurrentCount = 0;
        }

        /// <summary>
        /// Executes the task logic.
        /// </summary>
        /// <returns>The status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            var taskComponents = m_BehaviorTree.World.EntityManager.GetBuffer<TaskComponent>(m_BehaviorTree.Entity);
            if (taskComponents[Index + 1].Status == TaskStatus.Running || taskComponents[Index + 1].Status == TaskStatus.Queued) {
                return TaskStatus.Running;
            }

            if (taskComponents[Index + 1].Status == TaskStatus.Failure && m_EndOnFailure.Value) {
                return TaskStatus.Failure;
            }

            // The child isn't running. Repeat
            if (m_RepeatForever.Value || m_CurrentCount <= m_RepeatCount.Value) {
                m_CurrentCount++;
                return TaskStatus.Running;
            }

            return taskComponents[Index + 1].Status;
        }

        /// <summary>
        /// Specifies the type of reflection that should be used to save the task.
        /// </summary>
        /// <param name="index">The index of the sub-task. This is used for the task set allowing each contained task to have their own save type.</param>
        public override MemberVisibility GetSaveReflectionType(int index) { return MemberVisibility.None; }

        /// <summary>
        /// Returns the current task state.
        /// </summary>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        /// <returns>The current task state.</returns>
        public override object Save(World world, Entity entity)
        {
            // Save the current count.
            return m_CurrentCount;
        }

        /// <summary>
        /// Loads the previous task state.
        /// </summary>
        /// <param name="saveData">The previous task state.</param>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        public override void Load(object saveData, World world, Entity entity)
        {
            m_CurrentCount = (uint)saveData;
        }
    }
}
#endif