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
    [Opsive.Shared.Utility.Description("Selects the element from the list.")]
    public class GetElement : Action
    {
        [Tooltip("The list of elements.")]
        [SerializeField] protected SharedVariable m_List;
        [Tooltip("The index of the element that should be selected.")]
        [SerializeField] protected SharedVariable<int> m_ElementIndex;
        [Tooltip("The selected element.")]
        [RequireShared] [SerializeField] protected SharedVariable m_StoreResult;

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var listValue = m_List.GetValue() as IList;
            if (listValue == null || m_ElementIndex.Value < 0 || m_ElementIndex.Value >= listValue.Count) {
                return TaskStatus.Success;
            }

            m_StoreResult.SetValue(listValue[m_ElementIndex.Value]);
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
            m_StoreResult = null;
        }
    }
}
#endif