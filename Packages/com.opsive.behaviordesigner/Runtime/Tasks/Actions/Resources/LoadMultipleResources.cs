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
    public class ResourcePathEntry
    {
        public SharedVariable<string> path;
        public SharedVariable<Object> loadedResource;
    }

    [Opsive.Shared.Utility.Category("Resources")]
    [Opsive.Shared.Utility.Description("Loads multiple resources with progress tracking.")]
    public class LoadMultipleResources : Action
    {
        [Tooltip("The list of resource paths to load.")]
        [SerializeField] protected List<ResourcePathEntry> m_ResourcePaths = new List<ResourcePathEntry>();
        [Tooltip("The loading progress (0-1).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_LoadingProgress;
        [Tooltip("The number of resources successfully loaded.")]
        [SerializeField] [RequireShared] protected SharedVariable<int> m_LoadedCount;

        /// <summary>
        /// Loads all resources.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResourcePaths == null || m_ResourcePaths.Count == 0) {
                m_LoadingProgress.Value = 1.0f;
                m_LoadedCount.Value = 0;
                return TaskStatus.Success;
            }

            var loadedCount = 0;
            var totalCount = m_ResourcePaths.Count;

            for (int i = 0; i < m_ResourcePaths.Count; ++i) {
                var entry = m_ResourcePaths[i];
                if (entry == null || entry.path == null || string.IsNullOrEmpty(entry.path.Value)) {
                    continue;
                }

                var resource = UnityEngine.Resources.Load(entry.path.Value);
                if (entry.loadedResource != null) {
                    entry.loadedResource.Value = resource;
                }

                if (resource != null) {
                    loadedCount++;
                }
            }

            m_LoadedCount.Value = loadedCount;
            m_LoadingProgress.Value = totalCount > 0 ? (float)loadedCount / totalCount : 1.0f;

            return TaskStatus.Success;
        }
    }
}
#endif