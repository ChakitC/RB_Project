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
    using UnityEngine.UI;

    [Opsive.Shared.Utility.Category("Scene Management")]
    [Opsive.Shared.Utility.Description("Manages scene transition with fade/loading screen and progress.")]
    public class SceneTransition : Action
    {
        [Tooltip("The scene name to load.")]
        [SerializeField] protected SharedVariable<string> m_SceneName;
        [Tooltip("The scene build index. Used if Scene Name is empty.")]
        [SerializeField] protected SharedVariable<int> m_SceneBuildIndex = -1;
        [Tooltip("The loading screen GameObject (optional).")]
        [SerializeField] protected SharedVariable<GameObject> m_LoadingScreen;
        [Tooltip("The loading progress text (optional).")]
        [SerializeField] protected SharedVariable<Text> m_ProgressText;
        [Tooltip("The fade duration before loading.")]
        [SerializeField] protected SharedVariable<float> m_FadeDuration = 0.5f;
        [Tooltip("The loading progress (0-1).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_LoadingProgress;
        [Tooltip("Whether the transition is complete.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_TransitionComplete;

        private AsyncOperation m_LoadOperation;
        private float m_FadeElapsedTime;
        private bool m_HasStartedFade;
        private bool m_IsLoading;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_FadeElapsedTime = 0.0f;
            m_HasStartedFade = false;
            m_IsLoading = false;
            m_LoadingProgress.Value = 0.0f;
            m_TransitionComplete.Value = false;

            if (m_LoadingScreen.Value != null) {
                m_LoadingScreen.Value.SetActive(false);
            }
        }

        /// <summary>
        /// Updates the scene transition.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (!m_HasStartedFade && m_FadeDuration.Value > 0.0f) {
                m_FadeElapsedTime += Time.deltaTime;
                if (m_FadeElapsedTime >= m_FadeDuration.Value) {
                    m_HasStartedFade = true;
                    m_FadeElapsedTime = 0.0f;
                    StartLoading();
                }
            } else if (!m_HasStartedFade) {
                StartLoading();
            }

            if (m_IsLoading && m_LoadOperation != null) {
                m_LoadingProgress.Value = m_LoadOperation.progress;

                if (m_ProgressText.Value != null) {
                    m_ProgressText.Value.text = $"Loading... {(m_LoadOperation.progress * 100):F0}%";
                }

                if (m_LoadOperation.isDone) {
                    m_TransitionComplete.Value = true;
                    if (m_LoadingScreen.Value != null) {
                        m_LoadingScreen.Value.SetActive(false);
                    }
                    return TaskStatus.Success;
                }
            }

            return m_IsLoading || !m_HasStartedFade ? TaskStatus.Running : TaskStatus.Failure;
        }

        /// <summary>
        /// Starts loading the scene.
        /// </summary>
        private void StartLoading()
        {
            if (m_LoadingScreen.Value != null) {
                m_LoadingScreen.Value.SetActive(true);
            }

            if (!string.IsNullOrEmpty(m_SceneName.Value)) {
                m_LoadOperation = SceneManager.LoadSceneAsync(m_SceneName.Value, LoadSceneMode.Single);
            } else if (m_SceneBuildIndex.Value >= 0) {
                m_LoadOperation = SceneManager.LoadSceneAsync(m_SceneBuildIndex.Value, LoadSceneMode.Single);
            }

            m_IsLoading = m_LoadOperation != null;
        }
    }
}
#endif