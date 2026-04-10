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
    [Opsive.Shared.Utility.Description("Gets a child GameObject by index with bounds checking. Supports negative indices (e.g., -1 for last child).")]
    public class GetChildByIndex : Action
    {
        [Tooltip("The parent GameObject.")]
        [SerializeField] protected SharedVariable<GameObject> m_Parent;
        [Tooltip("The child index. Use -1 for last child, -2 for second to last, etc.")]
        [SerializeField] protected SharedVariable<int> m_ChildIndex = 0;
        [Tooltip("The found child GameObject.")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject> m_Child;

        /// <summary>
        /// Gets the child by index.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Parent.Value == null) {
                m_Child.Value = null;
                return TaskStatus.Success;
            }

            var parent = m_Parent.Value.transform;
            var childCount = parent.childCount;

            if (childCount == 0) {
                m_Child.Value = null;
                return TaskStatus.Success;
            }

            var index = m_ChildIndex.Value < 0 ? childCount + m_ChildIndex.Value : m_ChildIndex.Value;

            // Bounds check.
            if (index < 0 || index >= childCount) {
                m_Child.Value = null;
                return TaskStatus.Success;
            }

            m_Child.Value = parent.GetChild(index).gameObject;
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Parent = null;
            m_ChildIndex = 0;
            m_Child = null;
        }
    }
}
#endif