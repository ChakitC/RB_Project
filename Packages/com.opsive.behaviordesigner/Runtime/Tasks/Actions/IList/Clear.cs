#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.IList
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System;
    using System.Collections;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("IList")]
    [Opsive.Shared.Utility.Description("Clears all elements from the list.")]
    public class Clear : Actions.Action
    {
        [Tooltip("The list that should be cleared.")]
        [RequireShared] [SerializeField] protected SharedVariable m_List;

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var listValue = m_List.GetValue();
            if (listValue == null) {
                return TaskStatus.Success;
            }
            if (listValue is Array listArray) {
                m_List.SetValue(null);
            } else if (listValue is IList iList) {
                iList.Clear();
            }
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_List = null;
        }
    }
}
#endif