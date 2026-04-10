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
    [Opsive.Shared.Utility.Description("Enables or disables the specified component.")]
    public class SetEnabled : Action
    {
        [Tooltip("The component reference that should be enabled or disabled.")]
        [SerializeField] protected SharedVariable m_Component;
        [Tooltip("The behavior that should be enabled or disabled.")]
        [SerializeField] protected Behaviour m_Behavior;
        [Tooltip("Should the component be enabled?")]
        [SerializeField] protected SharedVariable<bool> m_Enable = true;

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var behaviour = m_Component?.GetValue() as Behaviour;
            if (behaviour != null) {
                behaviour.enabled = m_Enable.Value;
            }
            if (m_Behavior != null) {
                m_Behavior.enabled = m_Enable.Value;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Component = null;
            m_Behavior = null;
            m_Enable = true;
        }
    }
}
#endif