#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections.Generic;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("GameObject")]
    [Opsive.Shared.Utility.Description("Reparents all children from a source GameObject to a new parent. Can filter by name pattern or tag, and maintain world positions.")]
    public class ReparentChildren : Action
    {
        [Tooltip("The source GameObject whose children should be reparented.")]
        [SerializeField] protected SharedVariable<GameObject> m_SourceGameObject;
        [Tooltip("The new parent GameObject. If null, children are unparented.")]
        [SerializeField] protected SharedVariable<GameObject> m_NewParent;
        [Tooltip("Should world positions be maintained?")]
        [SerializeField] protected SharedVariable<bool> m_WorldPositionStays = true;
        [Tooltip("Should children be filtered by name pattern?")]
        [SerializeField] protected SharedVariable<bool> m_FilterByName = false;
        [Tooltip("The name pattern to filter by. Only used if Filter By Name is enabled.")]
        [SerializeField] protected SharedVariable<string> m_NamePattern;
        [Tooltip("Should children be filtered by tag?")]
        [SerializeField] protected SharedVariable<bool> m_FilterByTag = false;
        [Tooltip("The tag to filter by. Only used if Filter By Tag is enabled.")]
        [SerializeField] protected SharedVariable<string> m_Tag;
        [Tooltip("The number of children reparented.")]
        [SerializeField] [RequireShared] protected SharedVariable<int> m_ReparentedCount;

        /// <summary>
        /// Reparents the children.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_SourceGameObject.Value == null) {
                m_ReparentedCount.Value = 0;
                return TaskStatus.Success;
            }

            var source = m_SourceGameObject.Value.transform;
            var newParent = m_NewParent.Value?.transform;
            var childrenToReparent = new List<Transform>();

            // Collect children to reparent.
            foreach (Transform child in source) {
                var shouldReparent = true;
                if (m_FilterByName.Value && !string.IsNullOrEmpty(m_NamePattern.Value)) {
                    shouldReparent = child.name.Contains(m_NamePattern.Value);
                }
                if (shouldReparent && m_FilterByTag.Value && !string.IsNullOrEmpty(m_Tag.Value)) {
                    shouldReparent = child.CompareTag(m_Tag.Value);
                }
                if (shouldReparent) {
                    childrenToReparent.Add(child);
                }
            }

            // Reparent children.
            foreach (var child in childrenToReparent) {
                child.SetParent(newParent, m_WorldPositionStays.Value);
            }

            m_ReparentedCount.Value = childrenToReparent.Count;
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_SourceGameObject = null;
            m_NewParent = null;
            m_WorldPositionStays = true;
            m_FilterByName = false;
            m_NamePattern = null;
            m_FilterByTag = false;
            m_Tag = null;
            m_ReparentedCount = null;
        }
    }
}
#endif