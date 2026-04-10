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
    [Opsive.Shared.Utility.Description("Gets the parent Transform from a GameObject or Transform. Can traverse up multiple levels.")]
    public class GetParentTransform : Action
    {
        [Tooltip("The GameObject to get the parent from. If null, uses Transform.")]
        [SerializeField] protected SharedVariable<GameObject> m_SourceGameObject;
        [Tooltip("The Transform to get the parent from. Only used if GameObject is null.")]
        [SerializeField] protected SharedVariable<Transform> m_SourceTransform;
        [Tooltip("The number of levels up to traverse (0 = direct parent, 1 = grandparent, etc.).")]
        [SerializeField] protected SharedVariable<int> m_LevelsUp = 0;
        [Tooltip("The resulting parent Transform.")]
        [SerializeField] [RequireShared] protected SharedVariable<Transform> m_ParentTransform;

        /// <summary>
        /// Gets the parent Transform.
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
                m_ParentTransform.Value = null;
                return TaskStatus.Success;
            }

            var parent = sourceTransform.parent;

            for (int i = 0; i < m_LevelsUp.Value && parent != null; ++i) {
                parent = parent.parent;
            }

            m_ParentTransform.Value = parent;

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
            m_ParentTransform = null;
        }
    }
}
#endif