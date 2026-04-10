#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.GameObjectTasks
{
    using Opsive.GraphDesigner.Runtime;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("GameObject")]
    [Opsive.Shared.Utility.Description("Checks if a GameObject is on a specific layer using LayerMask.")]
    public class CompareLayer : TargetGameObjectConditional
    {
        [Tooltip("The layer mask to compare against.")]
        [SerializeField] protected LayerMask m_LayerMask = -1;

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            return ((1 << gameObject.layer) & m_LayerMask) != 0 ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}
#endif