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
    [Opsive.Shared.Utility.Description("Toggles a GameObject's active state. Can optionally delay the toggle. Returns Finished when toggled.")]
    public class ToggleActive : TargetGameObjectAction
    {
        [Tooltip("The delay before toggling (in seconds).")]
        [SerializeField] protected SharedVariable<float> m_Delay = 0f;

        private float m_ElapsedTime;
        private bool m_HasToggled;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0f;
            m_HasToggled = false;
        }

        /// <summary>
        /// Toggles the GameObject's active state.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_HasToggled) {
                return TaskStatus.Success;
            }

            if (m_ElapsedTime >= m_Delay.Value) {
                gameObject.SetActive(!gameObject.activeSelf);
                m_HasToggled = true;
                return TaskStatus.Success;
            }

            m_ElapsedTime += Time.deltaTime;
            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Delay = 0f;
        }
    }
}
#endif