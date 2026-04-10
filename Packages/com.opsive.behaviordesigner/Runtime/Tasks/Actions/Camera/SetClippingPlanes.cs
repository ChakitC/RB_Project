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
    [Opsive.Shared.Utility.Description("Sets camera near/far clipping planes with smooth transition.")]
    public class SetClippingPlanes : TargetGameObjectAction
    {
        [Tooltip("The near clipping plane.")]
        [SerializeField] protected SharedVariable<float> m_NearPlane = 0.3f;
        [Tooltip("The far clipping plane.")]
        [SerializeField] protected SharedVariable<float> m_FarPlane = 1000.0f;
        [Tooltip("The transition duration (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.0f;

        private Camera m_ResolvedCamera;
        private float m_StartNearPlane;
        private float m_StartFarPlane;
        private float m_ElapsedTime;

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
            if (m_ResolvedCamera != null) {
                m_StartNearPlane = m_ResolvedCamera.nearClipPlane;
                m_StartFarPlane = m_ResolvedCamera.farClipPlane;
            }
        }

        /// <summary>
        /// Updates the camera clipping planes.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCamera == null) {
                return TaskStatus.Failure;
            }

            if (m_TransitionDuration.Value > 0.0f) {
                m_ElapsedTime += Time.deltaTime;
                var progress = Mathf.Clamp01(m_ElapsedTime / m_TransitionDuration.Value);
                m_ResolvedCamera.nearClipPlane = Mathf.Lerp(m_StartNearPlane, m_NearPlane.Value, progress);
                m_ResolvedCamera.farClipPlane = Mathf.Lerp(m_StartFarPlane, m_FarPlane.Value, progress);
                return progress >= 1.0f ? TaskStatus.Success : TaskStatus.Running;
            } else {
                m_ResolvedCamera.nearClipPlane = m_NearPlane.Value;
                m_ResolvedCamera.farClipPlane = m_FarPlane.Value;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_NearPlane = 0.3f;
            m_FarPlane = 1000.0f;
            m_TransitionDuration = 0.0f;
        }
    }
}
#endif