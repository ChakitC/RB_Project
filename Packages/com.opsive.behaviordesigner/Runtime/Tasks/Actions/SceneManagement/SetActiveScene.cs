#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.SceneManagementTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [Opsive.Shared.Utility.Category("Scene Management")]
    [Opsive.Shared.Utility.Description("Sets active scene with validation and optional callback.")]
    public class SetActiveScene : Action
    {
        [Tooltip("The scene name to set as active.")]
        [SerializeField] protected SharedVariable<string> m_SceneName;
        [Tooltip("The scene build index. Used if Scene Name is empty.")]
        [SerializeField] protected SharedVariable<int> m_SceneBuildIndex = -1;
        [Tooltip("Whether the scene was successfully set as active.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_SceneSetActive;

        /// <summary>
        /// Sets the active scene.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            Scene sceneToSet = default(Scene);

            if (!string.IsNullOrEmpty(m_SceneName.Value)) {
                sceneToSet = SceneManager.GetSceneByName(m_SceneName.Value);
            } else if (m_SceneBuildIndex.Value >= 0) {
                sceneToSet = SceneManager.GetSceneByBuildIndex(m_SceneBuildIndex.Value);
            }

            if (sceneToSet.IsValid() && sceneToSet.isLoaded) {
                SceneManager.SetActiveScene(sceneToSet);
                m_SceneSetActive.Value = true;
            } else {
                m_SceneSetActive.Value = false;
            }

            return TaskStatus.Success;
        }
    }
}
#endif