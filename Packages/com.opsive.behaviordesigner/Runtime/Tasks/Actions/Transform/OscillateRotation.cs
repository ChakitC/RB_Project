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
    [Opsive.Shared.Utility.Description("Oscillates the Transform's rotation between two rotations using sine, cosine, or ping-pong oscillation.")]
    public class OscillateRotation : TargetGameObjectAction
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

        /// <summary>
        /// Specifies which axis to oscillate.
        /// </summary>
        public enum OscillateAxis
        {
            X,      // Oscillate X axis only.
            Y,      // Oscillate Y axis only.
            Z,      // Oscillate Z axis only.
            All     // Oscillate all axes.
        }

        [Tooltip("The start rotation (Euler angles in degrees).")]
        [SerializeField] protected SharedVariable<Vector3> m_StartRotation;
        [Tooltip("The end rotation (Euler angles in degrees).")]
        [SerializeField] protected SharedVariable<Vector3> m_EndRotation;
        [Tooltip("The oscillation speed.")]
        [SerializeField] protected SharedVariable<float> m_OscillationSpeed = 1f;
        [Tooltip("The oscillation type.")]
        [SerializeField] protected OscillationType m_OscillationType = OscillationType.Sine;
        [Tooltip("The axis to oscillate.")]
        [SerializeField] protected OscillateAxis m_OscillateAxis = OscillateAxis.All;
        [Tooltip("Should the oscillation be continuous?")]
        [SerializeField] protected SharedVariable<bool> m_Continuous = true;
        [Tooltip("The initial time offset (0 to 1).")]
        [SerializeField] protected SharedVariable<float> m_InitialOffset = 0f;

        private float m_Time;
        private Vector3 m_InitialRotation;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_Time = m_InitialOffset.Value;
            m_InitialRotation = transform.eulerAngles;
        }

        /// <summary>
        /// Oscillates the rotation.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (!m_Continuous.Value && m_Time >= 1f) {
                transform.rotation = Quaternion.Euler(m_EndRotation.Value);
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

            var currentEuler = transform.eulerAngles;

            Vector3 newEuler;
            switch (m_OscillateAxis) {
                case OscillateAxis.X:
                    newEuler = new Vector3(
                        Mathf.LerpAngle(m_StartRotation.Value.x, m_EndRotation.Value.x, t),
                        currentEuler.y,
                        currentEuler.z
                    );
                    break;
                case OscillateAxis.Y:
                    newEuler = new Vector3(
                        currentEuler.x,
                        Mathf.LerpAngle(m_StartRotation.Value.y, m_EndRotation.Value.y, t),
                        currentEuler.z
                    );
                    break;
                case OscillateAxis.Z:
                    newEuler = new Vector3(
                        currentEuler.x,
                        currentEuler.y,
                        Mathf.LerpAngle(m_StartRotation.Value.z, m_EndRotation.Value.z, t)
                    );
                    break;
                case OscillateAxis.All:
                default:
                    newEuler = new Vector3(
                        Mathf.LerpAngle(m_StartRotation.Value.x, m_EndRotation.Value.x, t),
                        Mathf.LerpAngle(m_StartRotation.Value.y, m_EndRotation.Value.y, t),
                        Mathf.LerpAngle(m_StartRotation.Value.z, m_EndRotation.Value.z, t)
                    );
                    break;
            }

            transform.rotation = Quaternion.Euler(newEuler);

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_StartRotation = null;
            m_EndRotation = null;
            m_OscillationSpeed = 1f;
            m_OscillationType = OscillationType.Sine;
            m_OscillateAxis = OscillateAxis.All;
            m_Continuous = true;
            m_InitialOffset = 0f;
        }
    }
}
#endif