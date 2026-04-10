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
    using UnityEngine.Audio;

    [Opsive.Shared.Utility.Category("Audio")]
    [Opsive.Shared.Utility.Description("Sets audio source to mixer group with volume and pitch control.")]
    public class SetMixerGroup : TargetGameObjectAction
    {
        [Tooltip("The AudioMixerGroup to assign.")]
        [SerializeField] protected AudioMixerGroup m_MixerGroup;
        [Tooltip("The volume level.")]
        [SerializeField] protected SharedVariable<float> m_Volume = 1.0f;
        [Tooltip("The pitch level.")]
        [SerializeField] protected SharedVariable<float> m_Pitch = 1.0f;

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
        /// Updates the mixer settings.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAudioSource == null) {
                return TaskStatus.Success;
            }

            m_ResolvedAudioSource.outputAudioMixerGroup = m_MixerGroup;
            m_ResolvedAudioSource.volume = Mathf.Clamp01(m_Volume.Value);
            m_ResolvedAudioSource.pitch = Mathf.Clamp(m_Pitch.Value, 0.1f, 3.0f);

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_MixerGroup = null;
            m_Volume = 1.0f;
            m_Pitch = 1.0f;
        }
    }
}
#endif