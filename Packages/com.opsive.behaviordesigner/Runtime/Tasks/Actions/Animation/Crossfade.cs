#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AnimationTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Animation")]
    [Opsive.Shared.Utility.Description("Crossfades between animations on the Animation component. Optionally waits until the animation completes.")]
    public class Crossfade : TargetGameObjectAction
    {
        [Tooltip("The name of the animation clip to crossfade to.")]
        [SerializeField] protected SharedVariable<string> m_AnimationName;
        [Tooltip("The duration of the crossfade transition.")]
        [SerializeField] protected SharedVariable<float> m_FadeLength = 0.3f;
        [Tooltip("The play mode for the animation.")]
        [SerializeField] protected PlayMode m_PlayMode = PlayMode.StopSameLayer;
        [Tooltip("Should the action wait until the animation completes before finishing?")]
        [SerializeField] protected SharedVariable<bool> m_WaitForCompletion = false;

        private UnityEngine.Animation m_ResolvedAnimation;
        private bool m_HasStartedAnimation;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedAnimation = m_ResolvedGameObject.GetComponent<UnityEngine.Animation>();
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
            if (m_ResolvedAnimation == null) {
                return TaskStatus.Success;
            }

            if (string.IsNullOrEmpty(m_AnimationName.Value)) {
                return TaskStatus.Success;
            }

            if (!m_HasStartedAnimation) {
                if (m_ResolvedAnimation[m_AnimationName.Value] == null) {
                    return TaskStatus.Success;
                }

                m_ResolvedAnimation.CrossFade(m_AnimationName.Value, m_FadeLength.Value, m_PlayMode);
                m_HasStartedAnimation = true;

                if (!m_WaitForCompletion.Value) {
                    return TaskStatus.Success;
                }
            }

            if (m_WaitForCompletion.Value) {
                if (m_ResolvedAnimation.IsPlaying(m_AnimationName.Value)) {
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
            m_AnimationName = "";
            m_FadeLength = 0.3f;
            m_PlayMode = PlayMode.StopSameLayer;
            m_WaitForCompletion = false;
        }
    }
}
#endif