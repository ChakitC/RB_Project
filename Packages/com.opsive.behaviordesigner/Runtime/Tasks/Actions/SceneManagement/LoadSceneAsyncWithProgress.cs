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
    [Opsive.Shared.Utility.Description("Loads scene asynchronously with progress tracking and activation control.")]
    public class LoadSceneAsyncWithProgress : Action
    {
        [Tooltip("The scene name or index to load.")]
        [SerializeField] protected SharedVariable<string> m_SceneName;
        [Tooltip("The scene build index. Used if Scene Name is empty.")]
        [SerializeField] protected SharedVariable<int> m_SceneBuildIndex = -1;
        [Tooltip("The load mode (Single or Additive).")]
        [SerializeField] protected LoadSceneMode m_LoadMode = LoadSceneMode.Single;
        [Tooltip("Whether to allow scene activation immediately.")]
        [SerializeField] protected SharedVariable<bool> m_AllowSceneActivation = true;
        [Tooltip("The loading progress (0-1).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_LoadingProgress;
        [Tooltip("Whether the scene is loaded.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_SceneLoaded;

        private AsyncOperation m_LoadOperation;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_LoadingProgress.Value = 0.0f;
            m_SceneLoaded.Value = false;
            m_LoadOperation = null;

            if (!string.IsNullOrEmpty(m_SceneName.Value)) {
                m_LoadOperation = SceneManager.LoadSceneAsync(m_SceneName.Value, m_LoadMode);
            } else if (m_SceneBuildIndex.Value >= 0) {
                m_LoadOperation = SceneManager.LoadSceneAsync(m_SceneBuildIndex.Value, m_LoadMode);
            }

            if (m_LoadOperation != null) {
                m_LoadOperation.allowSceneActivation = m_AllowSceneActivation.Value;
            }
        }

        /// <summary>
        /// Updates the scene loading.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_LoadOperation != null) {
                m_LoadingProgress.Value = m_LoadOperation.progress;
                m_SceneLoaded.Value = m_LoadOperation.isDone;

                if (m_LoadOperation.isDone) {
                    return TaskStatus.Success;
                }
            } else {
                m_LoadingProgress.Value = 0.0f;
                m_SceneLoaded.Value = false;
                return TaskStatus.Failure;
            }

            return TaskStatus.Running;
        }
    }
}
#endif