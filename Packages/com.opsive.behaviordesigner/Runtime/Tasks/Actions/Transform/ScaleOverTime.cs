#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Transform")]
    [Opsive.Shared.Utility.Description("Scales the Transform to a target scale over time. Returns Finished when scaled.")]
    public class ScaleOverTime : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the easing curve type for scaling.
        /// </summary>
        public enum EasingType
        {
            Linear,      // Linear scaling.
            EaseIn,      // Slow start, fast end.
            EaseOut,     // Fast start, slow end.
            EaseInOut    // Slow start and end, fast middle.
        }

        [Tooltip("The target scale.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetScale = Vector3.one;
        [Tooltip("The scale speed.")]
        [SerializeField] protected SharedVariable<float> m_ScaleSpeed = 1f;
        [Tooltip("The easing curve type.")]
        [SerializeField] protected EasingType m_EasingType = EasingType.Linear;
        [Tooltip("The arrival threshold for scale.")]
        [SerializeField] protected SharedVariable<float> m_ArrivedDistance = 0.01f;

        private Vector3 m_InitialScale;
        private float m_Progress;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_InitialScale = transform.localScale;
            m_Progress = 0f;
        }

        /// <summary>
        /// Scales the Transform over time.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Progress += m_ScaleSpeed.Value * Time.deltaTime;
            var t = Mathf.Clamp01(m_Progress);

            // Apply easing.
            var easedT = ApplyEasing(t);

            // Lerp scale.
            var newScale = Vector3.Lerp(m_InitialScale, m_TargetScale.Value, easedT);
            transform.localScale = newScale;

            // Check if arrived. Only snap when remaining distance is negligible to avoid visible snap with easing.
            var distance = Vector3.Distance(transform.localScale, m_TargetScale.Value);
            var snapThreshold = Mathf.Min(0.001f, m_ArrivedDistance.Value);
            if (distance < snapThreshold) {
                transform.localScale = m_TargetScale.Value;
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Applies easing to the progress value.
        /// </summary>
        /// <param name="t">The normalized time value (0 to 1).</param>
        /// <returns>The eased value.</returns>
        private float ApplyEasing(float t)
        {
            t = Mathf.Clamp01(t);
            switch (m_EasingType) {
                case EasingType.Linear:
                    return t;
                case EasingType.EaseIn:
                    return t * t;
                case EasingType.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                case EasingType.EaseInOut:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
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
            m_TargetScale = Vector3.one;
            m_ScaleSpeed = 1f;
            m_EasingType = EasingType.Linear;
            m_ArrivedDistance = 0.01f;
        }
    }
}
#endif