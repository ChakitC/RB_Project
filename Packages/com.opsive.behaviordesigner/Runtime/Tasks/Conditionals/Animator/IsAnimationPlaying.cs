#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.AnimatorTasks
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Animator")]
    [Opsive.Shared.Utility.Description("Checks if an animation is currently playing on the Animator.")]
    public class IsAnimationPlaying : TargetGameObjectConditional
    {
        [Tooltip("The name of the animation state to check.")]
        [SerializeField] protected SharedVariable<string> m_StateName;
        [Tooltip("The layer index of the animation.")]
        [SerializeField] protected SharedVariable<int> m_Layer = 0;
        [Tooltip("Should the condition check if animation is playing (true) or not playing (false)?")]
        [SerializeField] protected SharedVariable<bool> m_IsPlaying = true;

        private Animator m_ResolvedAnimator;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedAnimator = m_ResolvedGameObject.GetComponent<Animator>();
        }

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAnimator == null || string.IsNullOrEmpty(m_StateName.Value)) {
                return TaskStatus.Failure;
            }

            var animatorStateInfo = m_ResolvedAnimator.GetCurrentAnimatorStateInfo(m_Layer.Value);
            var isPlaying = animatorStateInfo.IsName(m_StateName.Value) && animatorStateInfo.normalizedTime < 1f;

            return (isPlaying == m_IsPlaying.Value) ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}
#endif