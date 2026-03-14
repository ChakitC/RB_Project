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

    [Opsive.Shared.Utility.Description("Set the GameObject value.")]
    [Shared.Utility.Category("Unity")]
    public class SetGameObject : TargetGameObjectAction
    {
        [Tooltip("The variable that should be set.")]
        [RequireShared] [SerializeField] protected SharedVariable<GameObject> m_StoreResult;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            InitializeTarget();

            m_StoreResult.Value = m_ResolvedGameObject;
            return TaskStatus.Success;
        }
    }
}
#endif