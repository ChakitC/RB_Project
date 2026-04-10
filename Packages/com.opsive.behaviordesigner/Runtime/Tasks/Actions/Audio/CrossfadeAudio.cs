#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AudioTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Audio")]
    [Opsive.Shared.Utility.Description("Crossfades between two audio sources with duration and volume control.")]
    public class CrossfadeAudio : Action
    {
        [Tooltip("The first AudioSource (currently playing).")]
        [SerializeField] protected SharedVariable<AudioSource> m_AudioSource1;
        [Tooltip("The second AudioSource (to fade in).")]
        [SerializeField] protected SharedVariable<AudioSource> m_AudioSource2;
        [Tooltip("The audio clip to play on the second audio source.")]
        [SerializeField] protected SharedVariable<AudioClip> m_AudioClip;
        [Tooltip("The crossfade duration (seconds).")]
        [SerializeField] protected SharedVariable<float> m_Duration = 1.0f;
        [Tooltip("The target volume for both sources.")]
        [SerializeField] protected SharedVariable<float> m_TargetVolume = 1.0f;

        private float m_ElapsedTime;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;

            if (m_AudioSource2.Value != null && m_AudioClip.Value != null) {
                m_AudioSource2.Value.clip = m_AudioClip.Value;
                m_AudioSource2.Value.volume = 0.0f;
                m_AudioSource2.Value.Play();
            }
        }

        /// <summary>
        /// Updates the crossfade.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_ElapsedTime += Time.deltaTime;
            var progress = Mathf.Clamp01(m_ElapsedTime / m_Duration.Value);

            if (m_AudioSource1.Value != null) {
                m_AudioSource1.Value.volume = Mathf.Lerp(m_TargetVolume.Value, 0.0f, progress);
            }

            if (m_AudioSource2.Value != null) {
                m_AudioSource2.Value.volume = Mathf.Lerp(0.0f, m_TargetVolume.Value, progress);
            }

            if (progress >= 1.0f) {
                if (m_AudioSource1.Value != null) {
                    m_AudioSource1.Value.Stop();
                }
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_AudioSource1 = null;
            m_AudioSource2 = null;
            m_AudioClip = null;
            m_Duration = 1.0f;
            m_TargetVolume = 1.0f;
        }
    }
}
#endif