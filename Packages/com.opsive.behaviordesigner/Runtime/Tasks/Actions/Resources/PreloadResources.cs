#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Resources
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections.Generic;
    using UnityEngine;

    [System.Serializable]
    public class PreloadResourceEntry
    {
        public SharedVariable<string> path;
        public SharedVariable<bool> preloadComplete;
    }

    [Opsive.Shared.Utility.Category("Resources")]
    [Opsive.Shared.Utility.Description("Preloads multiple resources with progress tracking.")]
    public class PreloadResources : Action
    {
        [Tooltip("The list of resource paths to preload.")]
        [SerializeField] protected List<PreloadResourceEntry> m_ResourcePaths = new List<PreloadResourceEntry>();
        [Tooltip("The preloading progress (0-1).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_PreloadProgress;
        [Tooltip("The number of resources successfully preloaded.")]
        [SerializeField] [RequireShared] protected SharedVariable<int> m_PreloadedCount;

        private List<ResourceRequest> m_ResourceRequests = new List<ResourceRequest>();

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ResourceRequests.Clear();
            m_PreloadProgress.Value = 0.0f;
            m_PreloadedCount.Value = 0;

            if (m_ResourcePaths != null) {
                for (int i = 0; i < m_ResourcePaths.Count; ++i) {
                    var entry = m_ResourcePaths[i];
                    if (entry != null && entry.path != null && !string.IsNullOrEmpty(entry.path.Value)) {
                        if (entry.preloadComplete != null) {
                            entry.preloadComplete.Value = false;
                        }
                        m_ResourceRequests.Add(UnityEngine.Resources.LoadAsync(entry.path.Value));
                    }
                }
            }
        }

        /// <summary>
        /// Updates the preload operation.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResourceRequests.Count == 0) {
                m_PreloadProgress.Value = 0.0f;
                return TaskStatus.Failure;
            }

            var completedCount = 0;
            var totalProgress = 0.0f;

            for (int i = 0; i < m_ResourceRequests.Count && i < m_ResourcePaths.Count; ++i) {
                var request = m_ResourceRequests[i];
                var entry = m_ResourcePaths[i];

                if (request != null) {
                    totalProgress += request.progress;

                    if (request.isDone) {
                        completedCount++;
                        if (entry != null && entry.preloadComplete != null) {
                            entry.preloadComplete.Value = true;
                        }
                    }
                }
            }

            m_PreloadedCount.Value = completedCount;
            m_PreloadProgress.Value = m_ResourceRequests.Count > 0 ? totalProgress / m_ResourceRequests.Count : 1.0f;

            if (completedCount >= m_ResourceRequests.Count) {
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }
    }
}
#endif