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
    [Opsive.Shared.Utility.Description("Finds GameObjects matching both tag and layer mask.")]
    public class FindGameObjectByTagAndLayer : Action
    {
        [Tooltip("The tag to search for.")]
        [SerializeField] protected SharedVariable<string> m_Tag;
        [Tooltip("The layer mask to filter by.")]
        [SerializeField] protected LayerMask m_LayerMask = -1;
        [Tooltip("The found GameObjects.")]
        [SerializeField] [RequireShared] protected SharedVariable<List<GameObject>> m_FoundGameObjects;

        private List<GameObject> m_FoundList;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();
            if (m_FoundGameObjects.Value == null) {
                m_FoundGameObjects.Value = new List<GameObject>();
            }
            m_FoundList = m_FoundGameObjects.Value;
        }

        /// <summary>
        /// Finds GameObjects by tag and layer.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_FoundList.Clear();

            if (!string.IsNullOrEmpty(m_Tag.Value)) {
                var gameObjects = GameObject.FindGameObjectsWithTag(m_Tag.Value);

                for (int i = 0; i < gameObjects.Length; ++i) {
                    if (gameObjects[i] != null && ((1 << gameObjects[i].layer) & m_LayerMask) != 0) {
                        m_FoundList.Add(gameObjects[i]);
                    }
                }
            }

            m_FoundGameObjects.Value = m_FoundList;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Tag = null;
            m_LayerMask = -1;
            m_FoundGameObjects = null;
        }
    }
}
#endif