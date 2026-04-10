#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.IListTasks
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("IList")]
    [Opsive.Shared.Utility.Description("Checks if the list is empty.")]
    public class IsEmpty : Conditional
    {
        [Tooltip("The list that should be checked.")]
        [SerializeField] protected SharedVariable m_List;

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            var listValue = m_List.GetValue() as IList;
            return (listValue == null || listValue.Count == 0) ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}
#endif