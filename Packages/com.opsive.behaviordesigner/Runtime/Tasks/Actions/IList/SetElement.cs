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
    /// Sets the element at the specified list index.
    /// </summary>
    [Opsive.Shared.Utility.Category("IList")]
    [Opsive.Shared.Utility.Description("Sets the element at the specified list index.")]
    public class SetElement : Action
    {
        [Tooltip("The list whose element should be set.")]
        [RequireShared] [SerializeField] protected SharedVariable m_List;
        [Tooltip("The index of the element that should be set.")]
        [SerializeField] protected SharedVariable<int> m_ElementIndex;
        [Tooltip("The value that should be assigned to the element.")]
        [SerializeField] protected SharedVariable m_Element;

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var listValue = m_List.GetValue() as IList;
            if (listValue == null || listValue.IsReadOnly || m_ElementIndex.Value < 0 || m_ElementIndex.Value >= listValue.Count) {
                return TaskStatus.Success;
            }

            listValue[m_ElementIndex.Value] = m_Element.GetValue();
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