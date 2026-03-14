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

    [Opsive.Shared.Utility.Description("Removes the GameObject from the array.")]
    [Shared.Utility.Category("Lists")]
    public class RemoveGameObjectFromArray : TargetGameObjectAction
    {
        [Tooltip("The list of possible GameObjects.")]
        [RequireShared] [SerializeField] protected SharedVariable<GameObject[]> m_StoreResult;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            var array = m_StoreResult.Value;
            if (array == null) {
                return TaskStatus.Failure;
            }

            // Find the index of the GameObject to remove.
            var indexToRemove = -1;
            for (int i = 0; i < array.Length; i++) {
                if (array[i] == m_ResolvedGameObject) {
                    indexToRemove = i;
                    break;
                }
            }
            if (indexToRemove == -1) {
                return TaskStatus.Failure;
            }

            // Create a new array with the GameObject removed.
            var newArray = new GameObject[array.Length - 1];
            for (int i = 0, j = 0; i < array.Length; ++i) {
                if (i != indexToRemove) {
                    newArray[j] = array[i];
                    ++j;
                }
            }

            m_StoreResult.Value = newArray;
            return TaskStatus.Success;
        }
    }
}
#endif