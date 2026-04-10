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
    [Opsive.Shared.Utility.Description("Gets a child GameObject by name. Can search recursively and use partial name matching.")]
    public class GetChildByName : Action
    {
        [Tooltip("The parent GameObject.")]
        [SerializeField] protected SharedVariable<GameObject> m_Parent;
        [Tooltip("The child name to search for.")]
        [SerializeField] protected SharedVariable<string> m_ChildName;
        [Tooltip("Should the search be recursive (search children of children)?")]
        [SerializeField] protected SharedVariable<bool> m_Recursive = false;
        [Tooltip("Should partial name matching be used (contains instead of exact match)?")]
        [SerializeField] protected SharedVariable<bool> m_PartialMatch = false;
        [Tooltip("The found child GameObject.")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject> m_Child;

        /// <summary>
        /// Gets the child by name.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Parent.Value == null || string.IsNullOrEmpty(m_ChildName.Value)) {
                m_Child.Value = null;
                return TaskStatus.Success;
            }

            var parent = m_Parent.Value.transform;

            GameObject found = null;

            if (m_Recursive.Value) {
                found = FindChildRecursive(parent, m_ChildName.Value, m_PartialMatch.Value);
            } else {
                found = FindChildDirect(parent, m_ChildName.Value, m_PartialMatch.Value);
            }

            m_Child.Value = found;
            return TaskStatus.Success;
        }

        /// <summary>
        /// Finds a child directly (non-recursive).
        /// </summary>
        private GameObject FindChildDirect(Transform parent, string name, bool partialMatch)
        {
            foreach (Transform child in parent) {
                if (partialMatch ? child.name.Contains(name) : child.name == name) {
                    return child.gameObject;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds a child recursively.
        /// </summary>
        private GameObject FindChildRecursive(Transform parent, string name, bool partialMatch)
        {
            foreach (Transform child in parent) {
                if (partialMatch ? child.name.Contains(name) : child.name == name) {
                    return child.gameObject;
                }
                var found = FindChildRecursive(child, name, partialMatch);
                if (found != null) {
                    return found;
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
            m_Parent = null;
            m_ChildName = null;
            m_Recursive = false;
            m_PartialMatch = false;
            m_Child = null;
        }
    }
}
#endif