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
    [Opsive.Shared.Utility.Description("Gets the root Transform (topmost parent) from a GameObject or Transform.")]
    public class GetRootTransform : Action
    {
        [Tooltip("The GameObject to get the root from. If null, uses Transform.")]
        [SerializeField] protected SharedVariable<GameObject> m_SourceGameObject;
        [Tooltip("The Transform to get the root from. Only used if GameObject is null.")]
        [SerializeField] protected SharedVariable<Transform> m_SourceTransform;
        [Tooltip("The resulting root Transform.")]
        [SerializeField] [RequireShared] protected SharedVariable<Transform> m_RootTransform;

        /// <summary>
        /// Gets the root Transform.
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
                m_RootTransform.Value = null;
                return TaskStatus.Success;
            }

            m_RootTransform.Value = sourceTransform.root;

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
            m_RootTransform = null;
        }
    }
}
#endif