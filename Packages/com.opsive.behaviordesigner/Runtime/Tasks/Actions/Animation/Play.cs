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

    /// <summary>
    /// Specifies whether to play an animation by name or by index.
    /// </summary>
    public enum AnimationPlayType
    {
        ByName,
        ByIndex
    }

    [Opsive.Shared.Utility.Category("Animation")]
    [Opsive.Shared.Utility.Description("Plays an animation on the Animation component. Optionally waits until the animation completes.")]
    public class Play : TargetGameObjectAction
    {
        [Tooltip("Specifies whether to play the animation by name or by index.")]
        [SerializeField] protected AnimationPlayType m_PlayType = AnimationPlayType.ByName;
        [Tooltip("The name of the animation clip to play.")]
        [SerializeField] protected SharedVariable<string> m_AnimationName;
        [Tooltip("The index of the animation clip to play.")]
        [SerializeField] protected SharedVariable<int> m_AnimationIndex = 0;
        [Tooltip("The play mode for the animation.")]
        [SerializeField] protected PlayMode m_PlayMode = PlayMode.StopSameLayer;
        [Tooltip("Should the action wait until the animation completes before finishing?")]
        [SerializeField] protected SharedVariable<bool> m_WaitForCompletion = false;

        private UnityEngine.Animation m_ResolvedAnimation;
        private bool m_HasStartedAnimation;
        private string m_PlayingAnimationName;

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
        /// Gets the animation clip name at the specified index.
        /// </summary>
        /// <param name="index">The index of the animation clip.</param>
        /// <returns>The name of the animation at the index, or null if the index is invalid.</returns>
        private string GetAnimationNameAtIndex(int index)
        {
            var currentIndex = 0;
            foreach (AnimationState state in m_ResolvedAnimation) {
                if (currentIndex == index) {
                    return state.name;
                }
                currentIndex++;
            }
            return null;
        }

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAnimation == null) {
                return TaskStatus.Success;
            }

            string animationName = null;
            if (m_PlayType == AnimationPlayType.ByName) {
                animationName = m_AnimationName.Value;
                if (string.IsNullOrEmpty(animationName)) {
                    return TaskStatus.Success;
                }
            } else {
                animationName = GetAnimationNameAtIndex(m_AnimationIndex.Value);
                if (animationName == null) {
                    return TaskStatus.Success;
                }
            }

            if (!m_HasStartedAnimation) {
                if (m_ResolvedAnimation[animationName] == null) {
                    return TaskStatus.Success;
                }

                m_ResolvedAnimation.Play(animationName, m_PlayMode);
                m_HasStartedAnimation = true;
                m_PlayingAnimationName = animationName;

                if (!m_WaitForCompletion.Value) {
                    return TaskStatus.Success;
                }
            }

            if (m_WaitForCompletion.Value) {
                if (m_ResolvedAnimation.IsPlaying(m_PlayingAnimationName)) {
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
            m_PlayType = AnimationPlayType.ByName;
            m_AnimationName = "";
            m_AnimationIndex = 0;
            m_PlayMode = PlayMode.StopSameLayer;
            m_WaitForCompletion = false;
        }
    }
}
#endif