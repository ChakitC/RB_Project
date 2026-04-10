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
    [Opsive.Shared.Utility.Description("Sets a random element value from the list.")]
    public class RandomElement : Action
    {
        [Tooltip("The list of possible elements.")]
        [SerializeField] protected SharedVariable m_List;
        [Tooltip("The variable that should be set.")]
        [RequireShared] [SerializeField] protected SharedVariable m_StoreResult;
        [Tooltip("The seed of the random number generator. Set to 0 to disable.")]
        [SerializeField] protected int m_Seed;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();
            if (m_Seed != 0) {
                Random.InitState(m_Seed);
            }
        }

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var listValue = m_List.GetValue() as IList;
            if (listValue == null || listValue.Count == 0) {
                return TaskStatus.Success;
            }

            m_StoreResult.SetValue(listValue[Random.Range(0, listValue.Count)]);
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
            m_Seed = 0;
        }
    }
}
#endif