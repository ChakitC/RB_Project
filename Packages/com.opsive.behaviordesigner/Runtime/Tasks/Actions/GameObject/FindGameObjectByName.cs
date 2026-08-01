#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("GameObject")]
    [Opsive.Shared.Utility.Description("Finds a GameObject by name with search scope options (children, parent, scene, siblings). Can include inactive GameObjects and search recursively.")]
    public class FindGameObjectByName : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the search scope.
        /// </summary>
        public enum SearchScope
        {
            ChildrenOnly,   // Search only in children.
            ParentOnly,     // Search only in parent hierarchy.
            EntireScene,    // Search entire scene.
            SiblingsOnly    // Search only siblings.
        }

        [Tooltip("The name to search for.")]
        [SerializeField] protected SharedVariable<string> m_Name;
        [Tooltip("The search scope.")]
        [SerializeField] protected SearchScope m_SearchScope = SearchScope.EntireScene;
        [Tooltip("Should inactive GameObjects be included in the search?")]
        [SerializeField] protected SharedVariable<bool> m_IncludeInactive = false;
        [Tooltip("Should the search be recursive (search children of children)?")]
        [SerializeField] protected SharedVariable<bool> m_Recursive = true;
        [Tooltip("The found GameObject.")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject> m_FoundObject;

        /// <summary>
        /// Finds the GameObject by name.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (string.IsNullOrEmpty(m_Name.Value)) {
                m_FoundObject.Value = null;
                return TaskStatus.Success;
            }

            GameObject found = null;
            switch (m_SearchScope) {
                case SearchScope.ChildrenOnly:
                    found = FindInChildren(m_ResolvedGameObject, m_Name.Value, m_Recursive.Value, m_IncludeInactive.Value);
                    break;
                case SearchScope.ParentOnly:
                    found = FindInParent(m_ResolvedGameObject, m_Name.Value);
                    break;
                case SearchScope.EntireScene:
                    found = FindInScene(m_Name.Value, m_IncludeInactive.Value);
                    break;
                case SearchScope.SiblingsOnly:
                    found = FindInSiblings(m_ResolvedGameObject, m_Name.Value, m_IncludeInactive.Value);
                    break;
            }

            m_FoundObject.Value = found;
            return TaskStatus.Success;
        }

        /// <summary>
        /// Finds a GameObject in children.
        /// </summary>
        private GameObject FindInChildren(GameObject root, string name, bool recursive, bool includeInactive)
        {
            foreach (Transform child in root.transform) {
                if ((includeInactive || child.gameObject.activeSelf) && child.name == name) {
                    return child.gameObject;
                }
                if (recursive) {
                    var found = FindInChildren(child.gameObject, name, true, includeInactive);
                    if (found != null) {
                        return found;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds a GameObject in parent hierarchy.
        /// </summary>
        private GameObject FindInParent(GameObject root, string name)
        {
            var parent = root.transform.parent;
            while (parent != null) {
                if (parent.name == name) {
                    return parent.gameObject;
                }
                parent = parent.parent;
            }
            return null;
        }

        /// <summary>
        /// Finds a GameObject in the entire scene.
        /// </summary>
        private GameObject FindInScene(string name, bool includeInactive)
        {
#if UNITY_6000_4_OR_NEWER
            var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#elif UNITY_6000_0_OR_NEWER
            var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(includeInactive);
#endif
            foreach (var obj in allObjects) {
                if (obj.name == name) {
                    return obj;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds a GameObject in siblings.
        /// </summary>
        private GameObject FindInSiblings(GameObject root, string name, bool includeInactive)
        {
            var parent = root.transform.parent;
            if (parent == null) {
                return null;
            }

            foreach (Transform sibling in parent) {
                if (sibling.gameObject != root && (includeInactive || sibling.gameObject.activeSelf) && sibling.name == name) {
                    return sibling.gameObject;
                }
            }
            return null;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Name = null;
            m_SearchScope = SearchScope.EntireScene;
            m_IncludeInactive = false;
            m_Recursive = true;
            m_FoundObject = null;
        }
    }
}
#endif