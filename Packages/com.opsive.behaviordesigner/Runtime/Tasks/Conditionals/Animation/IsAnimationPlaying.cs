#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.AnimationTasks
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Animation")]
    [Opsive.Shared.Utility.Description("Checks if an animation is currently playing on the Animation component.")]
    public class IsAnimationPlaying : TargetGameObjectConditional
    {
        [Tooltip("The name of the animation clip to check.")]
        [SerializeField] protected SharedVariable<string> m_AnimationName;
        [Tooltip("Should the condition check if animation is playing (true) or not playing (false)?")]
        [SerializeField] protected SharedVariable<bool> m_IsPlaying = true;

        private UnityEngine.Animation m_ResolvedAnimation;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedAnimation = m_ResolvedGameObject.GetComponent<UnityEngine.Animation>();
        }

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAnimation == null || string.IsNullOrEmpty(m_AnimationName.Value)) {
                return TaskStatus.Failure;
            }

            var isPlaying = m_ResolvedAnimation.IsPlaying(m_AnimationName.Value);
            return (isPlaying == m_IsPlaying.Value) ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}
#endif