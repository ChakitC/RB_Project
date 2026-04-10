#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AudioTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Audio")]
    [Opsive.Shared.Utility.Description("Smoothly changes audio volume over time with curve support.")]
    public class VolumeOverTime : TargetGameObjectAction
    {
        [Tooltip("The target volume.")]
        [SerializeField] protected SharedVariable<float> m_TargetVolume = 1.0f;
        [Tooltip("The duration of the volume change.")]
        [SerializeField] protected SharedVariable<float> m_Duration = 1.0f;
        [Tooltip("The easing curve type.")]
        [SerializeField] protected SmoothMoveTo.EasingType m_EasingType = SmoothMoveTo.EasingType.Linear;

        private AudioSource m_ResolvedAudioSource;
        private float m_StartVolume;
        private float m_ElapsedTime;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedAudioSource = m_ResolvedGameObject.GetComponent<AudioSource>();
        }

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;
            if (m_ResolvedAudioSource != null) {
                m_StartVolume = m_ResolvedAudioSource.volume;
            }
        }

        /// <summary>
        /// Updates the audio volume.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAudioSource == null) {
                return TaskStatus.Failure;
            }

            m_ElapsedTime += Time.deltaTime;
            var progress = Mathf.Clamp01(m_ElapsedTime / m_Duration.Value);
            var easedProgress = ApplyEasing(progress);

            var targetVolume = Mathf.Clamp01(m_TargetVolume.Value);
            m_ResolvedAudioSource.volume = Mathf.Lerp(m_StartVolume, targetVolume, easedProgress);

            return progress >= 1.0f ? TaskStatus.Success : TaskStatus.Running;
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
            m_TargetVolume = 1.0f;
            m_Duration = 1.0f;
            m_EasingType = SmoothMoveTo.EasingType.Linear;
        }
    }
}
#endif