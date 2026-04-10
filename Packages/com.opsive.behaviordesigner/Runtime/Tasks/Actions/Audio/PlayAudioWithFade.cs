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
    [Opsive.Shared.Utility.Description("Plays audio clip with fade in/out, volume control, and optional looping.")]
    public class PlayAudioWithFade : TargetGameObjectAction
    {
        [Tooltip("The audio clip to play.")]
        [SerializeField] protected SharedVariable<AudioClip> m_AudioClip;
        [Tooltip("The target volume.")]
        [SerializeField] protected SharedVariable<float> m_Volume = 1.0f;
        [Tooltip("Whether to loop the audio.")]
        [SerializeField] protected SharedVariable<bool> m_Loop = false;
        [Tooltip("The fade in duration (seconds).")]
        [SerializeField] protected SharedVariable<float> m_FadeInDuration = 0.5f;
        [Tooltip("The fade out duration (seconds). Set to 0 to disable fade out.")]
        [SerializeField] protected SharedVariable<float> m_FadeOutDuration = 0.0f;
        [Tooltip("Whether the audio is currently playing.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_IsPlaying;

        private AudioSource m_ResolvedAudioSource;
        private float m_ElapsedTime;
        private bool m_IsFadingIn;
        private bool m_IsFadingOut;

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
            m_IsFadingIn = true;
            m_IsFadingOut = false;
            m_IsPlaying.Value = false;

            if (m_ResolvedAudioSource != null && m_AudioClip.Value != null) {
                m_ResolvedAudioSource.clip = m_AudioClip.Value;
                m_ResolvedAudioSource.loop = m_Loop.Value;
                m_ResolvedAudioSource.volume = 0.0f;
                m_ResolvedAudioSource.Play();
                m_IsPlaying.Value = true;
            }
        }

        /// <summary>
        /// Updates the audio playback and fade.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAudioSource == null) {
                return TaskStatus.Failure;
            }

            if (!m_ResolvedAudioSource.isPlaying && !m_IsFadingIn) {
                m_IsPlaying.Value = false;
                return TaskStatus.Success;
            }

            m_ElapsedTime += Time.deltaTime;

            if (m_IsFadingIn && m_FadeInDuration.Value > 0.0f) {
                var fadeProgress = Mathf.Clamp01(m_ElapsedTime / m_FadeInDuration.Value);
                m_ResolvedAudioSource.volume = Mathf.Lerp(0.0f, m_Volume.Value, fadeProgress);

                if (fadeProgress >= 1.0f) {
                    m_IsFadingIn = false;
                    m_ElapsedTime = 0.0f;
                }
            } else if (!m_IsFadingIn && !m_IsFadingOut) {
                m_ResolvedAudioSource.volume = m_Volume.Value;
            }

            if (m_FadeOutDuration.Value > 0.0f && !m_ResolvedAudioSource.loop && m_ResolvedAudioSource.clip != null) {
                var timeRemaining = m_ResolvedAudioSource.clip.length - m_ResolvedAudioSource.time;
                if (timeRemaining <= m_FadeOutDuration.Value && !m_IsFadingOut) {
                    m_IsFadingOut = true;
                    m_ElapsedTime = 0.0f;
                }

                if (m_IsFadingOut) {
                    var fadeProgress = Mathf.Clamp01(m_ElapsedTime / m_FadeOutDuration.Value);
                    m_ResolvedAudioSource.volume = Mathf.Lerp(m_Volume.Value, 0.0f, fadeProgress);

                    if (fadeProgress >= 1.0f) {
                        m_ResolvedAudioSource.Stop();
                        m_IsPlaying.Value = false;
                        return TaskStatus.Success;
                    }
                }
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_AudioClip = null;
            m_Volume = 1.0f;
            m_Loop = false;
            m_FadeInDuration = 0.5f;
            m_FadeOutDuration = 0.0f;
            m_IsPlaying = null;
        }
    }
}
#endif