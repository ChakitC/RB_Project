#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.SceneManagementTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections.Generic;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Scene Management")]
    [Opsive.Shared.Utility.Description("Marks multiple GameObjects to persist across scenes.")]
    public class DontDestroyOnLoadObjects : Action
    {
        [Tooltip("The list of GameObjects to mark as DontDestroyOnLoad.")]
        [SerializeField] protected List<SharedVariable<GameObject>> m_GameObjects = new List<SharedVariable<GameObject>>();

        /// <summary>
        /// Marks all GameObjects as DontDestroyOnLoad.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            for (int i = 0; i < m_GameObjects.Count; ++i) {
                if (m_GameObjects[i] != null && m_GameObjects[i].Value != null) {
                    Object.DontDestroyOnLoad(m_GameObjects[i].Value);
                }
            }

            return TaskStatus.Success;
        }
    }
}
#endif