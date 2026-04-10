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
    [Opsive.Shared.Utility.Description("Sets camera viewport rect with smooth transition.")]
    public class SetViewport : TargetGameObjectAction
    {
        [Tooltip("The target viewport rect (x, y, width, height).")]
        [SerializeField] protected SharedVariable<Rect> m_TargetViewport = new Rect(0, 0, 1, 1);
        [Tooltip("The transition duration (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.0f;

        private Camera m_ResolvedCamera;
        private Rect m_StartViewport;
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
                m_StartViewport = m_ResolvedCamera.rect;
            }
        }

        /// <summary>
        /// Updates the camera viewport.
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
                m_ResolvedCamera.rect = new Rect(
                    Mathf.Lerp(m_StartViewport.x, m_TargetViewport.Value.x, progress),
                    Mathf.Lerp(m_StartViewport.y, m_TargetViewport.Value.y, progress),
                    Mathf.Lerp(m_StartViewport.width, m_TargetViewport.Value.width, progress),
                    Mathf.Lerp(m_StartViewport.height, m_TargetViewport.Value.height, progress)
                );
                return progress >= 1.0f ? TaskStatus.Success : TaskStatus.Running;
            } else {
                m_ResolvedCamera.rect = m_TargetViewport.Value;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_TargetViewport = new Rect(0, 0, 1, 1);
            m_TransitionDuration = 0.0f;
        }
    }
}
#endif