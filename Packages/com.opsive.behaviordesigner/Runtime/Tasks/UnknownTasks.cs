#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks
{
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Composites;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.Shared.Utility;
    using UnityEngine;

    /// <summary>
    /// Represents a task node that can no longer be found.
    /// </summary>
    [HideInFilterWindow]
    public class UnknownTaskNode : ActionNode, IUnknownObject
    {
        [Tooltip("The original type name that could not be found.")]
        [SerializeField] [HideInInspector] private string m_UnknownType;

        /// <summary>
        /// Gets the original type name that could not be found.
        /// </summary>
        public string UnknownType => m_UnknownType;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public UnknownTaskNode() { }

        /// <summary>
        /// UnknownTaskNode constructor.
        /// </summary>
        /// <param name="unknownType">The type that cannot be found.</param>
        public UnknownTaskNode(string unknownType)
        {
            m_UnknownType = unknownType;
        }

        /// <summary>
        /// Called once when the behavior tree is initialized.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();

            Debug.LogWarning($"Warning: Unable to find the task of type {m_UnknownType}. An unknown task has been replaced with it.");
        }
    }

    /// <summary>
    /// Represents a task that can no longer be found.
    /// </summary>
    [HideInFilterWindow]
    public class UnknownTask : Action, IUnknownObject
    {
        [Tooltip("The original type name that could not be found.")]
        [SerializeField] [HideInInspector] private string m_UnknownType;

        /// <summary>
        /// Gets the original type name that could not be found.
        /// </summary>
        public string UnknownType => m_UnknownType;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public UnknownTask() { }

        /// <summary>
        /// UnknownTask constructor.
        /// </summary>
        /// <param name="unknownType">The type that cannot be found.</param>
        public UnknownTask(string unknownType)
        {
            m_UnknownType = unknownType;
        }

        /// <summary>
        /// Called once when the behavior tree is initialized.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();

            var typeMessage = string.IsNullOrEmpty(m_UnknownType) ? "the original task" : $"the task of type {m_UnknownType}";
            Debug.LogWarning($"Warning: Unable to find {typeMessage}. An unknown task has been replaced with it.");
        }
    }

    /// <summary>
    /// Represents a parent task node that can no longer be found.
    /// </summary>
    [HideInFilterWindow]
    public class UnknownParentTaskNode : CompositeNode, IUnknownObject
    {
        [Tooltip("The original type name that could not be found.")]
        [SerializeField] [HideInInspector] private string m_UnknownType;

        /// <summary>
        /// Gets the original type name that could not be found.
        /// </summary>
        public string UnknownType => m_UnknownType;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public UnknownParentTaskNode() { }

        /// <summary>
        /// UnknownParentTaskNode constructor.
        /// </summary>
        /// <param name="unknownType">The type that cannot be found.</param>
        public UnknownParentTaskNode(string unknownType)
        {
            m_UnknownType = unknownType;
        }

        /// <summary>
        /// Called once when the behavior tree is initialized.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();

            Debug.LogWarning($"Warning: Unable to find the task of type {m_UnknownType}. An unknown task has been replaced with it.");
        }
    }

    /// <summary>
    /// Represents an event node that can no longer be found.
    /// </summary>
    [HideInFilterWindow]
    public class UnknownEventTask : IEventNode, IUnknownObject
    {
        [Tooltip("Specifies if the node is enabled.")]
        [SerializeField] [HideInInspector] protected bool m_Enabled = true;
        [Tooltip("The original type name that could not be found.")]
        [SerializeField] [HideInInspector] private string m_UnknownType;

        public ushort Index { get; set; }
        public ushort ConnectedIndex { get => ushort.MaxValue; set { } }
        public bool Enabled { get => m_Enabled; set => m_Enabled = value; }
        public string UnknownType => m_UnknownType;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public UnknownEventTask() { }

        /// <summary>
        /// UnknownEventTask constructor.
        /// </summary>
        /// <param name="unknownType">The type that cannot be found.</param>
        public UnknownEventTask(string unknownType)
        {
            m_UnknownType = unknownType;
        }
    }
}
#endif