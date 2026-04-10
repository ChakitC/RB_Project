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
    [Opsive.Shared.Utility.Description("Crossfades between animations with duration, layer, and normalized time control. Optionally waits until the animation completes.")]
    public class Crossfade : TargetGameObjectAction
    {
        [Tooltip("The name of the animation state to crossfade to.")]
        [SerializeField] protected SharedVariable<string> m_StateName;
        [Tooltip("The duration of the crossfade transition.")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.25f;
        [Tooltip("The layer index to crossfade on.")]
        [SerializeField] protected SharedVariable<int> m_Layer = 0;
        [Tooltip("The normalized time to start the animation at.")]
        [SerializeField] protected SharedVariable<float> m_NormalizedTime = 0.0f;
        [Tooltip("Should the action wait until the animation completes before finishing?")]
        [SerializeField] protected SharedVariable<bool> m_WaitForCompletion = false;

        private Animator m_ResolvedAnimator;
        private bool m_HasStartedAnimation;

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
            m_HasStartedAnimation = false;
        }

        /// <summary>
        /// Crossfades to the animation state.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAnimator == null) {
                return TaskStatus.Success;
            }

            if (string.IsNullOrEmpty(m_StateName.Value)) {
                return TaskStatus.Success;
            }

            if (!m_HasStartedAnimation) {
                m_ResolvedAnimator.CrossFade(m_StateName.Value, m_TransitionDuration.Value, m_Layer.Value, m_NormalizedTime.Value);
                m_HasStartedAnimation = true;

                if (!m_WaitForCompletion.Value) {
                    return TaskStatus.Success;
                }
            }

            if (m_WaitForCompletion.Value) {
                var animatorStateInfo = m_ResolvedAnimator.GetCurrentAnimatorStateInfo(m_Layer.Value);
                if (animatorStateInfo.IsName(m_StateName.Value)) {
                    if (animatorStateInfo.loop) {
                        return TaskStatus.Success;
                    }
                    if (animatorStateInfo.normalizedTime < 1.0f) {
                        return TaskStatus.Running;
                    }
                } else {
                    return TaskStatus.Running;
                }
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_StateName = "";
            m_TransitionDuration = 0.25f;
            m_Layer = 0;
            m_NormalizedTime = 0.0f;
            m_WaitForCompletion = false;
        }
    }
}
#endif