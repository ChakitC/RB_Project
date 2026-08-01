#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.GraphDesigner.Runtime.Utility;
    using UnityEngine;

    /// <summary>
    /// A TaskObject implementation of the Conditional task. This class can be used when the task should not be grouped by the StackedConditional task.
    /// </summary>
    [NodeIcon("e6fc90c130121da4f9067b5e15b02975", "69959064b54a0cb4cb077dbb6967a3e1")]
    [Opsive.Shared.Utility.Description("Returns success as soon as the event specified by eventName has been received.")]
    public class HasReceivedEvent : TargetBehaviorTreeConditional
    {
        [Tooltip("The name of the event that should be registered.")]
        [SerializeField] protected SharedVariable<string> m_EventName;
        [Tooltip("Is the event a global event?")]
        [SerializeField] protected SharedVariable<bool> m_GlobalEvent;
        [Tooltip("Optionally store the first sent argument.")]
        [RequireShared] [SerializeField] protected SharedVariable m_StoredValue1;
        [Tooltip("Optionally store the second sent argument.")]
        [RequireShared] [SerializeField] protected SharedVariable m_StoredValue2;
        [Tooltip("Optionally store the third sent argument.")]
        [RequireShared] [SerializeField] protected SharedVariable m_StoredValue3;

        private SharedVariableEventHandler m_SharedVariableEventHandler;
        private bool m_EventRegistered;
        private bool m_EventReceived;
        private bool m_ResetEventReceived = true;

        /// <summary>
        /// The behavior tree has started.
        /// </summary>
        public override void OnBehaviorTreeStarted()
        {
            base.OnBehaviorTreeStarted();

            RegisterEvents();
        }

        /// <summary>
        /// Initializes the target behavior tree.
        /// </summary>
        protected override void InitializeTarget()
        {
            if (m_ResolvedBehaviorTree != null) {
                UnregisterEvents();
            }

            base.InitializeTarget();

            RegisterEvents();
        }

        /// <summary>
        /// Registers for the events.
        /// </summary>
        private void RegisterEvents()
        {
            if (m_EventRegistered) {
                return;
            }

            if (string.IsNullOrEmpty(m_EventName.Value)) {
                Debug.LogError("Error: Unable to receive event. The event name is empty.");
                return;
            }

            m_SharedVariableEventHandler = SharedVariableEventHandler.Create(m_StoredValue1, m_StoredValue2, m_StoredValue3, ReceivedEvent);
            m_SharedVariableEventHandler.Register(m_ResolvedBehaviorTree, m_EventName.Value, m_GlobalEvent.Value);

            m_EventName.OnValueChange += UpdateEvents;
            m_GlobalEvent.OnValueChange += UpdateEvents;
            if (m_StoredValue1 != null) { m_StoredValue1.OnValueChange += UpdateEvents; }
            if (m_StoredValue2 != null) { m_StoredValue2.OnValueChange += UpdateEvents; }
            if (m_StoredValue3 != null) { m_StoredValue3.OnValueChange += UpdateEvents; }

            m_EventRegistered = true;
        }

        /// <summary>
        /// The event name or parameter count has changed. Update the events.
        /// </summary>
        private void UpdateEvents()
        {
            UnregisterEvents();
            RegisterEvents();
        }

        /// <summary>
        /// A parameterless event has been recevied.
        /// </summary>
        private void ReceivedEvent()
        {
            m_EventReceived = true;
        }

        /// <summary>
        /// Callback when the task is started.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            if (m_ResetEventReceived) {
                m_EventReceived = false;
            }
        }

        /// <summary>
        /// The task has been updated.
        /// </summary>
        /// <returns>True if an event has been received.</returns>
        public override TaskStatus OnUpdate()
        {
            return m_EventReceived ? TaskStatus.Success : TaskStatus.Failure;
        }

        /// <summary>
        /// Reevaluates the task logic.
        /// </summary>
        /// <returns>The status of the task during the reevaluation phase.</returns>
        public override TaskStatus OnReevaluateUpdate()
        {
            if (m_EventReceived) {
                // OnStart/OnUpdate will be called immediately after the task is reevaluated. Do not reset the receive status.
                m_ResetEventReceived = false;
                return TaskStatus.Success;
            }
            return TaskStatus.Failure;
        }

        /// <summary>
        /// The task has ended.
        /// </summary>
        public override void OnEnd()
        {
            base.OnEnd();

            m_EventReceived = false;
            m_ResetEventReceived = true;
        }

        /// <summary>
        /// The behavior tree has been stopped.
        /// </summary>
        /// <param name="paused">Is the behavior tree paused?</param>
        public override void OnBehaviorTreeStopped(bool paused)
        {
            base.OnBehaviorTreeStopped(paused);

            UnregisterEvents();
            m_EventReceived = false;
            m_ResetEventReceived = true;
        }

        /// <summary>
        /// Unregisters for the events that were registered.
        /// </summary>
        private void UnregisterEvents()
        {
            if (!m_EventRegistered) {
                return;
            }

            m_SharedVariableEventHandler.Unregister();
            m_SharedVariableEventHandler = null;

            m_EventName.OnValueChange -= UpdateEvents;
            m_GlobalEvent.OnValueChange -= UpdateEvents;
            if (m_StoredValue1 != null) { m_StoredValue1.OnValueChange -= UpdateEvents; }
            if (m_StoredValue2 != null) { m_StoredValue2.OnValueChange -= UpdateEvents; }
            if (m_StoredValue3 != null) { m_StoredValue3.OnValueChange -= UpdateEvents; }

            m_EventRegistered = false;
        }
    }
}
#endif