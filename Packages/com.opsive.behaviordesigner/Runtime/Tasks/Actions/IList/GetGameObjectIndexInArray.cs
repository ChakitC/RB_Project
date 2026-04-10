#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.IList
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("IList")]
    [Opsive.Shared.Utility.Description("Gets the index of the specified GameObject within the array. Returns -1 if not found.")]
    public class GetGameObjectIndexInArray : Action
    {
        [Tooltip("The GameObject to find the index of.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The array to search in.")]
        [SerializeField] protected SharedVariable<GameObject[]> m_Array;
        [Tooltip("The index of the GameObject in the array (-1 if not found).")]
        [RequireShared] [SerializeField] protected SharedVariable<int> m_StoreResult;

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Array.Value == null || m_Target.Value == null) {
                m_StoreResult.Value = -1;
                return TaskStatus.Success;
            }

            for (int i = 0; i < m_Array.Value.Length; ++i) {
                if (m_Array.Value[i] == m_Target.Value) {
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
            m_Target = null;
            m_Array = null;
            m_StoreResult = null;
        }
    }
}
#endif