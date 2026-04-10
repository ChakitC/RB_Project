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
    [Opsive.Shared.Utility.Description("Gets the count of elements in the list.")]
    public class GetCount : Action
    {
        [Tooltip("The list that should have its count retrieved.")]
        [SerializeField] protected SharedVariable m_List;
        [Tooltip("The count of elements in the list.")]
        [RequireShared] [SerializeField] protected SharedVariable<int> m_StoreResult;

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var listValue = m_List.GetValue() as IList;
            m_StoreResult.Value = listValue == null ? 0 : listValue.Count;
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_List = null;
            m_StoreResult = null;
        }
    }
}
#endif