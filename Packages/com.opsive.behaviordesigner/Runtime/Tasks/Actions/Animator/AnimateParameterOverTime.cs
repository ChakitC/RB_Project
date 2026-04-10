#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AnimatorTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Animator")]
    [Opsive.Shared.Utility.Description("Animates a parameter value over time with easing curves.")]
    public class AnimateParameterOverTime : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the parameter type to animate.
        /// </summary>
        public enum ParameterType
        {
            Float,
            Int
        }

        [Tooltip("The type of parameter to animate.")]
        [SerializeField] protected ParameterType m_ParameterType = ParameterType.Float;
        [Tooltip("The name of the parameter to animate.")]
        [SerializeField] protected SharedVariable<string> m_ParameterName;
        [Tooltip("The starting value.")]
        [SerializeField] protected SharedVariable<float> m_StartValue = 0.0f;
        [Tooltip("The target value.")]
        [SerializeField] protected SharedVariable<float> m_TargetValue = 1.0f;
        [Tooltip("The duration of the animation.")]
        [SerializeField] protected SharedVariable<float> m_Duration = 1.0f;
        [Tooltip("The easing curve type.")]
        [SerializeField] protected SmoothMoveTo.EasingType m_EasingType = SmoothMoveTo.EasingType.Linear;

        private Animator m_ResolvedAnimator;
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
        }

        /// <summary>
        /// Animates the parameter value.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAnimator == null) {
                return TaskStatus.Failure;
            }

            if (string.IsNullOrEmpty(m_ParameterName.Value)) {
                return TaskStatus.Failure;
            }

            m_ElapsedTime += Time.deltaTime;
            var progress = Mathf.Clamp01(m_ElapsedTime / m_Duration.Value);
            var easedProgress = ApplyEasing(progress);

            var currentValue = Mathf.Lerp(m_StartValue.Value, m_TargetValue.Value, easedProgress);

            if (m_ParameterType == ParameterType.Float) {
                m_ResolvedAnimator.SetFloat(m_ParameterName.Value, currentValue);
            } else {
                m_ResolvedAnimator.SetInteger(m_ParameterName.Value, Mathf.RoundToInt(currentValue));
            }

            if (progress >= 1.0f) {
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
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
            m_ParameterType = ParameterType.Float;
            m_ParameterName = null;
            m_StartValue = 0.0f;
            m_TargetValue = 1.0f;
            m_Duration = 1.0f;
            m_EasingType = SmoothMoveTo.EasingType.Linear;
        }
    }
}
#endif