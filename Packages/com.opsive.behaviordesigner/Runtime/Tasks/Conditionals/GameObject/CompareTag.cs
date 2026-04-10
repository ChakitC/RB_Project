#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.GameObjectTasks
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("GameObject")]
    [Opsive.Shared.Utility.Description("Checks if a GameObject has a specific tag.")]
    public class CompareTag : TargetGameObjectConditional
    {
        [Tooltip("The tag to compare against.")]
        [SerializeField] protected SharedVariable<string> m_Tag;

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            if (string.IsNullOrEmpty(m_Tag.Value)) {
                return TaskStatus.Failure;
            }

            return gameObject.CompareTag(m_Tag.Value) ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}
#endif