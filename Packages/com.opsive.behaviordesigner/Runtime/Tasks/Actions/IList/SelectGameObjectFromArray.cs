#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.UnityObjects
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Selects the GameObject from the array.")]
    [Shared.Utility.Category("Lists")]
    public class SelectGameObjectFromArray : Action
    {
        [Tooltip("The list of possible GameObjects.")]
        [SerializeField] protected SharedVariable<GameObject[]> m_GameObjects;
        [Tooltip("The index of the GameObject that should be selected.")]
        [SerializeField] protected SharedVariable<int> m_ElementIndex;
        [Tooltip("The selected GameObject.")]
        [RequireShared] [SerializeField] protected SharedVariable<GameObject> m_StoreResult;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_GameObjects.Value == null || m_ElementIndex.Value < 0 || m_ElementIndex.Value > m_GameObjects.Value.Length) {
                return TaskStatus.Failure;
            }

            m_StoreResult.Value = m_GameObjects.Value[m_ElementIndex.Value];
            return TaskStatus.Success;
        }
    }
}
#endif