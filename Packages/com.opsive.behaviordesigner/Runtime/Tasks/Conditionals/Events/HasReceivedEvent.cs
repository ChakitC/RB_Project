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
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// A TaskObject implementation of the Conditional task. This class can be used when the task should not be grouped by the StackedConditional task.
    /// </summary>
    [NodeIcon("e6fc90c130121da4f9067b5e15b02975", "69959064b54a0cb4cb077dbb6967a3e1")]
    [Opsive.Shared.Utility.Description("Returns success as soon as the event specified by eventName has been received.")]
    public class HasReceivedEvent : TargetBehaviorTreeConditional
    {
        /// <summary>
        /// Specifies how received events should be retained until the task is evaluated.
        /// </summary>
        public enum ReceiveMode : byte
        {
            Latest, // Retain only whether at least one event has been received.
            Queued  // Retain every event and its argument values in receive order until consumed or the behavior tree stops.
        }

        /// <summary>
        /// Specifies which event should be discarded when a bounded queue is full.
        /// </summary>
        public enum QueueOverflowPolicy : byte
        {
            DropOldest, // Discard the oldest pending event and enqueue the newly received event.
            DropNewest  // Discard the newly received event and preserve the existing queue.
        }

        /// <summary>
        /// Stores the argument values for a queued event.
        /// </summary>
        private struct ReceivedEventData
        {
            private byte m_ValueCount;
            private object m_Value1;
            private object m_Value2;
            private object m_Value3;

            /// <summary>
            /// Initializes a queued event from the received argument variables.
            /// </summary>
            /// <param name="value1">The first received argument.</param>
            /// <param name="value2">The second received argument.</param>
            /// <param name="value3">The third received argument.</param>
            public ReceivedEventData(SharedVariable value1, SharedVariable value2, SharedVariable value3)
            {
                m_ValueCount = 0;
                m_Value1 = null;
                m_Value2 = null;
                m_Value3 = null;

                if (value1 == null || !value1.IsShared) {
                    return;
                }
                m_ValueCount = 1;
                m_Value1 = value1.GetValue();

                if (value2 == null || !value2.IsShared) {
                    return;
                }
                m_ValueCount = 2;
                m_Value2 = value2.GetValue();

                if (value3 == null || !value3.IsShared) {
                    return;
                }
                m_ValueCount = 3;
                m_Value3 = value3.GetValue();
            }

            /// <summary>
            /// Restores the queued argument values to the configured storage variables.
            /// </summary>
            /// <param name="value1">The first argument storage variable.</param>
            /// <param name="value2">The second argument storage variable.</param>
            /// <param name="value3">The third argument storage variable.</param>
            public void Restore(SharedVariable value1, SharedVariable value2, SharedVariable value3)
            {
                if (m_ValueCount > 0 && value1 != null) {
                    value1.SetValue(m_Value1);
                }
                if (m_ValueCount > 1 && value2 != null) {
                    value2.SetValue(m_Value2);
                }
                if (m_ValueCount > 2 && value3 != null) {
                    value3.SetValue(m_Value3);
                }
            }
        }

        [Tooltip("The name of the event that should be registered.")]
        [SerializeField] protected SharedVariable<string> m_EventName;
        [Tooltip("Is the event a global event?")]
        [SerializeField] protected SharedVariable<bool> m_GlobalEvent;
        [Tooltip("Specifies whether pending events are coalesced to the latest event or queued across task executions.")]
        [SerializeField] protected ReceiveMode m_ReceiveMode;
        [Tooltip("The maximum number of pending events. Set to 0 for an unlimited queue.")]
        [Min(0)] [SerializeField] protected int m_MaxQueueSize;
        [Tooltip("Specifies which event should be discarded when the queue is full.")]
        [SerializeField] protected QueueOverflowPolicy m_QueueOverflowPolicy;
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
        private bool m_UpdatingStoredValues;
        private Queue<ReceivedEventData> m_ReceivedEventQueue;

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
                ClearReceivedEvents();
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

            m_SharedVariableEventHandler = SharedVariableEventHandler.Create(m_StoredValue1, m_StoredValue2, m_StoredValue3, PrepareToReceiveEvent, ReceivedEvent);
            m_SharedVariableEventHandler.Register(m_ResolvedBehaviorTree, m_EventName.Value, m_GlobalEvent.Value);

            m_EventName.OnValueChange += UpdateEvents;
            m_GlobalEvent.OnValueChange += UpdateEvents;
            if (m_StoredValue1 != null) { m_StoredValue1.OnValueChange += UpdateEventArguments; }
            if (m_StoredValue2 != null) { m_StoredValue2.OnValueChange += UpdateEventArguments; }
            if (m_StoredValue3 != null) { m_StoredValue3.OnValueChange += UpdateEventArguments; }

            m_EventRegistered = true;
        }

        /// <summary>
        /// The event name or parameter count has changed. Update the events.
        /// </summary>
        private void UpdateEvents()
        {
            ClearReceivedEvents();
            UpdateEventArguments();
        }

        /// <summary>
        /// Recreates the event handler when the argument signature changes.
        /// </summary>
        private void UpdateEventArguments()
        {
            if (m_UpdatingStoredValues) {
                return;
            }

            UnregisterEvents();
            RegisterEvents();
        }

        /// <summary>
        /// Prevents received argument values from changing the registered event signature.
        /// </summary>
        private void PrepareToReceiveEvent()
        {
            m_UpdatingStoredValues = true;
        }

        /// <summary>
        /// An event has been received and its arguments have been stored.
        /// </summary>
        private void ReceivedEvent()
        {
            try {
                if (m_ReceiveMode == ReceiveMode.Queued) {
                    EnqueueReceivedEvent();
                } else {
                    m_EventReceived = true;
                }
            } finally {
                m_UpdatingStoredValues = false;
            }
        }

        /// <summary>
        /// Enqueues the received event while applying the configured overflow policy.
        /// </summary>
        private void EnqueueReceivedEvent()
        {
            if (m_ReceivedEventQueue == null) {
                m_ReceivedEventQueue = new Queue<ReceivedEventData>();
            }

            if (m_MaxQueueSize > 0 && m_ReceivedEventQueue.Count >= m_MaxQueueSize) {
                if (m_QueueOverflowPolicy == QueueOverflowPolicy.DropNewest) {
                    return;
                }

                while (m_ReceivedEventQueue.Count >= m_MaxQueueSize) {
                    m_ReceivedEventQueue.Dequeue();
                }
            }

            m_ReceivedEventQueue.Enqueue(new ReceivedEventData(m_StoredValue1, m_StoredValue2, m_StoredValue3));
        }

        /// <summary>
        /// Returns whether an event is waiting to be consumed.
        /// </summary>
        /// <returns>True if an event is waiting to be consumed.</returns>
        private bool HasPendingEvent()
        {
            return m_ReceiveMode == ReceiveMode.Queued
                ? m_ReceivedEventQueue != null && m_ReceivedEventQueue.Count > 0
                : m_EventReceived;
        }

        /// <summary>
        /// Consumes the next received event.
        /// </summary>
        /// <returns>True if an event was consumed.</returns>
        private bool ConsumeReceivedEvent()
        {
            if (m_ReceiveMode == ReceiveMode.Queued) {
                if (m_ReceivedEventQueue == null || m_ReceivedEventQueue.Count == 0) {
                    return false;
                }

                var receivedEvent = m_ReceivedEventQueue.Dequeue();
                m_UpdatingStoredValues = true;
                try {
                    receivedEvent.Restore(m_StoredValue1, m_StoredValue2, m_StoredValue3);
                } finally {
                    m_UpdatingStoredValues = false;
                }
                return true;
            }

            if (m_EventReceived) {
                m_EventReceived = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears all received event state.
        /// </summary>
        private void ClearReceivedEvents()
        {
            m_EventReceived = false;
            m_ReceivedEventQueue?.Clear();
        }

        /// <summary>
        /// Callback when the task is started.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            if (m_ResetEventReceived && m_ReceiveMode == ReceiveMode.Latest) {
                m_EventReceived = false;
            }
        }

        /// <summary>
        /// The task has been updated.
        /// </summary>
        /// <returns>True if an event has been received.</returns>
        public override TaskStatus OnUpdate()
        {
            return ConsumeReceivedEvent() ? TaskStatus.Success : TaskStatus.Failure;
        }

        /// <summary>
        /// Reevaluates the task logic.
        /// </summary>
        /// <returns>The status of the task during the reevaluation phase.</returns>
        public override TaskStatus OnReevaluateUpdate()
        {
            if (HasPendingEvent()) {
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

            if (m_ReceiveMode == ReceiveMode.Latest) {
                m_EventReceived = false;
            }
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
            ClearReceivedEvents();
            m_ResetEventReceived = true;
        }

        /// <summary>
        /// The behavior tree has been destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();

            UnregisterEvents();
            ClearReceivedEvents();
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
            if (m_StoredValue1 != null) { m_StoredValue1.OnValueChange -= UpdateEventArguments; }
            if (m_StoredValue2 != null) { m_StoredValue2.OnValueChange -= UpdateEventArguments; }
            if (m_StoredValue3 != null) { m_StoredValue3.OnValueChange -= UpdateEventArguments; }

            m_EventRegistered = false;
        }
    }
}
#endif