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

    [Opsive.Shared.Utility.Category("Audio")]
    [Opsive.Shared.Utility.Description("Manages audio source pooling with automatic cleanup.")]
    public class AudioSourcePool : Action
    {
        [Tooltip("The parent GameObject to create pooled audio sources under.")]
        [SerializeField] protected SharedVariable<GameObject> m_PoolParent;
        [Tooltip("The initial pool size.")]
        [SerializeField] protected SharedVariable<int> m_PoolSize = 5;
        [Tooltip("The maximum pool size.")]
        [SerializeField] protected SharedVariable<int> m_MaxPoolSize = 10;
        [Tooltip("The audio clip to play from the pool.")]
        [SerializeField] protected SharedVariable<AudioClip> m_AudioClip;
        [Tooltip("The volume for the audio.")]
        [SerializeField] protected SharedVariable<float> m_Volume = 1.0f;
        [Tooltip("The currently playing AudioSource from the pool.")]
        [SerializeField] [RequireShared] protected SharedVariable<AudioSource> m_PlayingAudioSource;

        private List<AudioSource> m_Pool = new List<AudioSource>();
        private List<AudioSource> m_ActiveSources = new List<AudioSource>();

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();
            InitializePool();
        }

        /// <summary>
        /// Initializes the audio source pool.
        /// </summary>
        private void InitializePool()
        {
            var poolParent = m_PoolParent.Value != null ? m_PoolParent.Value : m_GameObject;
            for (int i = 0; i < m_PoolSize.Value; ++i) {
                var go = new GameObject($"AudioSource_{i}");
                go.transform.SetParent(poolParent != null ? poolParent.transform : null);
                var audioSource = go.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                m_Pool.Add(audioSource);
            }
        }

        /// <summary>
        /// Gets an available audio source from the pool.
        /// </summary>
        private AudioSource GetPooledAudioSource()
        {
            for (int i = 0; i < m_Pool.Count; ++i) {
                if (!m_Pool[i].isPlaying) {
                    return m_Pool[i];
                }
            }

            if (m_Pool.Count < m_MaxPoolSize.Value) {
                var poolParent = m_PoolParent.Value != null ? m_PoolParent.Value : m_GameObject;
                var go = new GameObject($"AudioSource_{m_Pool.Count}");
                go.transform.SetParent(poolParent != null ? poolParent.transform : null);
                var audioSource = go.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                m_Pool.Add(audioSource);
                return audioSource;
            }

            return null;
        }

        /// <summary>
        /// Updates the audio source pool.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_AudioClip.Value != null) {
                var audioSource = GetPooledAudioSource();
                if (audioSource != null) {
                    audioSource.clip = m_AudioClip.Value;
                    audioSource.volume = m_Volume.Value;
                    audioSource.Play();
                    m_PlayingAudioSource.Value = audioSource;
                    m_ActiveSources.Add(audioSource);
                }
            }

            for (int i = m_ActiveSources.Count - 1; i >= 0; i--) {
                if (m_ActiveSources[i] == null || !m_ActiveSources[i].isPlaying) {
                    m_ActiveSources.RemoveAt(i);
                }
            }

            if (m_PlayingAudioSource.Value != null && !m_PlayingAudioSource.Value.isPlaying) {
                m_PlayingAudioSource.Value = null;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_PoolParent = null;
            m_PoolSize = 5;
            m_MaxPoolSize = 10;
            m_AudioClip = null;
            m_Volume = 1.0f;
            m_PlayingAudioSource = null;
        }
    }
}
#endif