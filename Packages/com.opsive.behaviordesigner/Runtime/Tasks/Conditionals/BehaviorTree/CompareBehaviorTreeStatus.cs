#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.BehaviorTree
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Behavior Tree")]
    [Opsive.Shared.Utility.Description("Checks if another behavior tree is in a specific status.")]
    public class CompareBehaviorTreeStatus : TargetBehaviorTreeConditional
    {
        [Tooltip("The status that should be checked.")]
        [SerializeField] protected SharedVariable<TaskStatus> m_TaskStatus;

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedBehaviorTree == null) {
                return TaskStatus.Failure;
            }

            return m_ResolvedBehaviorTree.Status == m_TaskStatus.Value ? TaskStatus.Success : TaskStatus.Failure;
        }

        /// <summary>
        /// Resets the conditional values.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_TaskStatus = TaskStatus.Inactive;
        }
    }
}
#endif