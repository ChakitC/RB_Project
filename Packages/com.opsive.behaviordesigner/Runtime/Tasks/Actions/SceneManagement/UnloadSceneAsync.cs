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
    [Opsive.Shared.Utility.Description("Unloads scene asynchronously with progress tracking.")]
    public class UnloadSceneAsync : Action
    {
        [Tooltip("The scene name to unload.")]
        [SerializeField] protected SharedVariable<string> m_SceneName;
        [Tooltip("The scene build index. Used if Scene Name is empty.")]
        [SerializeField] protected SharedVariable<int> m_SceneBuildIndex = -1;
        [Tooltip("The unloading progress (0-1).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_UnloadingProgress;
        [Tooltip("Whether the scene is unloaded.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_SceneUnloaded;

        private AsyncOperation m_UnloadOperation;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_UnloadingProgress.Value = 0.0f;
            m_SceneUnloaded.Value = false;
            m_UnloadOperation = null;

            Scene sceneToUnload = default(Scene);
            if (!string.IsNullOrEmpty(m_SceneName.Value)) {
                sceneToUnload = SceneManager.GetSceneByName(m_SceneName.Value);
            } else if (m_SceneBuildIndex.Value >= 0) {
                sceneToUnload = SceneManager.GetSceneByBuildIndex(m_SceneBuildIndex.Value);
            }

            if (sceneToUnload.IsValid() && sceneToUnload.isLoaded) {
                m_UnloadOperation = SceneManager.UnloadSceneAsync(sceneToUnload);
            }
        }

        /// <summary>
        /// Updates the scene unloading.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_UnloadOperation != null) {
                m_UnloadingProgress.Value = m_UnloadOperation.progress;
                m_SceneUnloaded.Value = m_UnloadOperation.isDone;

                if (m_UnloadOperation.isDone) {
                    return TaskStatus.Success;
                }
            } else {
                m_UnloadingProgress.Value = 0.0f;
                m_SceneUnloaded.Value = false;
                return TaskStatus.Failure;
            }

            return TaskStatus.Running;
        }
    }
}
#endif