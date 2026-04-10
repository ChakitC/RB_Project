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
    [Opsive.Shared.Utility.Description("Gets the index of the element within the list.")]
    public class GetElementIndex : Action
    {
        [Tooltip("The element that should be found.")]
        [SerializeField] protected SharedVariable m_Element;
        [Tooltip("The list of elements.")]
        [SerializeField] protected SharedVariable m_List;
        [Tooltip("The index of the element in the list (-1 if not found).")]
        [RequireShared] [SerializeField] protected SharedVariable<int> m_StoreResult;

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var elementValue = m_Element.GetValue();
            if (elementValue == null) {
                m_StoreResult.Value = -1;
                return TaskStatus.Success;
            }

            var listValue = m_List.GetValue() as IList;
            if (listValue == null) {
                m_StoreResult.Value = -1;
                return TaskStatus.Success;
            }

            for (int i = 0; i < listValue.Count; ++i) {
                if (Equals(listValue[i], elementValue)) {
                    m_StoreResult.Value = i;
                    return TaskStatus.Success;
                }
            }

            m_StoreResult.Value = -1;
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
            m_StoreResult = null;
        }
    }
}
#endif