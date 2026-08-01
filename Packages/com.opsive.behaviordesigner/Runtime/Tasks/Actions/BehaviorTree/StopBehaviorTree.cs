#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.BehaviorTree
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;
    using UnityEngine.Scripting.APIUpdating;

    [NodeIcon("e0a8f1df788b6274a9a24003859dfa7e")]
    [Opsive.Shared.Utility.Description("Stops the specified behavior tree.")]
    [MovedFrom(false, "Opsive.BehaviorDesigner.Runtime.Tasks.Actions", "Opsive.BehaviorDesigner.Runtime", "StopBehaviorTree")]
    public class StopBehaviorTree : TargetBehaviorTreeAction
    {
        [SerializeField] protected SharedVariable<bool> m_PauseBehaviorTree;
        /// <summary>
        /// Executes the task logic.
        /// </summary>
        /// <returns>The status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedBehaviorTree == null || !m_ResolvedBehaviorTree.IsActive()) {
                return TaskStatus.Failure;
            }

            return m_ResolvedBehaviorTree.StopBehavior(m_PauseBehaviorTree.Value) ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}
#endif