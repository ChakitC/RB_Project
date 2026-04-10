#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AudioTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Represents a single entry in an audio sequence with clip, volume, and delay settings.
    /// </summary>
    [System.Serializable]
    public class AudioSequenceEntry
    {
        [Tooltip("The audio clip to play.")]
        public SharedVariable<AudioClip> clip;
        [Tooltip("The volume to play the audio clip at (0.0 to 1.0).")]
        public SharedVariable<float> volume = 1.0f;
        [Tooltip("The delay in seconds before playing this audio clip.")]
        public SharedVariable<float> delay = 0.0f;
    }

    [Opsive.Shared.Utility.Category("Audio")]
    [Opsive.Shared.Utility.Description("Plays multiple audio clips in sequence with delays and volume control.")]
    public class PlayAudioSequence : TargetGameObjectAction
    {
        [Tooltip("The list of audio clips to play in sequence.")]
        [SerializeField] protected List<AudioSequenceEntry> m_AudioClips = new List<AudioSequenceEntry>();

        private AudioSource m_ResolvedAudioSource;
        private int m_CurrentIndex;
        private float m_NextPlayTime;
        private bool m_IsPlaying;

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
            m_CurrentIndex = 0;
            m_NextPlayTime = 0.0f;
            m_IsPlaying = false;
        }

        /// <summary>
        /// Updates the audio sequence.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAudioSource == null) {
                return TaskStatus.Success;
            }

            if (m_AudioClips == null || m_AudioClips.Count == 0) {
                return TaskStatus.Success;
            }

            if (m_CurrentIndex >= m_AudioClips.Count) {
                return TaskStatus.Success;
            }

            if (!m_IsPlaying && Time.time >= m_NextPlayTime) {
                var entry = m_AudioClips[m_CurrentIndex];
                if (entry != null && entry.clip.Value != null) {
                    m_ResolvedAudioSource.clip = entry.clip.Value;
                    m_ResolvedAudioSource.volume = entry.volume.Value;
                    m_ResolvedAudioSource.Play();
                    m_IsPlaying = true;
                } else {
                    m_CurrentIndex++;
                    m_IsPlaying = false;
                    m_NextPlayTime = Time.time;
                }
            }

            if (m_IsPlaying && !m_ResolvedAudioSource.isPlaying) {
                m_IsPlaying = false;
                m_CurrentIndex++;
                var entry = m_CurrentIndex < m_AudioClips.Count ? m_AudioClips[m_CurrentIndex] : null;
                var delay = entry != null ? entry.delay.Value : 0.0f;
                m_NextPlayTime = Time.time + delay;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_AudioClips = new List<AudioSequenceEntry>();
        }
    }
}
#endif