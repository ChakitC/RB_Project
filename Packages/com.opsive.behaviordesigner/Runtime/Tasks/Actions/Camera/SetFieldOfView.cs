#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.CameraTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Camera")]
    [Opsive.Shared.Utility.Description("Sets camera FOV with smooth transition and optional curve.")]
    public class SetFieldOfView : TargetGameObjectAction
    {
        [Tooltip("The target field of view.")]
        [SerializeField] protected SharedVariable<float> m_TargetFOV = 60.0f;
        [Tooltip("The transition duration (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.0f;
        [Tooltip("The easing curve type.")]
        [SerializeField] protected SmoothMoveTo.EasingType m_EasingType = SmoothMoveTo.EasingType.Linear;

        private Camera m_ResolvedCamera;
        private float m_StartFOV;
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
                m_StartFOV = m_ResolvedCamera.fieldOfView;
            }
        }

        /// <summary>
        /// Updates the camera FOV.
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
                var easedProgress = ApplyEasing(progress);
                m_ResolvedCamera.fieldOfView = Mathf.Lerp(m_StartFOV, m_TargetFOV.Value, easedProgress);
                return progress >= 1.0f ? TaskStatus.Success : TaskStatus.Running;
            } else {
                m_ResolvedCamera.fieldOfView = m_TargetFOV.Value;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Applies easing to the value.
        /// </summary>
        private float ApplyEasing(float t)
        {
            switch (m_EasingType) {
                case SmoothMoveTo.EasingType.EaseIn:
                    return t * t;
                case SmoothMoveTo.EasingType.EaseOut:
                    return 1.0f - (1.0f - t) * (1.0f - t);
                case SmoothMoveTo.EasingType.EaseInOut:
                    return t < 0.5f ? 2.0f * t * t : 1.0f - Mathf.Pow(-2.0f * t + 2.0f, 2.0f) / 2.0f;
                default:
                    return t;
            }
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_TargetFOV = 60.0f;
            m_TransitionDuration = 0.0f;
            m_EasingType = SmoothMoveTo.EasingType.Linear;
        }
    }
}
#endif