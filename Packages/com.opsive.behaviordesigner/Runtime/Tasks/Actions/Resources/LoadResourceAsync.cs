#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Resources
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Resources")]
    [Opsive.Shared.Utility.Description("Loads resource asynchronously with progress tracking.")]
    public class LoadResourceAsync : Action
    {
        [Tooltip("The resource path to load.")]
        [SerializeField] protected SharedVariable<string> m_ResourcePath;
        [Tooltip("The loading progress (0-1).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_LoadingProgress;
        [Tooltip("The loaded resource (as Object).")]
        [SerializeField] [RequireShared] protected SharedVariable<Object> m_LoadedResource;
        [Tooltip("Whether the load is complete.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_LoadComplete;

        private ResourceRequest m_ResourceRequest;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_LoadingProgress.Value = 0.0f;
            m_LoadComplete.Value = false;
            m_LoadedResource.Value = null;
            m_ResourceRequest = null;

            if (!string.IsNullOrEmpty(m_ResourcePath.Value)) {
                m_ResourceRequest = UnityEngine.Resources.LoadAsync(m_ResourcePath.Value);
            }
        }

        /// <summary>
        /// Updates the async load operation.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResourceRequest != null) {
                m_LoadingProgress.Value = m_ResourceRequest.progress;
                m_LoadComplete.Value = m_ResourceRequest.isDone;

                if (m_ResourceRequest.isDone) {
                    m_LoadedResource.Value = m_ResourceRequest.asset;
                    return TaskStatus.Success;
                }
            } else {
                m_LoadingProgress.Value = 0.0f;
                m_LoadComplete.Value = false;
                return TaskStatus.Failure;
            }

            return TaskStatus.Running;
        }
    }
}
#endif