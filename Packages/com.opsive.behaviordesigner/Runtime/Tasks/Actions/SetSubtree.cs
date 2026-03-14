#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions
{
    using Opsive.GraphDesigner.Runtime;
    using System.Collections;
    using UnityEngine;

    [NodeIcon("e0a8f1df788b6274a9a24003859dfa7e")]
    [Opsive.Shared.Utility.Description("Sets the specified subtree.")]
    public class SetSubtree : TargetBehaviorTreeAction
    {
        [Tooltip("The subtree that should be set.")]
        [SerializeField] protected Subtree m_Subtree;
        [Tooltip("Should the behavior tree be started after the subtree is set?")]
        [SerializeField] protected bool m_StartBehavior = true;

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
                return m_Status;
            }

            if (m_ResolvedBehaviorTree == null) {
                return TaskStatus.Failure;
            }

            m_Status = TaskStatus.Running;
            StartCoroutine(DoSetSubtree());
            return m_Status;
        }

        /// <summary>
        /// Sets the subtree using a coroutine to allow structural changes.
        /// </summary>
        private IEnumerator DoSetSubtree()
        {
            yield return new WaitForEndOfFrame();

            m_ResolvedBehaviorTree.Subgraph = m_Subtree;
            m_Status = TaskStatus.Success;

            if (m_StartBehavior) {
                m_ResolvedBehaviorTree.StartBehavior();
            }
        }
    }
}
#endif