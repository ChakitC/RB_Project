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
        private const int c_DataVersion = 2;

        /// <summary>
        /// Specifies the legacy transition work that should be performed.
        /// </summary>
        private enum LegacyTransitionMode
        {
            LoadScene,
            Fade,
            FadeThenLoadScene
        }

        /// <summary>
        /// Specifies how the scene should be selected.
        /// </summary>
        public enum SceneSource
        {
            SceneName,
            BuildIndex
        }

        [System.Obsolete("m_TransitionMode is obsolete in version 3.1.0. Use m_UseFade instead.")]
        [HideInInspector] [Tooltip("Obsolete in version 3.1.0. Use Use Fade instead.")]
        [SerializeField] private LegacyTransitionMode m_TransitionMode = LegacyTransitionMode.FadeThenLoadScene;
        [Tooltip("How the scene should be selected.")]
        [SerializeField] protected SceneSource m_SceneSource = SceneSource.SceneName;
        [Tooltip("The scene name to load.")]
        [SerializeField] protected SharedVariable<string> m_SceneName;
        [Tooltip("The scene build index.")]
        [SerializeField] protected SharedVariable<int> m_SceneBuildIndex = -1;
        [System.Obsolete("m_LoadingScreen is obsolete in version 3.1.0. Use Fade Progress and Loading Progress outputs to drive UI instead.")]
        [HideInInspector] [Tooltip("Obsolete in version 3.1.0. Use Fade Progress and Loading Progress outputs to drive UI instead.")]
        [SerializeField] protected SharedVariable<GameObject> m_LoadingScreen;
        [System.Obsolete("m_ProgressText is obsolete in version 3.1.0. Use Loading Progress output to drive text instead.")]
        [HideInInspector] [Tooltip("Obsolete in version 3.1.0. Use Loading Progress output to drive text instead.")]
        [SerializeField] protected SharedVariable<Text> m_ProgressText;
        [Tooltip("The duration of the fade output.")]
        [SerializeField] protected SharedVariable<float> m_FadeDuration = 0.5f;
        [Tooltip("Should fade progress be output before loading the scene?")]
        [SerializeField] protected SharedVariable<bool> m_UseFade = true;
        [Tooltip("The current fade value (0-1) when Use Fade is enabled.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_FadeProgress;
        [Tooltip("The loading progress (0-1).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_LoadingProgress;
        [Tooltip("Whether the transition is complete.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_TransitionComplete;
        [HideInInspector] [Tooltip("The serialized data version.")]
        [SerializeField] private int m_DataVersion;

        private AsyncOperation m_LoadOperation;
        private float m_FadeElapsedTime;
        private bool m_FadeComplete;
        private bool m_IsLoading;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            MigrateLegacyFields();
            m_FadeElapsedTime = 0.0f;
            m_FadeComplete = !UsesFade();
            m_IsLoading = false;
            m_LoadOperation = null;
            SetSharedValue(m_FadeProgress, 0.0f);
            SetSharedValue(m_LoadingProgress, 0.0f);
            SetSharedValue(m_TransitionComplete, false);
            SetLegacyLoadingScreenActive(false);
        }

        /// <summary>
        /// Updates the scene transition.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            MigrateLegacyFields();
            if (!m_FadeComplete) {
                m_FadeComplete = UpdateFade();
                if (!m_FadeComplete) {
                    return TaskStatus.Running;
                }
            }

            return UpdateLoadingStatus();
        }

        /// <summary>
        /// Migrates the legacy serialized fields to explicit transition fields.
        /// </summary>
        public void MigrateLegacyFields()
        {
            if (m_DataVersion >= c_DataVersion) {
                return;
            }

            m_SceneSource = string.IsNullOrEmpty(GetSceneName()) && GetSceneBuildIndex() >= 0 ? SceneSource.BuildIndex : SceneSource.SceneName;
            if (m_DataVersion == 0) {
                m_UseFade = GetFadeDuration() > 0.0f;
            } else {
#pragma warning disable CS0618
                m_UseFade = m_TransitionMode == LegacyTransitionMode.Fade || m_TransitionMode == LegacyTransitionMode.FadeThenLoadScene;
#pragma warning restore CS0618
            }
            m_DataVersion = c_DataVersion;
        }

        /// <summary>
        /// Updates the fade progress.
        /// </summary>
        /// <returns>True when the fade has completed.</returns>
        private bool UpdateFade()
        {
            var fadeDuration = GetFadeDuration();
            if (fadeDuration <= 0.0f) {
                SetSharedValue(m_FadeProgress, 1.0f);
                return true;
            }

            m_FadeElapsedTime += Time.deltaTime;
            SetSharedValue(m_FadeProgress, Mathf.Clamp01(m_FadeElapsedTime / fadeDuration));
            return m_FadeElapsedTime >= fadeDuration;
        }

        /// <summary>
        /// Updates scene loading progress.
        /// </summary>
        /// <returns>True when loading has completed.</returns>
        private bool UpdateLoading()
        {
            if (m_LoadOperation == null) {
                return false;
            }

            var progress = m_LoadOperation.isDone ? 1.0f : Mathf.Clamp01(m_LoadOperation.progress / 0.9f);
            SetSharedValue(m_LoadingProgress, progress);
            UpdateLegacyProgressText(progress);

            if (!m_LoadOperation.isDone) {
                return false;
            }

            SetSharedValue(m_LoadingProgress, 1.0f);
            SetLegacyLoadingScreenActive(false);
            return true;
        }

        /// <summary>
        /// Updates scene loading and returns the task status.
        /// </summary>
        /// <returns>The loading task status.</returns>
        private TaskStatus UpdateLoadingStatus()
        {
            if (!m_IsLoading && !StartLoading()) {
                return TaskStatus.Failure;
            }

            return UpdateLoading() ? CompleteTransition() : TaskStatus.Running;
        }

        /// <summary>
        /// Starts loading the scene.
        /// </summary>
        /// <returns>True if loading started.</returns>
        private bool StartLoading()
        {
            SetLegacyLoadingScreenActive(true);

            var sceneName = GetSceneName();
            var sceneBuildIndex = GetSceneBuildIndex();
            if (m_SceneSource == SceneSource.SceneName && !string.IsNullOrEmpty(sceneName)) {
                m_LoadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            } else if (m_SceneSource == SceneSource.BuildIndex && sceneBuildIndex >= 0) {
                m_LoadOperation = SceneManager.LoadSceneAsync(sceneBuildIndex, LoadSceneMode.Single);
            }

            m_IsLoading = m_LoadOperation != null;
            if (!m_IsLoading) {
                SetLegacyLoadingScreenActive(false);
            }
            return m_IsLoading;
        }

        /// <summary>
        /// Completes the transition.
        /// </summary>
        /// <returns>The success task status.</returns>
        private TaskStatus CompleteTransition()
        {
            SetSharedValue(m_TransitionComplete, true);
            return TaskStatus.Success;
        }

        /// <summary>
        /// Returns true if the transition includes fade progress.
        /// </summary>
        /// <returns>True if the transition includes fade progress.</returns>
        private bool UsesFade()
        {
            return m_UseFade != null && m_UseFade.Value;
        }

        /// <summary>
        /// Returns the configured scene name.
        /// </summary>
        /// <returns>The configured scene name.</returns>
        private string GetSceneName()
        {
            return m_SceneName != null ? m_SceneName.Value : null;
        }

        /// <summary>
        /// Returns the configured scene build index.
        /// </summary>
        /// <returns>The configured scene build index.</returns>
        private int GetSceneBuildIndex()
        {
            return m_SceneBuildIndex != null ? m_SceneBuildIndex.Value : -1;
        }

        /// <summary>
        /// Returns the configured fade duration.
        /// </summary>
        /// <returns>The configured fade duration.</returns>
        private float GetFadeDuration()
        {
            return m_FadeDuration != null ? m_FadeDuration.Value : 0.0f;
        }

        /// <summary>
        /// Sets the value on the specified SharedVariable.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sharedVariable">The SharedVariable to set.</param>
        /// <param name="value">The value to assign.</param>
        private static void SetSharedValue<T>(SharedVariable<T> sharedVariable, T value)
        {
            if (sharedVariable != null) {
                sharedVariable.Value = value;
            }
        }

        /// <summary>
        /// Sets the legacy loading screen active state.
        /// </summary>
        /// <param name="active">Should the loading screen be active?</param>
        private void SetLegacyLoadingScreenActive(bool active)
        {
#pragma warning disable CS0618
            if (m_LoadingScreen != null && m_LoadingScreen.Value != null) {
                m_LoadingScreen.Value.SetActive(active);
            }
#pragma warning restore CS0618
        }

        /// <summary>
        /// Updates the legacy loading progress text.
        /// </summary>
        /// <param name="progress">The loading progress.</param>
        private void UpdateLegacyProgressText(float progress)
        {
#pragma warning disable CS0618
            if (m_ProgressText != null && m_ProgressText.Value != null) {
                m_ProgressText.Value.text = $"Loading... {(progress * 100):F0}%";
            }
#pragma warning restore CS0618
        }
    }
}
#endif