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
    [Opsive.Shared.Utility.Description("Checks if an element is in the list.")]
    public class IsElementInList : Conditional
    {
        [Tooltip("The element to check for.")]
        [SerializeField] protected SharedVariable m_Element;
        [Tooltip("The list of possible elements.")]
        [SerializeField] protected SharedVariable m_List;

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            var elementValue = m_Element.GetValue();
            var listValue = m_List.GetValue() as IList;
            var valid = listValue != null && elementValue != null && listValue.Contains(elementValue);
            return valid ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}
#endif