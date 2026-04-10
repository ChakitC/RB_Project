#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections.Generic;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("GameObject")]
    [Opsive.Shared.Utility.Description("Gets all children of a GameObject and stores them in a list.")]
    public class GetChildren : TargetGameObjectAction
    {
        [Tooltip("The list of children GameObjects.")]
        [RequireShared] [SerializeField] protected SharedVariable<List<GameObject>> m_StoreResult;
        [Tooltip("Should inactive children be included?")]
        [SerializeField] protected SharedVariable<bool> m_IncludeInactive = false;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();
            if (m_StoreResult.Value == null) {
                m_StoreResult.Value = new List<GameObject>();
            }
        }

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedGameObject == null) {
                return TaskStatus.Success;
            }

            m_StoreResult.Value.Clear();
            var transform = m_ResolvedGameObject.transform;
            for (int i = 0; i < transform.childCount; ++i) {
                var child = transform.GetChild(i);
                if (m_IncludeInactive.Value || child.gameObject.activeSelf) {
                    m_StoreResult.Value.Add(child.gameObject);
                }
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_StoreResult = null;
            m_IncludeInactive = false;
        }
    }
}
#endif