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
    [Opsive.Shared.Utility.Description("Blends animations with weight control and optional time offset.")]
    public class Blend : TargetGameObjectAction
    {
        [Tooltip("The name of the animation state to blend to.")]
        [SerializeField] protected SharedVariable<string> m_StateName;
        [Tooltip("The blend weight (0 = original state, 1 = new state).")]
        [SerializeField] protected SharedVariable<float> m_BlendWeight = 1.0f;
        [Tooltip("The layer index to blend on.")]
        [SerializeField] protected SharedVariable<int> m_Layer = 0;
        [Tooltip("The normalized time offset to start the animation at.")]
        [SerializeField] protected SharedVariable<float> m_TimeOffset = 0.0f;
        [Tooltip("The transition duration for the blend.")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.25f;

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
        /// Blends the animation.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAnimator == null) {
                return TaskStatus.Success;
            }

            if (!string.IsNullOrEmpty(m_StateName.Value)) {
                var blendWeight = Mathf.Clamp01(m_BlendWeight.Value);

                m_ResolvedAnimator.CrossFade(m_StateName.Value, m_TransitionDuration.Value, m_Layer.Value, m_TimeOffset.Value);
                m_ResolvedAnimator.SetLayerWeight(m_Layer.Value, blendWeight);
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_StateName = null;
            m_BlendWeight = 1.0f;
            m_Layer = 0;
            m_TimeOffset = 0.0f;
            m_TransitionDuration = 0.25f;
        }
    }
}
#endif