#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AnimatorTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Animator")]
    [Opsive.Shared.Utility.Description("Sets animation speed with smooth transitions and optional curve.")]
    public class SetAnimationSpeed : TargetGameObjectAction
    {
        [Tooltip("The target animation speed multiplier.")]
        [SerializeField] protected SharedVariable<float> m_TargetSpeed = 1.0f;
        [Tooltip("The transition duration for speed change.")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.5f;
        [Tooltip("Whether to use smooth transition.")]
        [SerializeField] protected SharedVariable<bool> m_UseSmoothTransition = true;

        private Animator m_ResolvedAnimator;
        private float m_StartSpeed;
        private float m_ElapsedTime;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedAnimator = m_ResolvedGameObject.GetComponent<Animator>();
        }

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;
            if (m_ResolvedAnimator != null) {
                m_StartSpeed = m_ResolvedAnimator.speed;
            }
        }

        /// <summary>
        /// Sets the animation speed.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAnimator == null) {
                return TaskStatus.Failure;
            }

            if (m_UseSmoothTransition.Value && m_TransitionDuration.Value > 0.0f) {
                m_ElapsedTime += Time.deltaTime;
                var progress = Mathf.Clamp01(m_ElapsedTime / m_TransitionDuration.Value);
                var currentSpeed = Mathf.Lerp(m_StartSpeed, m_TargetSpeed.Value, progress);
                m_ResolvedAnimator.speed = currentSpeed;
                return progress >= 1.0f ? TaskStatus.Success : TaskStatus.Running;
            } else {
                m_ResolvedAnimator.speed = m_TargetSpeed.Value;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_TargetSpeed = 1.0f;
            m_TransitionDuration = 0.5f;
            m_UseSmoothTransition = true;
        }
    }
}
#endif