#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Components
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using System.Runtime.InteropServices;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// The runtime DOTS data associated with a task.
    /// </summary>
    [System.Serializable]
    public struct TaskComponent : IBufferElementData
    {
        [Tooltip("The current execution status of the task.")]
        //public TaskStatus Status;
        [SerializeField] private TaskStatus m_Status;
        public TaskStatus Status { get => m_Status; 
            set { 
                m_Status = value;
                CanReevaluate = value != TaskStatus.Inactive;
                //UnityEngine.Debug.Log(string.Format("{0} status: {1}", Index, value));
            } 
        }
        [Tooltip("The index of the task within the behavior tree.")]
        public ushort Index;
        [Tooltip("The index of the parent task within the behavior tree.")]
        public ushort ParentIndex;
        //public ushort m_ParentIndex;
        //public ushort ParentIndex { get { return m_ParentIndex; } set { UnityEngine.Debug.Log(Index + " parent: " + value); m_ParentIndex = value; } }
        [Tooltip("The index of the sibling task within the behavior tree.")]
        public ushort SiblingIndex;
        //public ushort m_SiblingIndex;
        //public ushort SiblingIndex { get { return m_SiblingIndex; } set { UnityEngine.Debug.Log(Index + " sibling: " + value); m_SiblingIndex = value; } }
        [Tooltip("The index of the branch the task executes within.")]
        public ushort BranchIndex;
        //public ushort m_BranchIndex;
        //public ushort BranchIndex { get { return m_BranchIndex; } set { UnityEngine.Debug.Log(Index + " branch: " + value); m_BranchIndex = value; } }
        [Tooltip("The component type responsible for indicating that the task is active.")]
        public ComponentType FlagComponentType;
        [Tooltip("Can the task be reevaluated with conditional aborts?")]
        [MarshalAs(UnmanagedType.U1)]
        public bool CanReevaluate;
        [Tooltip("Is the task being reevaluated with conditional aborts?")]
        [MarshalAs(UnmanagedType.U1)]
        public bool Reevaluate;
        //public bool m_Reevaluate;
        //public bool Reevaluate { get { return m_Reevaluate; } set { UnityEngine.Debug.Log(Index + " reevaluate: " + value + " " + GetHashCode());  m_Reevaluate = value; } }
        [Tooltip("Is the task disabled?")]
        [MarshalAs(UnmanagedType.U1)]
        public bool Disabled;
    }

    /// <summary>
    /// Specifies when the behavior tree should be updated.
    /// </summary>
    public enum UpdateMode
    {
        EveryFrame, // The behavior tree should be updated every frame.
        Manual      // The behavior tree should be updated manually via a user script.
    }

    /// <summary>
    /// Specifies how many tasks should be evaluated. Evaluation will end if all branches return a status of TaskStatus.Running.
    /// </summary>
    public enum EvaluationType : byte
    {
        EntireTree, // Evaluates up to all of the tasks within the tree.
        Count       // Evaluates up to the specified MaxEvaluationCount.
    }

    /// <summary>
    /// Specifies if the tree should be evaluated.
    /// </summary>
    public struct EvaluationComponent32 : IComponentData
    {
        [Tooltip("Specifies how many tasks should be updated during a single tick.")]
        public EvaluationType EvaluationType;
        [Tooltip("The maximum number of tasks that can run if the evaluation type is set to EvaluationType.Count.")]
        public ushort MaxEvaluationCount;
        [Tooltip("Based on the EvaluationType, a mask of the tasks that have been evaluated or the number of tasks that have executed. For EvaluationType.Count, EvaluatedTasks[0] is used as the counter.")]
        public FixedList32Bytes<ulong> EvaluatedTasks;
    }

    /// <summary>
    /// Specifies if the tree should be evaluated.
    /// </summary>
    public struct EvaluationComponent64 : IComponentData
    {
        [Tooltip("Specifies how many tasks should be updated during a single tick.")]
        public EvaluationType EvaluationType;
        [Tooltip("The maximum number of tasks that can run if the evaluation type is set to EvaluationType.Count.")]
        public ushort MaxEvaluationCount;
        [Tooltip("Based on the EvaluationType, a mask of the tasks that have been evaluated or the number of tasks that have executed. For EvaluationType.Count, EvaluatedTasks[0] is used as the counter.")]
        public FixedList64Bytes<ulong> EvaluatedTasks;
    }

    /// <summary>
    /// Specifies if the tree should be evaluated.
    /// </summary>
    public struct EvaluationComponent128 : IComponentData
    {
        [Tooltip("Specifies how many tasks should be updated during a single tick.")]
        public EvaluationType EvaluationType;
        [Tooltip("The maximum number of tasks that can run if the evaluation type is set to EvaluationType.Count.")]
        public ushort MaxEvaluationCount;
        [Tooltip("Based on the EvaluationType, a mask of the tasks that have been evaluated or the number of tasks that have executed. For EvaluationType.Count, EvaluatedTasks[0] is used as the counter.")]
        public FixedList128Bytes<ulong> EvaluatedTasks;
    }

    /// <summary>
    /// Specifies if the tree should be evaluated.
    /// </summary>
    public struct EvaluationComponent512 : IComponentData
    {
        [Tooltip("Specifies how many tasks should be updated during a single tick.")]
        public EvaluationType EvaluationType;
        [Tooltip("The maximum number of tasks that can run if the evaluation type is set to EvaluationType.Count.")]
        public ushort MaxEvaluationCount;
        [Tooltip("Based on the EvaluationType, a mask of the tasks that have been evaluated or the number of tasks that have executed. For EvaluationType.Count, EvaluatedTasks[0] is used as the counter.")]
        public FixedList512Bytes<ulong> EvaluatedTasks;
    }

    /// <summary>
    /// Specifies if the tree should be evaluated.
    /// </summary>
    public struct EvaluationComponent4096 : IComponentData
    {
        [Tooltip("Specifies how many tasks should be updated during a single tick.")]
        public EvaluationType EvaluationType;
        [Tooltip("The maximum number of tasks that can run if the evaluation type is set to EvaluationType.Count.")]
        public ushort MaxEvaluationCount;
        [Tooltip("Based on the EvaluationType, a mask of the tasks that have been evaluated or the number of tasks that have executed. For EvaluationType.Count, EvaluatedTasks[0] is used as the counter.")]
        public FixedList4096Bytes<ulong> EvaluatedTasks;
    }

    /// <summary>
    /// Specifies how the branch was interrupted.
    /// </summary>
    public enum InterruptType : byte
    {
        None,               // No interrupt.
        Branch,             // A conditional abort or utility selector triggered the interruption.
        ImmediateSuccess,   // The branch was interrupted with a success status.
        ImmediateFailure,   // The branch was interrupted with a failure status.
    }

    /// <summary>
    /// The runtime DOTS data associated with a branch.
    /// </summary>
    public struct BranchComponent : IBufferElementData
    {
        [Tooltip("The index of the task that is currently active.")]
        public ushort ActiveIndex;
        //public ushort m_ActiveIndex;
        //public ushort ActiveIndex { get { return m_ActiveIndex; } set { Debug.Log(string.Format("Active: {0}", value)); m_ActiveIndex = value; } }
        [Tooltip("The index of the task that should execute next.")]
        public ushort NextIndex;
        //public ushort m_NextIndex;
        //public ushort NextIndex { get { return m_NextIndex; } set { Debug.Log(string.Format("Next: {0}", value)); m_NextIndex = value; } }
        [Tooltip("The index of the last active task.")]
        public ushort LastActiveIndex;
        [Tooltip("The component tag that is active.")]
        public ComponentType ActiveFlagComponentType;
        //public ComponentType m_ActiveFlagComponentType;
        //public ComponentType ActiveFlagComponentType { get { return m_ActiveFlagComponentType; } set { Debug.Log(string.Format("Tag: {0}", value)); m_ActiveFlagComponentType = value; } }
        [Tooltip("Specifies how the branch is interrupted.")]
        public InterruptType InterruptType;
        //public InterruptType m_InterruptType;
        //public InterruptType InterruptType { get { return m_InterruptType; } set { m_InterruptType = value; Debug.Log("Interrupt Type " + value); } }
        [Tooltip("The index of the task that caused an interruption. A value of 0 indicates no interruption.")]
        public ushort InterruptIndex;
    }

    /// <summary>
    /// Specifies if the tree can be evaluated.
    /// </summary>
    public struct EvaluateFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Specifies if the tree is enabled.
    /// </summary>
    public struct EnabledFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Flag used to indicate when the branch should be interrupted.
    /// </summary>
    public struct InterruptFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Flag used to indicate that the branch has been interrupted.
    /// </summary>
    public struct InterruptedFlag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Specifies the reevaluation status of the task.
    /// </summary>
    public enum ReevaluateStatus : byte
    {
        Inactive,   // The task is not being reevaluated.
        Active,     // The task is currently being reevaluated.
        Dirty       // The task was reevaluated and triggered a change.
    }

    /// <summary>
    /// The runtime DOTS data associated with conditional aborts.
    /// </summary>
    public struct ReevaluateTaskComponent : IBufferElementData
    {
        [Tooltip("The index of the task.")]
        public ushort Index;
        [Tooltip("The type of conditional abort.")]
        public ConditionalAbortType AbortType;
        [Tooltip("The lower bound index of the next task if a lower priority abort is specified.")]
        public ushort LowerPriorityLowerIndex;
        [Tooltip("The upper bound index of the next task if a lower priority abort is specified.")]
        public ushort LowerPriorityUpperIndex;
        [Tooltip("The upper bound index of the next task if a self priority abort is specified.")]
        public ushort SelfPriorityUpperIndex;
        [Tooltip("The original status of the task.")]
        public TaskStatus OriginalStatus;
        [Tooltip("The tag specifiying the task should be reevaluated.")]
        public ComponentType ReevaluateFlagComponentType;
        [Tooltip("The current reevaluation status of the task.")]
        public ReevaluateStatus ReevaluateStatus;
    }

    /// <summary>
    /// Runtime representation of IEventNodes that can run its own entity logic.
    /// </summary>
    public interface IEventNodeEntityReceiver
    {
        /// <summary>
        /// Adds the IBufferElementData to the entity.
        /// </summary>
        /// <param name="world">The world that the entity exists in.</param>
        /// <param name="entity">The entity that the IBufferElementData should be assigned to.</param>
        /// <param name="gameObject">The GameObject that the entity is attached to.</param>
        /// <param name="taskOffset">The offset between the connected index and the runtime index.</param>
        void AddBufferElement(World world, Entity entity, GameObject gameObject, ushort taskOffset);

        /// <summary>
        /// Clears the IBufferElementData from the entity.
        /// </summary>
        /// <param name="world">The world that the entity exists in.</param>
        /// <param name="entity">The entity that the IBufferElementData should be cleared from.</param>
        void ClearBufferElement(World world, Entity entity);
    }
}
#endif