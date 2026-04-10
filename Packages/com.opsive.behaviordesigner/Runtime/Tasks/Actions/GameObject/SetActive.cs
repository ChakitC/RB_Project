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
    [Opsive.Shared.Utility.Description("Sets a GameObject active or inactive after a delay. Returns Finished when the state is changed.")]
    public class SetActive : TargetGameObjectAction
    {
        [Tooltip("The active state to set.")]
        [SerializeField] protected SharedVariable<bool> m_Active = true;
        [Tooltip("The delay before changing the active state (in seconds).")]
        [SerializeField] protected SharedVariable<float> m_Delay = 0f;

        private float m_ElapsedTime;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0f;
        }

        /// <summary>
        /// Sets the GameObject active/inactive after delay.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_ElapsedTime += Time.deltaTime;

            if (m_ElapsedTime >= m_Delay.Value) {
                m_ResolvedGameObject.SetActive(m_Active.Value);
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Active = true;
            m_Delay = 0f;
        }
    }
}
#endif