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
    [Opsive.Shared.Utility.Description("Gets resource path with validation.")]
    public class GetResourcePath : Action
    {
        [Tooltip("The resource to get the path for.")]
        [SerializeField] protected SharedVariable<Object> m_Resource;
        [Tooltip("The resource path (relative to Resources folder).")]
        [SerializeField] [RequireShared] protected SharedVariable<string> m_ResourcePath;
        [Tooltip("Whether a valid path was found.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_PathValid;

        /// <summary>
        /// Gets the resource path.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Resource.Value == null) {
                m_ResourcePath.Value = "";
                m_PathValid.Value = false;
                return TaskStatus.Success;
            }

            var resourcePath = "";
            var pathValid = false;

#if UNITY_EDITOR
            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(m_Resource.Value);
            if (!string.IsNullOrEmpty(assetPath)) {
                var resourcesIndex = assetPath.IndexOf("/Resources/");
                if (resourcesIndex >= 0) {
                    var startIndex = resourcesIndex + "/Resources/".Length;
                    var endIndex = assetPath.LastIndexOf('.');
                    if (endIndex > startIndex) {
                        resourcePath = assetPath.Substring(startIndex, endIndex - startIndex);
                        pathValid = true;
                    }
                }
            }
#else
            var resourceName = m_Resource.Value.name;
            resourcePath = resourceName;
            pathValid = !string.IsNullOrEmpty(resourceName);
#endif

            m_ResourcePath.Value = resourcePath;
            m_PathValid.Value = pathValid;

            return TaskStatus.Success;
        }
    }
}
#endif