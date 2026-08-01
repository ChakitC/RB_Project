#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.GraphDesigner.Runtime.Utility;
    using UnityEngine;

    /// <summary>
    /// Executes the specified event.
    /// </summary>
    [NodeIcon("bde76446ddfbd234488e8d591bc75e2f", "6d03b96c0f79bee4ab2e14fc82aa0031")]
    [Opsive.Shared.Utility.Description("Sends an event to the behavior tree, returns success after sending the event.")]
    public class SendEvent : TargetBehaviorTreeAction
    {
        [Tooltip("The name of the event.")]
        [SerializeField] protected SharedVariable<string> m_EventName;
        [Tooltip("Is the event a global event?")]
        [SerializeField] protected SharedVariable<bool> m_GlobalEvent;
        [Tooltip("Optionally specify a first argument to send.")]
        [RequireShared] [SerializeField] protected SharedVariable m_Argument1;
        [Tooltip("Optionally specify a second argument to send.")]
        [RequireShared] [SerializeField] protected SharedVariable m_Argument2;
        [Tooltip("Optionally specify a third argument to send.")]
        [RequireShared] [SerializeField] protected SharedVariable m_Argument3;

        private SharedVariableEventHandler m_SharedVariableEventHandler;

        /// <summary>
        /// Executes the event.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedBehaviorTree == null) {
                return TaskStatus.Failure;
            }

            if (string.IsNullOrEmpty(m_EventName.Value)) {
                UnityEngine.Debug.LogError("Error: Unable to send event. The event name is empty.");
                return TaskStatus.Failure;
            }

            if (m_SharedVariableEventHandler == null || !m_SharedVariableEventHandler.Matches(m_Argument1, m_Argument2, m_Argument3)) {
                m_SharedVariableEventHandler = SharedVariableEventHandler.Create(m_Argument1, m_Argument2, m_Argument3);
            }
            m_SharedVariableEventHandler.Execute(m_ResolvedBehaviorTree, m_EventName.Value, m_GlobalEvent.Value);

            return TaskStatus.Success;
        }
    }
}
#endif