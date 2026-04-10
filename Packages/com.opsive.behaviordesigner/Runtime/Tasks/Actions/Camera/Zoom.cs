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
    [Opsive.Shared.Utility.Description("Zooms camera with smooth interpolation and bounds checking.")]
    public class Zoom : TargetGameObjectAction
    {
        [Tooltip("The zoom amount (positive = zoom in, negative = zoom out).")]
        [SerializeField] protected SharedVariable<float> m_ZoomAmount = 0.0f;
        [Tooltip("The minimum FOV.")]
        [SerializeField] protected SharedVariable<float> m_MinFOV = 10.0f;
        [Tooltip("The maximum FOV.")]
        [SerializeField] protected SharedVariable<float> m_MaxFOV = 120.0f;
        [Tooltip("The zoom speed.")]
        [SerializeField] protected SharedVariable<float> m_ZoomSpeed = 5.0f;

        private Camera m_ResolvedCamera;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedCamera = m_ResolvedGameObject.GetComponent<Camera>();
        }

        /// <summary>
        /// Updates the camera zoom.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCamera == null) {
                return TaskStatus.Failure;
            }

            var targetFOV = m_ResolvedCamera.fieldOfView - m_ZoomAmount.Value * m_ZoomSpeed.Value * Time.deltaTime;
            m_ResolvedCamera.fieldOfView = Mathf.Clamp(targetFOV, m_MinFOV.Value, m_MaxFOV.Value);

            return Mathf.Approximately(m_ZoomAmount.Value, 0.0f) ? TaskStatus.Success : TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_ZoomAmount = 0.0f;
            m_MinFOV = 10.0f;
            m_MaxFOV = 120.0f;
            m_ZoomSpeed = 5.0f;
        }
    }
}
#endif