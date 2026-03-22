#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Conversions
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Converts a GameObject value to a Transform value.")]
    [Shared.Utility.Category("Conversions")]
    public class ConvertGameObjectToTransform : Action
    {
        [Tooltip("The GameObject value to convert.")]
        [SerializeField] protected SharedVariable<GameObject> m_Value;
        [Tooltip("The variable that should be set to the converted Transform value.")]
        [RequireShared] [SerializeField] protected SharedVariable<Transform> m_StoreResult;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Value.Value != null) {
                m_StoreResult.Value = m_Value.Value.transform;
                return TaskStatus.Success;
            }
            m_StoreResult.Value = null;
            return TaskStatus.Failure;
        }
    }
}
#endif

