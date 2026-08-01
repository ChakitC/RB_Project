#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Events
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.GraphDesigner.Runtime.Utility;
    using UnityEngine;

    [AllowMultipleTypes]
    [Opsive.Shared.Utility.Description("Invoked when the specified event is received.")]
    public class OnReceivedEvent : EventNode
    {
        [Tooltip("The name of the event that starts the branch.")]
        [SerializeField] protected SharedVariable<string> m_EventName;
        [Tooltip("Optionally store the first sent argument.")]
        [RequireShared] [SerializeField] protected SharedVariable m_StoredValue1;
        [Tooltip("Optionally store the second sent argument.")]
        [RequireShared] [SerializeField] protected SharedVariable m_StoredValue2;
        [Tooltip("Optionally store the third sent argument.")]
        [RequireShared] [SerializeField] protected SharedVariable m_StoredValue3;

        private SharedVariableEventHandler m_SharedVariableEventHandler;
        private bool m_Initialized;

        /// <summary>
        /// Initializes the node to the specified graph.
        /// </summary>
        /// <param name="graph">The graph that is initializing the task.</param>
        public override void Initialize(IGraph graph)
        {
            if (m_Initialized) {
                return;
            }
            m_Initialized = true;

            base.Initialize(graph);

            m_BehaviorTree.OnBehaviorTreeDestroyed += Destroy;

            m_EventName.OnValueChange += UpdateEvents;
            if (m_StoredValue1 != null) { m_StoredValue1.OnValueChange += UpdateEvents; }
            if (m_StoredValue2 != null) { m_StoredValue2.OnValueChange += UpdateEvents; }
            if (m_StoredValue3 != null) { m_StoredValue3.OnValueChange += UpdateEvents; }

            RegisterEvents();
        }

        /// <summary>
        /// Registers for the events.
        /// </summary>
        private void RegisterEvents()
        {
            m_SharedVariableEventHandler = SharedVariableEventHandler.Create(m_StoredValue1, m_StoredValue2, m_StoredValue3, afterEvent: ReceivedEvent);
            m_SharedVariableEventHandler.Register(m_BehaviorTree, m_EventName.Value, false);
        }

        /// <summary>
        /// Unregisters for the events that were registered.
        /// </summary>
        private void UnregisterEvents()
        {
            if (m_SharedVariableEventHandler == null) {
                return;
            }

            m_SharedVariableEventHandler.Unregister();
            m_SharedVariableEventHandler = null;
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
        /// The event has been received.
        /// </summary>
        private void ReceivedEvent()
        {
            m_BehaviorTree.StartBranch(this);
        }

        /// <summary>
        /// The behavior tree has been destroyed.
        /// </summary>
        private void Destroy()
        {
            m_BehaviorTree.OnBehaviorTreeDestroyed -= Destroy;

            m_EventName.OnValueChange -= UpdateEvents;
            if (m_StoredValue1 != null) { m_StoredValue1.OnValueChange -= UpdateEvents; }
            if (m_StoredValue2 != null) { m_StoredValue2.OnValueChange -= UpdateEvents; }
            if (m_StoredValue3 != null) { m_StoredValue3.OnValueChange -= UpdateEvents; }

            UnregisterEvents();
            m_Initialized = false;
        }
    }
}
#endif