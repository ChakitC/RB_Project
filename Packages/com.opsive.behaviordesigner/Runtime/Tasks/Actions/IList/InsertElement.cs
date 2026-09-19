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

    /// <summary>
    /// Inserts the element at the specified list index.
    /// </summary>
    [Opsive.Shared.Utility.Category("IList")]
    [Opsive.Shared.Utility.Description("Inserts the element at the specified list index.")]
    public class InsertElement : Action
    {
        [Tooltip("The list that the element should be inserted into.")]
        [RequireShared] [SerializeField] protected SharedVariable m_List;
        [Tooltip("The index at which the element should be inserted.")]
        [SerializeField] protected SharedVariable<int> m_ElementIndex;
        [Tooltip("The element that should be inserted into the list.")]
        [SerializeField] protected SharedVariable m_Element;

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var listValue = m_List.GetValue() as IList;
            if (listValue == null || listValue.IsReadOnly || listValue.IsFixedSize || m_ElementIndex.Value < 0 || m_ElementIndex.Value > listValue.Count) {
                return TaskStatus.Success;
            }

            listValue.Insert(m_ElementIndex.Value, m_Element.GetValue());
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_List = null;
            m_ElementIndex = null;
            m_Element = null;
        }
    }
}
#endif