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
    using UnityEngine.SceneManagement;

    [System.Serializable]
    public class SceneLoadEntry
    {
        public SharedVariable<string> sceneName;
        public SharedVariable<int> sceneBuildIndex = -1;
        public SharedVariable<bool> loadComplete;
    }

    [Opsive.Shared.Utility.Category("Scene Management")]
    [Opsive.Shared.Utility.Description("Loads multiple scenes additively with progress tracking.")]
    public class LoadAdditiveScenes : Action
    {
        [Tooltip("The list of scenes to load additively.")]
        [SerializeField] protected List<SceneLoadEntry> m_Scenes = new List<SceneLoadEntry>();
        [Tooltip("The overall loading progress (0-1).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_LoadingProgress;
        [Tooltip("The number of scenes successfully loaded.")]
        [SerializeField] [RequireShared] protected SharedVariable<int> m_LoadedCount;

        private List<AsyncOperation> m_LoadOperations = new List<AsyncOperation>();

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_LoadOperations.Clear();
            m_LoadingProgress.Value = 0.0f;
            m_LoadedCount.Value = 0;

            if (m_Scenes != null) {
                for (int i = 0; i < m_Scenes.Count; ++i) {
                    var entry = m_Scenes[i];
                    if (entry == null) {
                        continue;
                    }

                    AsyncOperation loadOp = null;
                    if (entry.sceneName != null && !string.IsNullOrEmpty(entry.sceneName.Value)) {
                        loadOp = SceneManager.LoadSceneAsync(entry.sceneName.Value, LoadSceneMode.Additive);
                    } else if (entry.sceneBuildIndex != null && entry.sceneBuildIndex.Value >= 0) {
                        loadOp = SceneManager.LoadSceneAsync(entry.sceneBuildIndex.Value, LoadSceneMode.Additive);
                    }

                    if (loadOp != null) {
                        m_LoadOperations.Add(loadOp);
                        if (entry.loadComplete != null) {
                            entry.loadComplete.Value = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Updates the scene loading.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_LoadOperations.Count == 0) {
                m_LoadingProgress.Value = 0.0f;
                return TaskStatus.Failure;
            }

            var loadedCount = 0;
            var totalProgress = 0.0f;

            for (int i = 0; i < m_LoadOperations.Count && i < m_Scenes.Count; ++i) {
                var loadOp = m_LoadOperations[i];
                var entry = m_Scenes[i];

                if (loadOp != null) {
                    totalProgress += loadOp.progress;

                    if (loadOp.isDone) {
                        loadedCount++;
                        if (entry != null && entry.loadComplete != null) {
                            entry.loadComplete.Value = true;
                        }
                    }
                }
            }

            m_LoadedCount.Value = loadedCount;
            m_LoadingProgress.Value = m_LoadOperations.Count > 0 ? totalProgress / m_LoadOperations.Count : 1.0f;

            if (loadedCount >= m_LoadOperations.Count) {
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }
    }
}
#endif