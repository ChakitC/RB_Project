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
    [Opsive.Shared.Utility.Description("Loads resource with fallback option and type validation.")]
    public class LoadResourceWithFallback : Action
    {
        [Tooltip("The resource path to load.")]
        [SerializeField] protected SharedVariable<string> m_ResourcePath;
        [Tooltip("The fallback resource path if primary fails.")]
        [SerializeField] protected SharedVariable<string> m_FallbackPath;
        [Tooltip("The loaded resource (as Object).")]
        [SerializeField] [RequireShared] protected SharedVariable<Object> m_LoadedResource;
        [Tooltip("Whether the load was successful.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_LoadSuccessful;

        /// <summary>
        /// Loads the resource with fallback.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (string.IsNullOrEmpty(m_ResourcePath.Value)) {
                m_LoadSuccessful.Value = false;
                m_LoadedResource.Value = null;
                return TaskStatus.Success;
            }

            var resource = UnityEngine.Resources.Load(m_ResourcePath.Value);

            if (resource == null && !string.IsNullOrEmpty(m_FallbackPath.Value)) {
                resource = UnityEngine.Resources.Load(m_FallbackPath.Value);
            }

            m_LoadedResource.Value = resource;
            m_LoadSuccessful.Value = resource != null;

            return TaskStatus.Success;
        }
    }
}
#endif