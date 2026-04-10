#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Conversions
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Conversions")]
    [Opsive.Shared.Utility.Description("Gets the parent GameObject from a GameObject or Transform. Can traverse up multiple levels.")]
    public class GetParentGameObject : Action
    {
        [Tooltip("The GameObject to get the parent from. If null, uses Transform.")]
        [SerializeField] protected SharedVariable<GameObject> m_SourceGameObject;
        [Tooltip("The Transform to get the parent from. Only used if GameObject is null.")]
        [SerializeField] protected SharedVariable<Transform> m_SourceTransform;
        [Tooltip("The number of levels up to traverse (0 = direct parent, 1 = grandparent, etc.).")]
        [SerializeField] protected SharedVariable<int> m_LevelsUp = 0;
        [Tooltip("The resulting parent GameObject.")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject> m_ParentGameObject;

        /// <summary>
        /// Gets the parent GameObject.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            Transform sourceTransform = null;

            if (m_SourceGameObject.Value != null) {
                sourceTransform = m_SourceGameObject.Value.transform;
            } else if (m_SourceTransform.Value != null) {
                sourceTransform = m_SourceTransform.Value;
            }

            if (sourceTransform == null) {
                m_ParentGameObject.Value = null;
                return TaskStatus.Success;
            }

            var parent = sourceTransform.parent;

            for (int i = 0; i < m_LevelsUp.Value && parent != null; ++i) {
                parent = parent.parent;
            }

            m_ParentGameObject.Value = parent != null ? parent.gameObject : null;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_SourceGameObject = null;
            m_SourceTransform = null;
            m_LevelsUp = 0;
            m_ParentGameObject = null;
        }
    }
}
#endif