#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Variables
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Set the GameObject value.")]
    [Shared.Utility.Category("Variables")]
    public class SetGameObject : TargetGameObjectAction
    {
        [Tooltip("Can an empty value be set?")]
        [SerializeField] protected SharedVariable<bool> m_AllowEmpty;
        [Tooltip("The variable that should be set.")]
        [RequireShared][SerializeField] protected SharedVariable<GameObject> m_StoreResult;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            InitializeTarget();

            m_StoreResult.Value = m_AllowEmpty.Value ? m_TargetGameObject.Value : m_ResolvedGameObject;
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the task values.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_AllowEmpty = false;
        }
    }
}
#endif