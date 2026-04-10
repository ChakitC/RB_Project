#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Transform")]
    [Opsive.Shared.Utility.Description("Oscillates the Transform's scale between two values using sine, cosine, or ping-pong oscillation.")]
    public class OscillateScale : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the oscillation type.
        /// </summary>
        public enum OscillationType
        {
            Sine,       // Sine wave oscillation.
            Cosine,     // Cosine wave oscillation.
            PingPong    // Ping-pong between start and end.
        }

        [Tooltip("The minimum scale.")]
        [SerializeField] protected SharedVariable<Vector3> m_MinScale = Vector3.one * 0.5f;
        [Tooltip("The maximum scale.")]
        [SerializeField] protected SharedVariable<Vector3> m_MaxScale = Vector3.one;
        [Tooltip("The oscillation speed.")]
        [SerializeField] protected SharedVariable<float> m_OscillationSpeed = 1f;
        [Tooltip("The oscillation type.")]
        [SerializeField] protected OscillationType m_OscillationType = OscillationType.Sine;
        [Tooltip("Should the oscillation be continuous?")]
        [SerializeField] protected SharedVariable<bool> m_Continuous = true;
        [Tooltip("The initial time offset (0 to 1).")]
        [SerializeField] protected SharedVariable<float> m_InitialOffset = 0f;

        private float m_Time;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_Time = m_InitialOffset.Value;
        }

        /// <summary>
        /// Oscillates the scale.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (!m_Continuous.Value && m_Time >= 1f) {
                transform.localScale = m_MaxScale.Value;
                return TaskStatus.Success;
            }

            m_Time += m_OscillationSpeed.Value * Time.deltaTime;

            float t;
            switch (m_OscillationType) {
                case OscillationType.Sine:
                    t = (Mathf.Sin(m_Time * Mathf.PI * 2f) + 1f) / 2f;
                    break;
                case OscillationType.Cosine:
                    t = (Mathf.Cos(m_Time * Mathf.PI * 2f) + 1f) / 2f;
                    break;
                case OscillationType.PingPong:
                    t = Mathf.PingPong(m_Time, 1f);
                    break;
                default:
                    t = 0f;
                    break;
            }

            transform.localScale = Vector3.Lerp(m_MinScale.Value, m_MaxScale.Value, t);

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_MinScale = Vector3.one * 0.5f;
            m_MaxScale = Vector3.one;
            m_OscillationSpeed = 1f;
            m_OscillationType = OscillationType.Sine;
            m_Continuous = true;
            m_InitialOffset = 0f;
        }
    }
}
#endif