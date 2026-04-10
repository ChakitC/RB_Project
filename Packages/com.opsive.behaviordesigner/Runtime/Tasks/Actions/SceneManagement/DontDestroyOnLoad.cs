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
    [Opsive.Shared.Utility.Description("Marks the target GameObject to persist across scenes.")]
    public class DontDestroyOnLoad : TargetGameObjectAction
    {
        [Tooltip("The list of GameObjects to mark as DontDestroyOnLoad.")]
        [SerializeField] protected List<SharedVariable<GameObject>> m_GameObjects = new List<SharedVariable<GameObject>>();

        /// <summary>
        /// Marks the target GameObject as DontDestroyOnLoad.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            Object.DontDestroyOnLoad(gameObject);
            return TaskStatus.Success;
        }
    }
}
#endif