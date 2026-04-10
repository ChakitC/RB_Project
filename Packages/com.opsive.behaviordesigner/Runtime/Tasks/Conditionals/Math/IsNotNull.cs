#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.Math
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Math")]
    [Opsive.Shared.Utility.Description("Checks if a SharedVariable value is not null.")]
    public class IsNotNull : Conditional
    {
        [Tooltip("The SharedVariable to check for not null.")]
        [SerializeField] protected SharedVariable m_Variable;

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            return m_Variable.GetValue() != null ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}
#endif