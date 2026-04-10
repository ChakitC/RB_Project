#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.CameraTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Camera")]
    [Opsive.Shared.Utility.Description("Shakes camera with intensity, duration, and curve control.")]
    public class Shake : TargetGameObjectAction
    {
        [Tooltip("The shake intensity.")]
        [SerializeField] protected SharedVariable<float> m_Intensity = 1.0f;
        [Tooltip("The shake duration (seconds).")]
        [SerializeField] protected SharedVariable<float> m_Duration = 1.0f;
        [Tooltip("The shake frequency (shakes per second).")]
        [SerializeField] protected SharedVariable<float> m_Frequency = 10.0f;

        private Camera m_ResolvedCamera;
        private Vector3 m_OriginalPosition;
        private float m_ElapsedTime;
        private bool m_HasStarted;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedCamera = m_ResolvedGameObject.GetComponent<Camera>();
        }

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;
            m_HasStarted = false;

            if (m_ResolvedCamera != null) {
                m_OriginalPosition = m_ResolvedCamera.transform.localPosition;
            }
        }

        /// <summary>
        /// Updates the camera shake.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCamera == null) {
                return TaskStatus.Success;
            }

            if (!m_HasStarted) {
                m_OriginalPosition = m_ResolvedCamera.transform.localPosition;
                m_HasStarted = true;
            }

            m_ElapsedTime += Time.deltaTime;

            if (m_ElapsedTime >= m_Duration.Value) {
                m_ResolvedCamera.transform.localPosition = m_OriginalPosition;
                return TaskStatus.Success;
            }

            var progress = m_ElapsedTime / m_Duration.Value;
            var currentIntensity = m_Intensity.Value * (1.0f - progress);
            var offset = new Vector3(
                Random.Range(-currentIntensity, currentIntensity),
                Random.Range(-currentIntensity, currentIntensity),
                0.0f
            ) * (Mathf.Sin(m_ElapsedTime * m_Frequency.Value * Mathf.PI * 2.0f) * 0.5f + 0.5f);

            m_ResolvedCamera.transform.localPosition = m_OriginalPosition + offset;

            return TaskStatus.Running;
        }

        /// <summary>
        /// Called when the action ends.
        /// </summary>
        public override void OnEnd()
        {
            base.OnEnd();
            if (m_ResolvedCamera != null) {
                m_ResolvedCamera.transform.localPosition = m_OriginalPosition;
            }
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Intensity = 1.0f;
            m_Duration = 1.0f;
            m_Frequency = 10.0f;
        }
    }
}
#endif