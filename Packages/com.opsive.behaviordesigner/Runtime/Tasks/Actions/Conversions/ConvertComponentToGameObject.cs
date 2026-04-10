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
    [Opsive.Shared.Utility.Description("Gets the GameObject from a Component.")]
    public class ConvertComponentToGameObject : Action
    {
        [Tooltip("The Component to get the GameObject from.")]
        [SerializeField] protected SharedVariable<Component> m_SourceComponent;
        [Tooltip("The resulting GameObject.")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject> m_ResultGameObject;

        /// <summary>
        /// Gets the GameObject from the Component.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_SourceComponent.Value == null) {
                m_ResultGameObject.Value = null;
                return TaskStatus.Success;
            }

            m_ResultGameObject.Value = m_SourceComponent.Value.gameObject;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_SourceComponent = null;
            m_ResultGameObject = null;
        }
    }
}
#endif