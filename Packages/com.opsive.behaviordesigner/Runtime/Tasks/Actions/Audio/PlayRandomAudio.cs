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
    [Opsive.Shared.Utility.Description("Plays random audio from array with pitch/volume variation.")]
    public class PlayRandomAudio : TargetGameObjectAction
    {
        [Tooltip("The list of audio clips to randomly select from.")]
        [SerializeField] protected List<AudioClip> m_AudioClips = new List<AudioClip>();
        [Tooltip("The base volume.")]
        [SerializeField] protected SharedVariable<float> m_BaseVolume = 1.0f;
        [Tooltip("The volume variation range (random value between -variation and +variation).")]
        [SerializeField] protected SharedVariable<float> m_VolumeVariation = 0.1f;
        [Tooltip("The base pitch.")]
        [SerializeField] protected SharedVariable<float> m_BasePitch = 1.0f;
        [Tooltip("The pitch variation range (random value between -variation and +variation).")]
        [SerializeField] protected SharedVariable<float> m_PitchVariation = 0.1f;
        [Tooltip("Whether to loop the audio.")]
        [SerializeField] protected SharedVariable<bool> m_Loop = false;

        private AudioSource m_ResolvedAudioSource;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedAudioSource = m_ResolvedGameObject.GetComponent<AudioSource>();
        }

        /// <summary>
        /// Updates the action.
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

            var randomIndex = Random.Range(0, m_AudioClips.Count);
            var selectedClip = m_AudioClips[randomIndex];
            if (selectedClip != null) {
                m_ResolvedAudioSource.clip = selectedClip;
                m_ResolvedAudioSource.loop = m_Loop.Value;
                m_ResolvedAudioSource.volume = Mathf.Clamp01(m_BaseVolume.Value + Random.Range(-m_VolumeVariation.Value, m_VolumeVariation.Value));
                m_ResolvedAudioSource.pitch = Mathf.Clamp(m_BasePitch.Value + Random.Range(-m_PitchVariation.Value, m_PitchVariation.Value), 0.1f, 3.0f);
                m_ResolvedAudioSource.Play();
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_AudioClips = new List<AudioClip>();
            m_BaseVolume = 1.0f;
            m_VolumeVariation = 0.1f;
            m_BasePitch = 1.0f;
            m_PitchVariation = 0.1f;
            m_Loop = false;
        }
    }
}
#endif