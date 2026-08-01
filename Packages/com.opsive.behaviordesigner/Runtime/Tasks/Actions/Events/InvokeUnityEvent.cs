#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions
{
    using Opsive.GraphDesigner.Runtime;
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// Invokes a UnityEvent.
    /// </summary>
    [NodeIcon("bde76446ddfbd234488e8d591bc75e2f", "6d03b96c0f79bee4ab2e14fc82aa0031")]
    [Opsive.Shared.Utility.Description("Invokes a UnityEvent and returns success.")]
    public class InvokeUnityEvent : Action
    {
        [Tooltip("The UnityEvent that should be invoked.")]
        [SerializeField] protected UnityEvent m_UnityEvent = new UnityEvent();

        /// <summary>
        /// Resets the task values back to their default.
        /// </summary>
        public override void Reset()
        {
            m_UnityEvent = new UnityEvent();
        }

        /// <summary>
        /// Invokes the UnityEvent.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_UnityEvent != null) {
                m_UnityEvent.Invoke();
            }

            return TaskStatus.Success;
        }
    }
}
#endif