#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.BehaviorTree
{
    using Opsive.GraphDesigner.Runtime;
    using System.Collections;
    using UnityEngine;
    using UnityEngine.Scripting.APIUpdating;

    [NodeIcon("e0a8f1df788b6274a9a24003859dfa7e")]
    [Opsive.Shared.Utility.Description("Starts the specified behavior tree.")]
    [MovedFrom(false, "Opsive.BehaviorDesigner.Runtime.Tasks.Actions", "Opsive.BehaviorDesigner.Runtime", "StartBehaviorTree")]
    public class StartBehaviorTree : TargetBehaviorTreeAction
    {
        [Tooltip("Wait for the behaviro tree to complete before returning Success.")]
        [SerializeField] protected bool m_WaitForCompletion;

        private TaskStatus m_Status;

        /// <summary>
        /// The task has started.
        /// </summary>
        public override void OnStart()
        {
            m_Status = TaskStatus.Queued;
        }

        /// <summary>
        /// Executes the task logic.
        /// </summary>
        /// <returns>The status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            // The coroutine has already been started if the status is not queued.
            if (m_Status != TaskStatus.Queued) {
                if (m_WaitForCompletion && m_Status == TaskStatus.Running && !m_ResolvedBehaviorTree.IsRunning()) {
                    m_Status = m_ResolvedBehaviorTree.Status;
                }
                return m_Status;
            }

            if (m_ResolvedBehaviorTree == null || m_ResolvedBehaviorTree.IsRunning()) {
                return TaskStatus.Failure;
            }

            m_Status = TaskStatus.Running;
            StartCoroutine(StartBehavior());
            return m_Status;
        }

        /// <summary>
        /// Starts the behavior tree using a coroutine to allow structural changes.
        /// </summary>
        private IEnumerator StartBehavior()
        {
            yield return new WaitForEndOfFrame();

            if (m_ResolvedBehaviorTree.StartBehavior()) {
                if (!m_WaitForCompletion) {
                    m_Status = TaskStatus.Success;
                }
            } else {
                m_Status = TaskStatus.Failure;
            }
        }
    }
}
#endif