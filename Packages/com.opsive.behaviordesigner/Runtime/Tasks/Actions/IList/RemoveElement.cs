#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.IList
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("IList")]
    [Opsive.Shared.Utility.Description("Removes the element from the list.")]
    public class RemoveElement: Action
    {
        [Tooltip("The element to remove from the list.")]
        [SerializeField] protected SharedVariable m_Element;
        [Tooltip("The list that the element should be removed from.")]
        [RequireShared] [SerializeField] protected SharedVariable m_List;

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var listValue = m_List.GetValue() as IList;
            if (listValue == null) {
                return TaskStatus.Success;
            }

            var elementValue = m_Element.GetValue();
            listValue.Remove(elementValue);
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Element = null;
            m_List = null;
        }
    }
}
#endif